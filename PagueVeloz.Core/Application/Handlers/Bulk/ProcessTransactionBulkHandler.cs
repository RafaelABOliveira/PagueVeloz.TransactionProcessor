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
            int maxDegreeOfParallelism = 30;

            _logger.LogInformation("Executing transactions");

            await Parallel.ForEachAsync(request.Requests, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                async (transactionRequest, token) => 
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
                            "debit" => new DebitCommand
                            {
                                AccountId = transactionRequest.AccountId, 
                                Amount = transactionRequest.Amount,       
                                ReferenceId = transactionRequest.ReferenceId,
                                Description = transactionRequest.Description
                            },
                            "transfer" => new TransferCommand
                            {
                                SourceAccountId = transactionRequest.SourceAccountId,
                                TargetAccountId = transactionRequest.TargetAccountId,
                                Amount = transactionRequest.Amount,
                                ReferenceId = transactionRequest.ReferenceId,
                                Description = transactionRequest.Description
                            },
                            _ => throw new InvalidOperationException($"Operação inválida: {transactionRequest.Operation}")
                        };

                        var result = await _mediator.Send(command);
                        responses.Add(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while attepting to execute operations: {request}", request.Requests);
                        responses.Add(new TransactionResponse { Status = "failed", ErrorMessage = ex.Message });
                    }
                });

            return responses
              .OrderBy(transaction => int.Parse(transaction.TransactionId.Split('-')[1]))
              .ToList();
        }
    }
}
