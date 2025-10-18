using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace PagueVeloz.Infrastructure.Persistence.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly ILogger _logger;

        public TransactionRepository(ConnectionFactory connectionFactory, ILogger<TransactionRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task AddAsync(Transaction transaction)
        {
            try
            {
                _logger.LogInformation("Opening database connection to add new transaction for AccountId {AccountId}", transaction.AccountId);

                using var connection = (SqlConnection)_connectionFactory.CreateConnection();
                await connection.OpenAsync();
                _logger.LogInformation("Database connection opened. Inserting transaction.");

                var sql = @"
                    INSERT INTO [Transaction] (
                        TransactionId,
                        AccountId, 
                        TypeId, 
                        Amount, 
                        Description
                    )
                    VALUES (
                        @TransactionId,
                        @AccountId, 
                        @TypeId, 
                        @Amount, 
                        @Description
                    );
                ";

                using var command = new SqlCommand(sql, connection);

                command.Parameters.AddWithValue("@TransactionId", transaction.TransactionId);
                command.Parameters.AddWithValue("@AccountId", transaction.AccountId);
                command.Parameters.AddWithValue("@TypeId", (int)transaction.Type);
                command.Parameters.AddWithValue("@Amount", transaction.Amount);
                command.Parameters.AddWithValue("@Description", transaction.Description);

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Transaction successfully added. AccountId: {AccountId}, Amount: {Amount}", transaction.AccountId, transaction.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new transaction for AccountId {AccountId}", transaction.AccountId);
                throw new InvalidOperationException("Error adding new transaction", ex);
            }
        }

        public async Task<int> GenerateNextTransactionIdAsync()
        {
            try
            {
                using var connection = (SqlConnection)_connectionFactory.CreateConnection();
                await connection.OpenAsync();

                // Busca o maior Id atual da tabela Transaction
                var sql = @"SELECT ISNULL(MAX(Id), 0) + 1 FROM [Transaction];";
                using var command = new SqlCommand(sql, connection);
                var nextId = (int)await command.ExecuteScalarAsync();

                return nextId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating next Transaction Id, trying random fallback");

                using var connection = (SqlConnection)_connectionFactory.CreateConnection();
                await connection.OpenAsync();

                var sql = @"SELECT Id FROM [Transaction];";
                using var command = new SqlCommand(sql, connection);
                var existingIds = new HashSet<int>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingIds.Add(reader.GetInt32(0));
                    }
                }

                var random = new Random();
                int randomId;
                do
                {
                    randomId = random.Next(1, int.MaxValue);
                } while (existingIds.Contains(randomId));

                return randomId;
            }
        }
    }
}