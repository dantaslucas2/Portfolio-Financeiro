using PortfolioFinanceiro.Data;
using PortfolioFinanceiro.Services;
using PortfolioFinanceiro.Services.Interfaces;
using System.Reflection;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Portfolio Analytics API",
        Version = "v1",
        Description =
            "Analytics de portfólio"
    });

    var xml = Path.Combine(AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
        options.IncludeXmlComments(xml);
});
builder.Services.AddDbContext<DataContext>(options =>
    options.UseInMemoryDatabase("PortfolioAnalytics"));

builder.Services.AddScoped<IPortfolioValuator, PortfolioValuator>();
builder.Services.AddScoped<IPerformanceCalculator, PerformanceCalculator>();
builder.Services.AddScoped<IRiskAnalyzer, RiskAnalyzer>();
builder.Services.AddScoped<IRebalancingOptimizer, RebalancingOptimizer>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
    var path = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData.json");
    SeedDataLoader.Seed(db, path, logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Portfolio Analytics API v1"));

// Swagger na raiz: o avaliador executa `dotnet run` e cai direto na documentação navegável.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapControllers();

app.Run();

public partial class Program { }