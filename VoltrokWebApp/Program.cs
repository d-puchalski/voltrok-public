using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Serilog;
using System.Text;

using VoltrokEF;
using VoltrokServices.Services;
using VoltrokServices.Services.Auth;
using VoltrokServices.Services.Badges;
using VoltrokServices.Services.Chat;
using VoltrokServices.Services.Countries;
using VoltrokServices.Services.Military;
using VoltrokServices.Services.Notifications;
using VoltrokServices.Services.Players;
using VoltrokServices.Services.Premium;
using VoltrokServices.Services.Seasons;
using VoltrokServices.Services.Translations;
using VoltrokWebApp.Pages;

namespace VoltrokWebApp;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext());

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddDataProtection();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

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


            // Register Services
            builder.Services.AddScoped<PlayersService>();
            builder.Services.AddScoped<PlayerTradeService>();
            builder.Services.AddScoped<PlayerMilitaryTransportService>();
            builder.Services.AddScoped<NotificationService>();
            builder.Services.AddScoped<ChatService>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<CountriesService>();
            builder.Services.AddScoped<CountryGovernanceService>();
            builder.Services.AddScoped<AppState>();
            builder.Services.AddScoped<TranslationService>();
            builder.Services.AddScoped<PlayerBadgeService>();
            builder.Services.AddScoped<NpcBootstrapService>();
            builder.Services.AddScoped<SeasonService>();
            builder.Services.AddScoped<PremiumService>();

            builder.Services.AddMemoryCache();

            var app = builder.Build();

            app.UseSerilogRequestLogging();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(context =>
                    {
                        var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                        if (exceptionFeature?.Error != null)
                        {
                            Log.Error(
                                exceptionFeature.Error,
                                "Unhandled web exception for {Method} {Path}.",
                                context.Request.Method,
                                exceptionFeature.Path ?? context.Request.Path.Value);
                        }

                        context.Response.Redirect("/Error");
                        return Task.CompletedTask;
                    });
                });
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    if (!context.Context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    context.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=86400";
                }
            });
            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();
            app.UseAuthentication();

            app.UseAntiforgery();

            app.MapPost("/api/stripe/webhook", async (HttpContext httpContext, PremiumService premiumService) =>
            {
                using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
                var payload = await reader.ReadToEndAsync();
                var signature = httpContext.Request.Headers["Stripe-Signature"].ToString();

                await premiumService.HandleWebhookAsync(payload, signature);
                return Results.Ok();
            });

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Web server terminated unexpectedly.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
