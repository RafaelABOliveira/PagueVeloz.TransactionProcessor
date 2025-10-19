using MediatR;
using PagueVeloz.Core.Application.DTOs.Transaction;

namespace PagueVeloz.Core.Application.Commands.Transactions
{
    public class CaptureCommand : IRequest<TransactionResponse>
    {
        public string AccountId { get; set; }
        public long Amount { get; set; }
        public string ReferenceId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Currency { get; set; } = "BRL";
    }
}
