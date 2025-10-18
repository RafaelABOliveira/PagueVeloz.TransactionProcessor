namespace PagueVeloz.Core.Application.DTOs.Transaction
{
    public class TransactionResponse
    {
        public string TransactionId { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // success, failed, pending
        public long Balance { get; set; }
        public long ReservedBalance { get; set; }
        public long AvailableBalance { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
    }
}
