using PagueVeloz.Core.Domain.Enums;

namespace PagueVeloz.Core.Domain.Entities
{
    public class Account
    {
        public long AvailableBalance { get; set; }
        public long ReservedBalance { get; set; }
        public long CreditLimit { get; set; }
        public AccountStatus Status { get; set; }
        public string AccountId { get; set; }
        public int ClientId { get; set; }
        public Client Client { get; set; }

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
