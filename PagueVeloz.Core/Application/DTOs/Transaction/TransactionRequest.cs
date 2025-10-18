namespace PagueVeloz.Core.Application.DTOs.Transaction
{
    public class TransactionRequest
    {
        public string AccountId { get; set; }
        public long Amount { get; set; }
        public string Operation { get; set; } // credit, debit, reserve, capture, reversal, transfer
        public string ReferenceId { get; set; }
        public int TargetAccountId { get; set; } = 0;
        public int SourceAccountId { get; set; } = 0;
        public string Currency { get; set; } = "BRL";
        public string Description { get; set; } = string.Empty;
    }
}
