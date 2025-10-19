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
    public class ReversalCommandHandler : IRequestHandler<ReversalCommand, TransactionResponse>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILogger _logger;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _accountLocks = new();

        public ReversalCommandHandler(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ILogger<ReversalCommandHandler> logger)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _logger = logger;
        }

        public async Task<TransactionResponse> Handle(ReversalCommand command, CancellationToken cancellationToken)
        {
            var accountLock = _accountLocks.GetOrAdd(command.AccountId, _ => new SemaphoreSlim(1, 1));
            await accountLock.WaitAsync();

            try
            {
                var originalTransaction = await _transactionRepository.GetByReferenceIdAsync(command.ReferenceId, cancellationToken);
                if (originalTransaction == null)
                {
                    _logger.LogWarning("Original transaction not found for ReferenceId {ReferenceId}", command.ReferenceId);
                    return new TransactionResponse
                    {
                        TransactionId = $"TXN-{command.AccountId}-FAILED",
                        Status = "failed",
                        ErrorMessage = "Transação original não encontrada para estorno.",
                        Balance = 0,
                        ReservedBalance = 0,
                        AvailableBalance = 0,
                        Timestamp = DateTime.UtcNow
                    };
                }

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

                // Aplica o estorno conforme o tipo da transação original
                switch (originalTransaction.Type)
                {
                    case TransactionType.Credit:
                        if (account.AvailableBalance < originalTransaction.Amount)
                        {
                            return new TransactionResponse
                            {
                                TransactionId = $"TXN-{command.AccountId}-FAILED",
                                Status = "failed",
                                ErrorMessage = "Saldo insuficiente para estorno de crédito.",
                                Balance = account.AvailableBalance + account.ReservedBalance,
                                ReservedBalance = account.ReservedBalance,
                                AvailableBalance = account.AvailableBalance,
                                Timestamp = DateTime.UtcNow
                            };
                        }

                        account.AvailableBalance -= originalTransaction.Amount;
                        break;

                    case TransactionType.Debit:
                        account.AvailableBalance += originalTransaction.Amount;
                        break;

                    case TransactionType.Reserve:
                        account.ReservedBalance -= originalTransaction.Amount;
                        account.AvailableBalance += originalTransaction.Amount;
                        break;

                    case TransactionType.Capture:
                        if (account.AvailableBalance < originalTransaction.Amount)
                        {
                            return new TransactionResponse
                            {
                                TransactionId = $"TXN-{command.AccountId}-FAILED",
                                Status = "failed",
                                ErrorMessage = "Saldo insuficiente para estorno de captura de reserva.",
                                Balance = account.AvailableBalance + account.ReservedBalance,
                                ReservedBalance = account.ReservedBalance,
                                AvailableBalance = account.AvailableBalance,
                                Timestamp = DateTime.UtcNow
                            };
                        }
                        account.AvailableBalance -= originalTransaction.Amount;
                        account.ReservedBalance += originalTransaction.Amount;
                        break;

                    default:
                        return new TransactionResponse
                        {
                            TransactionId = $"TXN-{command.AccountId}-FAILED",
                            Status = "failed",
                            ErrorMessage = "Tipo de transação original não suportado para estorno.",
                            Balance = account.AvailableBalance + account.ReservedBalance,
                            ReservedBalance = account.ReservedBalance,
                            AvailableBalance = account.AvailableBalance,
                            Timestamp = DateTime.UtcNow
                        };
                }

                await _accountRepository.UpdateAsync(account);

                var reversalTransaction = new Transaction
                {
                    AccountId = account.AccountId,
                    Type = TransactionType.Reversal,
                    Amount = originalTransaction.Amount,
                    Description = command.Description,
                    ReferenceId = command.ReferenceId
                };

                _logger.LogInformation("Recording reversal transaction for AccountId {AccountId} with Amount {Amount}", command.AccountId, command.Amount);
                string transactionId = await _transactionRepository.AddAsyncTransactionRegistry(reversalTransaction);

                return new TransactionResponse
                {
                    TransactionId = $"{transactionId}",
                    Status = "success",
                    Balance = account.AvailableBalance + account.ReservedBalance,
                    ReservedBalance = account.ReservedBalance,
                    AvailableBalance = account.AvailableBalance,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reversal transaction failed for AccountId {AccountId}", command.AccountId);
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