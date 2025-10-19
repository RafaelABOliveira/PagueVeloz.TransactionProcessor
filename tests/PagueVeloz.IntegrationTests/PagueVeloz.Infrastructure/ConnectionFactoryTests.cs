using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PagueVeloz.Infrastructure.Persistence;
using System.Data.SqlClient;

namespace PagueVeloz.IntegrationTests.PagueVeloz.Infrastructure
{
    [Trait("Repositories", "Test Connection")]
    public class ConnectionFactoryTests
    {
        [Fact]
        public void CreateConnection_ShouldReturnSqlConnection_WithValidConnectionString()
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string> {
                {"ConnectionStrings:PagueVelozDatabase", "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var factory = new ConnectionFactory(configuration);

            // Act
            var connection = factory.CreateConnection();

            // Assert
            connection.Should().NotBeNull();
            connection.ConnectionString.Should().Be("Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;");
        }
    }
}
