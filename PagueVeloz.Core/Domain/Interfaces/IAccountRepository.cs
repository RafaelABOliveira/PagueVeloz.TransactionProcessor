using PagueVeloz.Core.Application.DTOs;
using PagueVeloz.Core.Domain.Entities;

namespace PagueVeloz.Core.Domain.Interfaces
{
    public interface IAccountRepository
    {
        Task<Response<Account>> GetByIdAsync(string accountId);
        Task UpdateAsync(Account account);
    }
}
