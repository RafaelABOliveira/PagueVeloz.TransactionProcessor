using PagueVeloz.Core;
using PagueVeloz.Infrastructure;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddCore(context.Configuration);
        services.AddInfrastructure(context.Configuration);
    })
    .Build();

await host.RunAsync();