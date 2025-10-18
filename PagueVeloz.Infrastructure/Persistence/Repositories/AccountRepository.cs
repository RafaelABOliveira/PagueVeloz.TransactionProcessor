using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PagueVeloz.Core.Application.DTOs;
using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Enums;
using PagueVeloz.Core.Domain.Interfaces;
using PagueVeloz.Infrastructure.Persistence;

namespace PagueVeloz.Infrastructure.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly ILogger _logger;

        public AccountRepository(ConnectionFactory connectionFactory, ILogger<AccountRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<Response<Account>> GetByIdAsync(string accountId)
        {
            try
            {
                _logger.LogInformation("Opening database connection to fetch account {AccountId}", accountId);
                using var connection = (SqlConnection)_connectionFactory.CreateConnection();
                await connection.OpenAsync();
                _logger.LogInformation("Database connection opened");

                var sql = @"
                    SELECT a.AccountId, a.ClientId, a.AvailableBalance, a.ReservedBalance, a.CreditLimit, 
                           a.StatusId, c.Name AS ClientName
                    FROM Account a
                    INNER JOIN Client c ON a.ClientId = c.Id
                    WHERE a.AccountId = @Id";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Id", accountId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var account = new Account
                    {
                        AccountId = reader.GetString(reader.GetOrdinal("AccountId")),
                        ClientId = reader.GetInt32(reader.GetOrdinal("ClientId")),
                        AvailableBalance = reader.GetInt64(reader.GetOrdinal("AvailableBalance")),
                        ReservedBalance = reader.GetInt64(reader.GetOrdinal("ReservedBalance")),
                        CreditLimit = reader.GetInt64(reader.GetOrdinal("CreditLimit")),
                        Status = (AccountStatus)reader.GetByte(reader.GetOrdinal("StatusId")),
                        Client = new Client
                        {
                            Name = reader.GetString(reader.GetOrdinal("ClientName"))
                        }
                    };

                    account.Client.Accounts.Add(account);

                    _logger.LogInformation("Account {AccountId} found", accountId);
                    return Response<Account>.Ok(account);
                }

                _logger.LogInformation("Account {AccountId} not found", accountId);
                return Response<Account>.Fail("Account not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching account by ID {AccountId}", accountId);
                return Response<Account>.Fail(ex.Message);
            }
        }

        public async Task UpdateAsync(Account account)
        {
            try
            {
                _logger.LogInformation("Opening database connection to update account {AccountId}", account.AccountId);
                using var connection = (SqlConnection)_connectionFactory.CreateConnection();
                await connection.OpenAsync();
                _logger.LogInformation("Database connection opened for update");

                var sql = @"
                    UPDATE Account
                    SET AvailableBalance = @AvailableBalance,
                        ReservedBalance = @ReservedBalance,
                        CreditLimit = @CreditLimit,
                        StatusId = @StatusId
                    WHERE AccountId = @AccountId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@AccountId", account.AccountId);
                command.Parameters.AddWithValue("@AvailableBalance", account.AvailableBalance);
                command.Parameters.AddWithValue("@ReservedBalance", account.ReservedBalance);
                command.Parameters.AddWithValue("@CreditLimit", account.CreditLimit);
                command.Parameters.AddWithValue("@StatusId", (byte)account.Status); 

                _logger.LogInformation("Executing update for account {AccountId}", account.AccountId);
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Account {AccountId} successfully updated", account.AccountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account ID {AccountId}", account.AccountId);
                throw;
            }
        }
    }
}
