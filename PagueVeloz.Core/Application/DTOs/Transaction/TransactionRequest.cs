namespace PagueVeloz.Core.Application.DTOs.Transaction
{
    public class TransactionRequest
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string Operation { get; set; } // credit, debit, reserve, capture, reversal, transfer
        public string ReferenceId { get; set; } 
        public int? TargetAccountId { get; set; } 
        public string Currency { get; set; } = "BRL";
        public string Description { get; set; } = string.Empty;
    }
}
