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
    public class CreditCommandHandler : IRequestHandler<CreditCommand, TransactionResponse>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILogger _logger;

        public CreditCommandHandler(IAccountRepository accountRepository, ITransactionRepository transactionRepository, ILogger<CreditCommandHandler> logger)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _logger = logger;
        }

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

        public async Task<TransactionResponse> Handle(CreditCommand command, CancellationToken cancellationToken)
        {

            if (command.Amount < 1)
            {
                const string errorMessage = "O valor do crédito deve ser igual ou superior a 1 centavo (Amount >= 1).";
                _logger.LogWarning("Credit transaction rejected: Amount invalid for AccountId {AccountId}. Amount: {Amount}", command.AccountId, command.Amount);

                return new TransactionResponse
                {
                    TransactionId = $"TXN-{command.AccountId}-REJECTED",
                    Status = "rejected",
                    ErrorMessage = errorMessage,
                    Timestamp = DateTime.UtcNow
                };
            }

            var accountLock = _accountLocks.GetOrAdd(command.AccountId, _ => new SemaphoreSlim(1, 1));
            await accountLock.WaitAsync();

            try
            {
                _logger.LogInformation("Processing credit for AccountId {AccountId} with Amount {Amount}", command.AccountId, command.Amount);

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

                account.AvailableBalance += command.Amount;
                await _accountRepository.UpdateAsync(account);

                var nextId = await _transactionRepository.GenerateNextTransactionIdAsync();
                var transaction = new Transaction
                {
                    TransactionId = $"TXN-{nextId:D3}",
                    AccountId = account.AccountId,
                    Type = TransactionType.Credit,
                    Amount = command.Amount,
                    Description = command.Description
                };

                _logger.LogInformation("Recording transaction for AccountId {AccountId} with Amount {Amount}", command.AccountId, command.Amount);
                await _transactionRepository.AddAsync(transaction);

                return new TransactionResponse
                {
                    TransactionId = $"TXN-{nextId:D3}-PROCESSED",
                    Status = "success",
                    Balance = account.AvailableBalance + account.ReservedBalance,
                    ReservedBalance = account.ReservedBalance,
                    AvailableBalance = account.AvailableBalance,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Credit transaction failed for AccountId {AccountId}", command.AccountId);
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
