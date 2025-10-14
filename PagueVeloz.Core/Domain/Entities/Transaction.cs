using PagueVeloz.Core.Domain.Enums;

namespace PagueVeloz.Core.Domain.Entities
{
    public class Transaction
    {
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }

        public Guid AccountId { get; set; }
        public Account Account { get; set; }
    }
}
