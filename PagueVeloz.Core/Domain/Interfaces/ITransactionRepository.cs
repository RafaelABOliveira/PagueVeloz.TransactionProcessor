using PagueVeloz.Core.Domain.Entities;

namespace PagueVeloz.Core.Domain.Interfaces
{
    public interface ITransactionRepository
    {
        Task AddAsync(Transaction transaction);
        Task<int> GenerateNextTransactionIdAsync();
    }
}
