using MediatR;
using Microsoft.Extensions.Logging;
using PagueVeloz.Core.Application.Commands.Transactions;
using PagueVeloz.Core.Application.DTOs.Transaction;
using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Interfaces;
using System.Collections.Concurrent;

namespace PagueVeloz.Core.Application.Handlers.Transactions
{
    public class TransferCommandHandler : IRequestHandler<TransferCommand, TransactionResponse>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILogger _logger;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

        public TransferCommandHandler(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ILogger<TransferCommandHandler> logger)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _logger = logger;
        }

        public async Task<TransactionResponse> Handle(TransferCommand command, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Validation if value is greater than zero cents");
            var minAmountResponse = TransactionValidationHelper.ValidateMinimumAmount(command.SourceAccountId, command.Amount, _logger, "crédito");
            if (minAmountResponse != null)
                return minAmountResponse;

            var sourceLock = _accountLocks.GetOrAdd(command.SourceAccountId, _ => new SemaphoreSlim(1, 1));
            var destinationLock = _accountLocks.GetOrAdd(command.TargetAccountId, _ => new SemaphoreSlim(1, 1));

            await Task.WhenAll(sourceLock.WaitAsync(), destinationLock.WaitAsync());

            try
            {
                var sourceResponse = await _accountRepository.GetByIdAsync(command.SourceAccountId);
                var destinationResponse = await _accountRepository.GetByIdAsync(command.TargetAccountId);

                if (!sourceResponse.Success || sourceResponse.Data == null)
                {
                    return new TransactionResponse
                    {
                        Status = "failed",
                        ErrorMessage = $"Conta de origem não encontrada: {command.SourceAccountId}",
                        TransactionId = $"TXN-{command.SourceAccountId}-FAILED",
                        Balance = 0,
                        ReservedBalance = 0,
                        AvailableBalance = 0,
                        Timestamp = DateTime.UtcNow
                    };
                }

                if (!destinationResponse.Success || destinationResponse.Data == null)
                {
                    return new TransactionResponse
                    {
                        Status = "failed",
                        ErrorMessage = $"Conta de destino não encontrada: {command.TargetAccountId}",
                        TransactionId = $"TXN-{command.TargetAccountId}-FAILED",
                        Balance = 0,
                        ReservedBalance = 0,
                        AvailableBalance = 0,
                        Timestamp = DateTime.UtcNow
                    };
                }

                var sourceAccount = sourceResponse.Data;
                var destinationAccount = destinationResponse.Data;

                if (sourceAccount.AvailableBalance < command.Amount)
                {
                    return new TransactionResponse
                    {
                        Status = "failed",
                        ErrorMessage = "Saldo insuficiente na conta de origem.",
                        TransactionId = $"TXN-{command.SourceAccountId}-FAILED",
                        Balance = sourceAccount.AvailableBalance + sourceAccount.ReservedBalance,
                        ReservedBalance = sourceAccount.ReservedBalance,
                        AvailableBalance = sourceAccount.AvailableBalance,
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Debita da conta de origem
                sourceAccount.AvailableBalance -= command.Amount;
                await _accountRepository.UpdateAsync(sourceAccount);

                // Credita na conta de destino
                destinationAccount.AvailableBalance += command.Amount;
                await _accountRepository.UpdateAsync(destinationAccount);

                // Registra transação de débito na origem
                var debitTransaction = new Transaction
                {
                    AccountId = sourceAccount.AccountId,
                    Type = Core.Domain.Enums.TransactionType.Transfer,
                    Amount = command.Amount,
                    Description = $"Transferência para {destinationAccount.AccountId}: {command.Description}",
                    ReferenceId = command.ReferenceId
                };
                var debitTransactionId = await _transactionRepository.AddAsyncTransactionRegistry(debitTransaction);

                // Registra transação de crédito na destino
                var creditTransaction = new Transaction
                {
                    AccountId = destinationAccount.AccountId,
                    Type = Core.Domain.Enums.TransactionType.Transfer,
                    Amount = command.Amount,
                    Description = $"Transferência recebida de {sourceAccount.AccountId}: {command.Description}",
                    ReferenceId = command.ReferenceId
                };
                var creditTransactionId = await _transactionRepository.AddAsyncTransactionRegistry(creditTransaction);

                return new TransactionResponse
                {
                    TransactionId = $"{debitTransactionId}|{creditTransactionId}",
                    Status = "success",
                    Balance = sourceAccount.AvailableBalance + sourceAccount.ReservedBalance,
                    ReservedBalance = sourceAccount.ReservedBalance,
                    AvailableBalance = sourceAccount.AvailableBalance,
                    Timestamp = DateTime.UtcNow,
                    ErrorMessage = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer failed from {SourceAccountId} to {DestinationAccountId}", command.SourceAccountId, command.TargetAccountId);
                return new TransactionResponse
                {
                    Status = "failed",
                    ErrorMessage = ex.Message,
                    TransactionId = $"TXN-{command.SourceAccountId}-FAILED",
                    Balance = 0,
                    ReservedBalance = 0,
                    AvailableBalance = 0,
                    Timestamp = DateTime.UtcNow
                };
            }
            finally
            {
                sourceLock.Release();
                destinationLock.Release();
            }
        }
    }
}