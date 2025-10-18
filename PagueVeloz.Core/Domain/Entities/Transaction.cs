using PagueVeloz.Core.Domain.Enums;

namespace PagueVeloz.Core.Domain.Entities
{
    public class Transaction
    {
        public TransactionType Type { get; set; }
        public long Amount { get; set; }

        public int Id { get; set; }
        public string TransactionId { get; set; }
        public Account Account { get; set; }
        public string AccountId { get; set; }
        public string Description { get; set; }
    }
}
