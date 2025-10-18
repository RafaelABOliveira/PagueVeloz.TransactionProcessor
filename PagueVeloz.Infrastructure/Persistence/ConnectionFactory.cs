using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace PagueVeloz.Infrastructure.Persistence
{
    public class ConnectionFactory
    {
        private readonly string _connectionString;

        public ConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("PagueVelozDatabase");
        }

        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
