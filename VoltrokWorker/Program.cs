using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

using VoltrokEF;
using VoltrokServices.Services.Auth;
using VoltrokServices.Services.Badges;
using VoltrokServices.Services.Countries;
using VoltrokServices.Services.Military;
using VoltrokServices.Services.Notifications;
using VoltrokServices.Services.Players;
using VoltrokServices.Services.Premium;
using VoltrokServices.Services.Seasons;
using VoltrokWorker.BackgroundWorker;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddWindowsService();
    builder.Services.AddDataProtection();

    builder.Services.AddDbContextFactory<AppDbContext>(options =>
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure();
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
    });

    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);


    builder.Services.AddHostedService<ElectionWorkerService>();
    builder.Services.AddHostedService<BadgeRefreshWorkerService>();
    builder.Services.AddHostedService<PlayerDailyStatsWorkerService>();
    builder.Services.AddHostedService<PlayerResourceGrowthWorker>();
    builder.Services.AddHostedService<NpcWorldSimulationWorkerService>();
    builder.Services.AddHostedService<NpcBootstrapWorkerService>();
    builder.Services.AddHostedService<PlayerMilitaryProductionWorkerService>();
    builder.Services.AddHostedService<PlayerTransportSettlementWorker>();
    builder.Services.AddHostedService<MilitaryCombatSettlementWorker>();
    builder.Services.AddHostedService<SeasonRolloverWorkerService>();

    builder.Services.AddScoped<PlayersService>();
    builder.Services.AddScoped<PlayerTradeService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<PlayerBadgeService>();
    builder.Services.AddScoped<PlayerMilitaryTransportService>();
    builder.Services.AddScoped<NotificationService>();
    builder.Services.AddScoped<CountryGovernanceService>();
    builder.Services.AddScoped<NpcBootstrapService>();
    builder.Services.AddScoped<SeasonService>();
    builder.Services.AddScoped<PremiumService>();

    builder.Services.AddMemoryCache();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
