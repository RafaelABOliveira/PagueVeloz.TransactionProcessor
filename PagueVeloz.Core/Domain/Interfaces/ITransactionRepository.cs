using PagueVeloz.Core.Domain.Entities;

namespace PagueVeloz.Core.Domain.Interfaces
{
    public interface ITransactionRepository
    {
        Task<string> AddAsyncTransactionRegistry(Transaction transaction);
    }
}
