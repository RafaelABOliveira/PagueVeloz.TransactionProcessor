using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PagueVeloz.Core.Domain.Entities;
using PagueVeloz.Core.Domain.Enums;
using PagueVeloz.Infrastructure.Persistence;
using PagueVeloz.Infrastructure.Persistence.Repositories;
using Xunit.Sdk;

namespace PagueVeloz.Infrastructure.Tests.Persistence
{
    [Trait("Repositories", "TransactionRepository")]
    public class TransactionRepositoryTests
    {
        private readonly string _connectionString =
            "Server=localhost;Database=PagueVeloz;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

        private readonly string _connectionStringFail =
            "Server=localhost;Database=Pagueloz;Integrated Security=False;Encrypt=True;TrustServerCertificate=True;";

        private readonly ConnectionFactory _factory;
        private readonly Mock<ILogger<TransactionRepository>> _loggerMock;
        private readonly TransactionRepository _repository;

        public TransactionRepositoryTests()
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                {"ConnectionStrings:PagueVelozDatabase", _connectionString}
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _factory = new ConnectionFactory(configuration);
            _loggerMock = new Mock<ILogger<TransactionRepository>>();
            _repository = new TransactionRepository(_factory, _loggerMock.Object);
        }

        [Fact]
        public async Task AddAsyncTransactionRegistry_ShouldInsertAndReturnTransactionId()
        {
            // Arrange
            var transaction = new Transaction
            {
                AccountId = "ACC-001",
                Type = TransactionType.Credit,
                Amount = 1500,
                Description = "Test credit"
            };

            // Act
            var transactionId = await _repository.AddAsyncTransactionRegistry(transaction);

            // Assert
            transactionId.Should().NotBeNullOrEmpty();
            transactionId.Should().StartWith("TXN-");
            transactionId.Should().EndWith("-PROCESSED");
        }

        [Fact]
        public async Task GetByReferenceIdAsync_ShouldReturnNull_WhenNotExists()
        {
            // Act
            var result = await _repository.GetByReferenceIdAsync("NON-EXISTENT", CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByReferenceIdAsync_ShouldReturnTransaction_WhenExists()
        {
            // Arrange
            var referenceId = "TXN-1";
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO [Transaction] (AccountId, TypeId, Amount, Description, ReferenceId, TransactionId)
                VALUES ('ACC-001', 1, 2000, 'Reference Test', @ReferenceId, 'TXN-001-PROCESSED');
            ";
            insertCmd.Parameters.AddWithValue("@ReferenceId", referenceId); // Changed in DB by trigger
            await insertCmd.ExecuteNonQueryAsync();

            // Act
            var result = await _repository.GetByReferenceIdAsync(referenceId, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.AccountId.Should().Be("ACC-001");
            result.ReferenceId.Should().Be(referenceId);
            result.TransactionId.Should().Be("TXN-1-PROCESSED");
        }

        [Fact]
        public async Task AddAsyncTransactionRegistry_ShouldThrowInvalidOperationException_WhenSqlExceptionOccurs_RealConnection()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<TransactionRepository>>();
  
                var inMemorySettings = new Dictionary<string, string>
                {
                    {"ConnectionStrings:PagueVelozDatabase", _connectionStringFail}
                };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var failingFactory = new ConnectionFactory(configuration);
            var repository = new TransactionRepository(failingFactory, loggerMock.Object);

            var transaction = new Transaction
            {
                AccountId = "ACC-001",
                Type = TransactionType.Credit,
                Amount = 100,
                Description = "Test error"
            };

            // Act
            Func<Task> act = async () => await repository.AddAsyncTransactionRegistry(transaction);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Could not add transaction registry*");
        }

        [Fact]
        public async Task GetByReferenceIdAsync_ShouldThrowInvalidOperationException_WhenSqlExceptionOccurs_RealConnection()
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string>
            {
                {"ConnectionStrings:PagueVelozDatabase", _connectionStringFail}
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            var failingFactory = new ConnectionFactory(configuration);
            var loggerMock = new LoggerFactory().CreateLogger<TransactionRepository>();
            var repository = new TransactionRepository(failingFactory, loggerMock);

            // Act
            Func<Task> act = async () => await repository.GetByReferenceIdAsync("ANY-REF", CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Could not find Transaction by ReferenceId*");
        }
    }
}
