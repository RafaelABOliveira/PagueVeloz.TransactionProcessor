using MediatR;
using Microsoft.Extensions.Logging;
using PagueVeloz.Core.Application.Commands.Bulk;
using PagueVeloz.Core.Application.Commands.Transactions;
using PagueVeloz.Core.Application.DTOs.Transaction;
using System.Collections.Concurrent;

namespace PagueVeloz.Core.Application.Handlers.Bulk
{
    public class ProcessTransactionsBulkHandler : IRequestHandler<ProcessTransactionsBulkCommand, List<TransactionResponse>>
    {
        private readonly IMediator _mediator;
        private readonly ILogger _logger;

        public ProcessTransactionsBulkHandler(IMediator mediator, ILogger<ProcessTransactionsBulkHandler> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<List<TransactionResponse>> Handle(ProcessTransactionsBulkCommand request, CancellationToken cancellationToken)
        {
            var responses = new ConcurrentBag<TransactionResponse>();
            _logger.LogInformation("Executing transactions");

            var groupedRequests = request.Requests.GroupBy(r => r.AccountId);

            var tasks = groupedRequests.Select(async group =>
            {
                foreach (var transactionRequest in group)
                {
                    try
                    {
                        IRequest<TransactionResponse> command = transactionRequest.Operation.ToLower() switch
                        {
                            "credit" => new CreditCommand
                            {
                                AccountId = transactionRequest.AccountId,
                                Amount = transactionRequest.Amount,
                                ReferenceId = transactionRequest.ReferenceId,
                                Description = transactionRequest.Description
                            },
                            "debit" => new DebitOrReserveCommand
                            {
                                AccountId = transactionRequest.AccountId,
                                Amount = transactionRequest.Amount,
                                ReferenceId = transactionRequest.ReferenceId,
                                Description = transactionRequest.Description
                            },
                            "reserve" => new DebitOrReserveCommand
                            {
                                AccountId = transactionRequest.AccountId,
                                Amount = transactionRequest.Amount,
                                ReferenceId = transactionRequest.ReferenceId,
                                Description = transactionRequest.Description,
                                IsReservation = true
                            },

                            _ => throw new InvalidOperationException($"Operação inválida: {transactionRequest.Operation}")
                        };

                        var result = await _mediator.Send(command, cancellationToken);
                        responses.Add(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while attempting to execute operations: {request}", request.Requests);
                        responses.Add(new TransactionResponse { Status = "failed", ErrorMessage = ex.Message });
                    }
                }
            });

            await Task.WhenAll(tasks);

            return responses
                .OrderBy(transaction => transaction.TransactionId)
                .ToList();
        }
    }
}