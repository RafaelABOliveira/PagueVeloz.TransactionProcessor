namespace PagueVeloz.Core.Application.DTOs.Transaction
{
    public class TransactionResponse
    {
        public string TransactionId { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // success, failed, pending
        public decimal Balance { get; set; }
        public decimal ReservedBalance { get; set; }
        public decimal AvailableBalance { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
    }
}
