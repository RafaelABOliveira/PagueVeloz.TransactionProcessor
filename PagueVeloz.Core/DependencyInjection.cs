using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PagueVeloz.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}