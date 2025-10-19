using MediatR;
using PagueVeloz.Core.Application.DTOs.Transaction;

namespace PagueVeloz.Core.Application.Commands.Transactions
{
    public class TransferCommand : IRequest<TransactionResponse>
    {
        public int SourceAccountId { get; set; }
        public int TargetAccountId { get; set; }
        public long Amount { get; set; }
        public string ReferenceId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Currency { get; set; } = "BRL";
    }
}
