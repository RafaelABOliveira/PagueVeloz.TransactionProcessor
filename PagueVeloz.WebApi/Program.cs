using PagueVeloz.Core.Application.Handlers.Transactions;
using PagueVeloz.Infrastructure;
using PagueVeloz.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "PagueVeloz API", Version = "v1" });
});

builder.Services.AddSingleton<ConnectionFactory>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreditCommandHandler).Assembly);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PagueVeloz API v1");
        options.RoutePrefix = string.Empty;
    });

    var url = "https://localhost:5001";
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch { 
    
        //logar
    
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Urls.Add("https://localhost:5001");

app.Run();
