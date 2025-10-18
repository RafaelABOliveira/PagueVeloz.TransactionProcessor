using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Core.Domain.Interfaces;
using PagueVeloz.Infrastructure.Persistence.Repositories;
using PagueVeloz.Infrastructure.Repositories;

namespace PagueVeloz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        return services;
    }
}