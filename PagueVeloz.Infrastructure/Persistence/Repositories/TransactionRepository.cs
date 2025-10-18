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

        public async Task<string> AddAsyncTransactionRegistry(Transaction transaction)
        {
            try
            {
                _logger.LogInformation("Opening database connection to add new transaction for AccountId {AccountId}", transaction.AccountId);

                using var connection = (SqlConnection)_connectionFactory.CreateConnection();
                await connection.OpenAsync();
                _logger.LogInformation("Database connection opened. Inserting transaction.");

                // Insere a transação sem TransactionId e retorna o Id gerado
                var insertSql = @"
                    INSERT INTO [Transaction] (
                        AccountId, 
                        TypeId, 
                        Amount, 
                        Description,
                        TransactionId
                    )
                    VALUES (
                        @AccountId, 
                        @TypeId, 
                        @Amount, 
                        @Description,
                        ''
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS int);
                ";

                using var insertCommand = new SqlCommand(insertSql, connection);
                insertCommand.Parameters.AddWithValue("@AccountId", transaction.AccountId);
                insertCommand.Parameters.AddWithValue("@TypeId", (int)transaction.Type);
                insertCommand.Parameters.AddWithValue("@Amount", transaction.Amount);
                insertCommand.Parameters.AddWithValue("@Description", transaction.Description ?? string.Empty);

                var insertedId = (int)await insertCommand.ExecuteScalarAsync();

                var selectSql = "SELECT TransactionId FROM [Transaction] WHERE Id = @Id";
                using var selectCommand = new SqlCommand(selectSql, connection);
                selectCommand.Parameters.AddWithValue("@Id", insertedId);
                var transactionId = (string)await selectCommand.ExecuteScalarAsync();

                _logger.LogInformation("Transaction successfully added. AccountId: {AccountId}, Amount: {Amount}, TransactionId: {TransactionId}", transaction.AccountId, transaction.Amount, transactionId);

                return transactionId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new transaction for AccountId {AccountId}", transaction.AccountId);
                throw new InvalidOperationException($"Error adding new transaction: {ex.Message}", ex);
            }
        }
    }
}