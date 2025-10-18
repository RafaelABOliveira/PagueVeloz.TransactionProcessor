using MediatR;
using PagueVeloz.Core.Application.DTOs.Transaction;

namespace PagueVeloz.Core.Application.Commands.Bulk
{
    public class ProcessTransactionsBulkCommand : IRequest<List<TransactionResponse>>
    {
        public List<TransactionRequest> Requests { get; set; }
    }
}
