namespace PagueVeloz.Core.Application.DTOs.Transaction
{
    public class TransactionRequest
    {
        public string AccountId { get; set; }
        public long Amount { get; set; }
        public string Operation { get; set; } 
        public string ReferenceId { get; set; }
        public string TargetAccountId { get; set; }
        public string SourceAccountId { get; set; }
        public string Currency { get; set; } = "BRL";
        public string Description { get; set; } = string.Empty;
    }
}
