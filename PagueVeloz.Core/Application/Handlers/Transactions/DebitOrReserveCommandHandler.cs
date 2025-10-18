using MediatR;
using Microsoft.Extensions.Logging;
using PagueVeloz.Core.Application.Commands.Transactions;
using PagueVeloz.Core.Application.DTOs.Transaction;
using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Enums;
using PagueVeloz.Core.Domain.Interfaces;
using System.Collections.Concurrent;

namespace PagueVeloz.Core.Application.Handlers.Transactions
{
    public class DebitOrReserveCommandHandler : IRequestHandler<DebitOrReserveCommand, TransactionResponse>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILogger _logger;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

        public DebitOrReserveCommandHandler(IAccountRepository accountRepository, ITransactionRepository transactionRepository, ILogger<DebitOrReserveCommandHandler> logger)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _logger = logger;
        }

        public async Task<TransactionResponse> Handle(DebitOrReserveCommand command, CancellationToken cancellationToken)
        {
            string operationType = command.IsReservation ? "reserva" : "débito";

            _logger.LogInformation("Validation if value is greater than zero cents");
            var minAmountResponse = TransactionValidationHelper.ValidateMinimumAmount(command.AccountId, command.Amount, _logger, operationType);
            if (minAmountResponse != null)
                return minAmountResponse;

            var accountLock = _accountLocks.GetOrAdd(command.AccountId, _ => new SemaphoreSlim(1, 1));
            await accountLock.WaitAsync();

            try
            {
                _logger.LogInformation("Processing debit for AccountId {AccountId} with Amount {Amount}", command.AccountId, command.Amount);

                var accountResponse = await _accountRepository.GetByIdAsync(command.AccountId);
                if (!accountResponse.Success || accountResponse.Data == null)
                {
                    _logger.LogWarning("Failed to fetch account {AccountId}. Error: {ErrorMessage}", command.AccountId, accountResponse.ErrorMessage);
                    return new TransactionResponse
                    {
                        TransactionId = $"TXN-{command.AccountId}-FAILED",
                        Status = "failed",
                        ErrorMessage = accountResponse.ErrorMessage ?? "Erro sem mensagem, entre em contato com o TI",
                        Balance = 0,
                        ReservedBalance = 0,
                        AvailableBalance = 0,
                        Timestamp = DateTime.UtcNow
                    };
                }

                var account = accountResponse.Data;

                // Regra: saldo disponível + limite de crédito
                var totalLimit = account.AvailableBalance + account.CreditLimit;
                if (command.Amount > totalLimit)
                {
                    string errorMessage = $"Saldo insuficiente para {operationType} considerando limite de crédito e saldo da conta disponível.";
                    _logger.LogWarning("Debit transaction rejected: Amount exceeds available balance + credit limit for AccountId {AccountId}. Amount: {Amount}, TotalLimit: {TotalLimit}", command.AccountId, command.Amount, totalLimit);

                    return new TransactionResponse
                    {
                        TransactionId = $"TXN-{command.AccountId}-FAILED",
                        Status = "failed",
                        ErrorMessage = errorMessage,
                        Balance = account.AvailableBalance + account.ReservedBalance,
                        ReservedBalance = account.ReservedBalance,
                        AvailableBalance = account.AvailableBalance,
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Operação aprovada
                account.AvailableBalance -= command.Amount;

                if (command.IsReservation) //Se for reserva, adiciona ao saldo reservado
                {
                    account.ReservedBalance += command.Amount;
                } else
                {
                    account.CreditLimit -= command.Amount;
                }

                if (account.AvailableBalance < 0)
                    account.AvailableBalance = 0; // Não permite saldo negativo

                await _accountRepository.UpdateAsync(account);

                var transaction = new Transaction
                {
                    AccountId = account.AccountId,
                    Type = TransactionType.Debit,
                    Amount = command.Amount,
                    Description = command.Description
                };

                _logger.LogInformation("Recording transaction for AccountId {AccountId} with Amount {Amount}", command.AccountId, command.Amount);
                string transactionId = await _transactionRepository.AddAsyncTransactionRegistry(transaction);

                return new TransactionResponse
                {
                    TransactionId = $"{transactionId}-PROCESSED",
                    Status = "success",
                    Balance = account.AvailableBalance + account.ReservedBalance,
                    ReservedBalance = account.ReservedBalance,
                    AvailableBalance = account.AvailableBalance,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{operationType} transaction failed for AccountId {AccountId}", operationType, command.AccountId);
                return new TransactionResponse
                {
                    TransactionId = $"TXN-{command.AccountId}-FAILED",
                    Status = "failed",
                    Balance = 0,
                    ReservedBalance = 0,
                    AvailableBalance = 0,
                    Timestamp = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                accountLock.Release();
            }
        }
    }
}