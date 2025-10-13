using Microsoft.Extensions.DependencyInjection;

namespace PagueVeloz.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}