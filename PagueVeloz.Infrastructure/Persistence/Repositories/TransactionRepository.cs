using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Enums;
using PagueVeloz.Core.Domain.Interfaces;
using PagueVeloz.Infrastructure.Resilience;
using Polly.Retry;

namespace PagueVeloz.Infrastructure.Persistence.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly ILogger _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        public TransactionRepository(ConnectionFactory connectionFactory, ILogger<TransactionRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
            _retryPolicy = PollyPolicies.SqlRetryPolicy(_logger);
        }

        public async Task<string> AddAsyncTransactionRegistry(Transaction transaction)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    _logger.LogInformation("Opening database connection to add new transaction for AccountId {AccountId}", transaction.AccountId);

                    using var connection = (SqlConnection)_connectionFactory.CreateConnection();
                    await connection.OpenAsync();
                    _logger.LogInformation("Database connection opened. Inserting transaction.");

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

                    var insertedId = (int?)await insertCommand.ExecuteScalarAsync();

                    var selectSql = "SELECT TransactionId FROM [Transaction] WHERE Id = @Id";
                    using var selectCommand = new SqlCommand(selectSql, connection);
                    selectCommand.Parameters.AddWithValue("@Id", insertedId);
                    var transactionId = (string?)await selectCommand.ExecuteScalarAsync();

                    if (transactionId == null)
                        throw new InvalidOperationException("TransactionId not found after insert but included in DB by trigger.");

                    _logger.LogInformation("Transaction successfully added. AccountId: {AccountId}, Amount: {Amount}, TransactionId: {TransactionId}", transaction.AccountId, transaction.Amount, transactionId); ;
                    return transactionId;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new transaction for AccountId {AccountId}", transaction.AccountId);
                throw new InvalidOperationException("Could not add transaction registry", ex);
            }
        }

        public async Task<Transaction?> GetByReferenceIdAsync(string referenceId, CancellationToken cancellationToken)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    _logger.LogInformation("Searching transaction by ReferenceId {ReferenceId}", referenceId);

                    using var connection = (SqlConnection)_connectionFactory.CreateConnection();
                    await connection.OpenAsync(cancellationToken);

                    var sql = @"
                        SELECT 
                            Id,
                            TransactionId,
                            AccountId,
                            TypeId,
                            Amount,
                            Description,
                            ReferenceId
                        FROM [Transaction]
                        WHERE ReferenceId = @ReferenceId
                    ";

                    using var command = new SqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@ReferenceId", referenceId);

                    using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        return new Transaction
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            TransactionId = reader.GetString(reader.GetOrdinal("TransactionId")),
                            AccountId = reader.GetString(reader.GetOrdinal("AccountId")),
                            Type = (TransactionType)reader.GetByte(reader.GetOrdinal("TypeId")),
                            Amount = reader.GetInt64(reader.GetOrdinal("Amount")),
                            Description = reader.GetString(reader.GetOrdinal("Description")),
                            ReferenceId = reader.GetString(reader.GetOrdinal("ReferenceId"))
                        };
                    }

                    return null;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching transaction by ReferenceId {ReferenceId}", referenceId);
                throw new InvalidOperationException("Could not find Transaction by ReferenceId", ex);
            }
        }
    }
}