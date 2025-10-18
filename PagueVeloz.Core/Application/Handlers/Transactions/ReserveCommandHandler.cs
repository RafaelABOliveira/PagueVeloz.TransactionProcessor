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
    public class ReserveCommandHandler : IRequestHandler<ReserveCommand, TransactionResponse>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILogger _logger;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

        public ReserveCommandHandler(IAccountRepository accountRepository, ITransactionRepository transactionRepository, ILogger<ReserveCommandHandler> logger)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _logger = logger;
        }

        public async Task<TransactionResponse> Handle(ReserveCommand command, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Validation if value is greater than zero cents");
            var minAmountResponse = TransactionValidationHelper.ValidateMinimumAmount(command.AccountId, command.Amount, _logger, "reserva");
            if (minAmountResponse != null)
                return minAmountResponse;

            var accountLock = _accountLocks.GetOrAdd(command.AccountId, _ => new SemaphoreSlim(1, 1));
            await accountLock.WaitAsync();

            try
            {
                _logger.LogInformation("Processing reserve for AccountId {AccountId} with Amount {Amount}", command.AccountId, command.Amount);

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
                if (command.Amount > account.AvailableBalance)
                {
                    string errorMessage = "Saldo insuficiente para reserva considerando apenas o saldo disponível.";
                    _logger.LogWarning("Reserve transaction rejected: Amount exceeds available balance for AccountId {AccountId}. Amount: {Amount}, Available: {AvailableBalance}", command.AccountId, command.Amount, account.AvailableBalance);

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

                account.AvailableBalance -= command.Amount;
                account.ReservedBalance += command.Amount;

                await _accountRepository.UpdateAsync(account);

                var transaction = new Transaction
                {
                    AccountId = account.AccountId,
                    Type = TransactionType.Reserve,
                    Amount = command.Amount,
                    Description = command.Description
                };

                _logger.LogInformation("Recording reserve transaction for AccountId {AccountId} with Amount {Amount}", command.AccountId, command.Amount);
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
                _logger.LogError(ex, "reserve transaction failed for AccountId {AccountId}", command.AccountId);
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