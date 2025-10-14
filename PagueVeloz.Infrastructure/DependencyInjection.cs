using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PagueVeloz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        //services.AddScoped<IAccountRepository, AccountRepository>();

        return services;
    }
}