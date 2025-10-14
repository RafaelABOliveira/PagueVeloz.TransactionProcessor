namespace PagueVeloz.Core.Domain.Entities
{
    public class Client
    {
        public string Name { get; set; }
        public ICollection<Account> Accounts { get; set; } = new List<Account>();
    }
}
