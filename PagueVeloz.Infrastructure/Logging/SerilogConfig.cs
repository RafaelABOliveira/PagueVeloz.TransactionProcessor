using Serilog;

namespace PagueVeloz.Infrastructure.Logging
{
    public static class SerilogConfig
    {
        public static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithThreadId()
                .WriteTo.Console()
                .WriteTo.File("logs/pagueveloz-.log", rollingInterval: RollingInterval.Day)
                .MinimumLevel.Information()
                .CreateLogger();
        }
    }
}
