using PagueVeloz.Core.Domain.Enums;

namespace PagueVeloz.Core.Domain.Entities
{
    public class Account
    {
        public decimal AvailableBalance { get; set; }
        public decimal ReservedBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public AccountStatus Status { get; set; }
        public Guid ClientId { get; set; }
        public Client Client { get; set; }

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
