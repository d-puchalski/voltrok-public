using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using Stripe;
using CheckoutSession = Stripe.Checkout.Session;
using CheckoutSessionCreateOptions = Stripe.Checkout.SessionCreateOptions;
using CheckoutSessionLineItemOptions = Stripe.Checkout.SessionLineItemOptions;
using CheckoutSessionService = Stripe.Checkout.SessionService;

using VoltrokEF;
using VoltrokServices.Services.Seasons;
using VoltrokUtils.Models;

namespace VoltrokServices.Services.Premium;

public sealed class PremiumService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IConfiguration configuration,
    SeasonService seasonService)
{
    private const decimal BattlePassSpeedMultiplier = 5m;
    private const decimal CurrentSeasonFullPriceUsd = 4.99m;
    private const string BattlePassTrialCode = "battlepass_trial_24h";
    private const string BattlePassSeasonCode = "battlepass_season";
    private const string BattlePassMonthCode = "battlepass_month";
    private const string BattlePassHalfYearCode = "battlepass_6_months";
    private const string BattlePassYearCode = "battlepass_year";
    private const string ShieldSeasonCode = "shield_season";
    private const string ShieldMonthCode = "shield_month";
    private const string ShieldHalfYearCode = "shield_6_months";
    private const string ShieldYearCode = "shield_year";

    public async Task<PremiumStatusView> GetStatusAsync(Guid playerId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayersId == playerId);

        if (player == null)
        {
            return new PremiumStatusView(false, null, false, 1m, 1m, 1m, 1m, false, false, false, null, false, true);
        }

        var nowUtc = DateTime.UtcNow;
        var battlePassEndsAt = GetBattlePassEndsAt(player);
        var shieldEndsAt = player.ShieldValidTill;
        var battlePassActive = battlePassEndsAt.HasValue && battlePassEndsAt > nowUtc;
        var shieldActive = shieldEndsAt.HasValue && shieldEndsAt > nowUtc;

        return new PremiumStatusView(
            battlePassActive,
            battlePassEndsAt,
            battlePassActive || shieldActive,
            battlePassActive ? BattlePassSpeedMultiplier : 1m,
            battlePassActive ? BattlePassSpeedMultiplier : 1m,
            battlePassActive ? BattlePassSpeedMultiplier : 1m,
            1m,
            shieldActive,
            battlePassActive,
            battlePassActive,
            shieldEndsAt,
            shieldActive,
            player.BattlePassTrialGrantedAt is null);
    }

    public async Task<string> CreateCheckoutSessionUrlAsync(Guid playerId, string planCode, string successUrl, string cancelUrl)
    {
        ConfigureStripe();

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var user = await GetUserByPlayerIdAsync(db, playerId);
        var plan = GetPlanOrThrow(planCode);

        if (plan.IsFreePlan)
        {
            throw new InvalidOperationException("Free trial plans do not use checkout.");
        }

        var customerId = await GetOrCreateStripeCustomerIdAsync(db, user);
        var sessionService = new CheckoutSessionService();

        var currentSeason = await seasonService.GetCurrentSeasonAsync();
        var lineItem = BuildCheckoutLineItem(plan, currentSeason.EndsAtUtc);
        var session = await sessionService.CreateAsync(new CheckoutSessionCreateOptions
        {
            Customer = customerId,
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems = [lineItem],
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.UsersId.ToString(),
                ["playerId"] = playerId.ToString(),
                ["planCode"] = plan.Code
            }
        });

        return session.Url ?? throw new InvalidOperationException("Stripe checkout session did not return a URL.");
    }

    public async Task ConfirmCheckoutSessionAsync(Guid playerId, string sessionId)
    {
        ConfigureStripe();

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var user = await GetUserByPlayerIdAsync(db, playerId);
        var sessionService = new CheckoutSessionService();
        var session = await sessionService.GetAsync(sessionId);

        if (!string.Equals(session.CustomerId, user.StripeCustomerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Stripe checkout session does not belong to this account.");
        }

        if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(session.Status, "complete", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Stripe checkout session has not been paid yet.");
        }

        if (await db.PremiumPaymentEvents.AnyAsync(e => e.StripeObjectId == sessionId && e.Status == "processed"))
        {
            return;
        }

        var planCode = GetMetadata(session.Metadata ?? new Dictionary<string, string>(), "planCode")
            ?? throw new InvalidOperationException("Stripe checkout session is missing plan metadata.");
        var payloadJson = JsonSerializer.Serialize(new
        {
            session.Id,
            session.CustomerId,
            session.PaymentStatus,
            session.Status,
            session.Mode,
            planCode,
            session.Metadata
        });

        await ApplyPlanAsync(db, user, planCode, DateTime.UtcNow);

        db.PremiumPaymentEvents.Add(new PremiumPaymentEvent
        {
            PremiumPaymentEventsId = Guid.NewGuid(),
            UsersId = user.UsersId,
            StripeEventId = $"manual-confirm:{sessionId}",
            StripeObjectId = sessionId,
            EventType = "checkout.session.completed.manual",
            Status = "processed",
            PayloadJson = payloadJson,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task ActivateFreePlanAsync(Guid playerId, string planCode)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var user = await GetUserByPlayerIdAsync(db, playerId);
        var player = user.Player ?? throw new InvalidOperationException("Player not found.");
        var plan = GetPlanOrThrow(planCode);

        if (!plan.IsFreePlan)
        {
            throw new InvalidOperationException("Paid plans are purchased through checkout.");
        }

        if (player.BattlePassTrialGrantedAt.HasValue)
        {
            throw new InvalidOperationException("The free Battle Pass trial was already claimed for this account.");
        }

        await ApplyPlanAsync(db, user, planCode, DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    public async Task HandleWebhookAsync(string payloadJson, string stripeSignature)
    {
        ConfigureStripe();
        var webhookSecret = configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            throw new InvalidOperationException("Stripe webhook secret is not configured.");
        }

        var stripeEvent = EventUtility.ConstructEvent(payloadJson, stripeSignature, webhookSecret);

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var paymentEvent = await db.PremiumPaymentEvents.FirstOrDefaultAsync(e => e.StripeEventId == stripeEvent.Id);

        if (paymentEvent is { Status: "processed" })
        {
            return;
        }

        paymentEvent ??= new PremiumPaymentEvent
        {
            PremiumPaymentEventsId = Guid.NewGuid(),
            StripeEventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            Status = "received",
            PayloadJson = payloadJson,
            CreatedAt = DateTime.UtcNow
        };

        if (db.Entry(paymentEvent).State == EntityState.Detached)
        {
            db.PremiumPaymentEvents.Add(paymentEvent);
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    if (stripeEvent.Data.Object is CheckoutSession checkoutSession)
                    {
                        paymentEvent.UsersId = await HandleCheckoutSessionCompletedAsync(db, checkoutSession, stripeEvent.Id);
                        paymentEvent.StripeObjectId = checkoutSession.Id;
                    }
                    break;
            }

            paymentEvent.Status = "processed";
            paymentEvent.ErrorMessage = null;
            paymentEvent.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to process Stripe webhook event {StripeEventId} of type {StripeEventType}.", stripeEvent.Id, stripeEvent.Type);
            paymentEvent.Status = "failed";
            paymentEvent.ErrorMessage = ex.Message;
            paymentEvent.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            throw;
        }
    }

    public async Task<bool> IsPremiumActiveAsync(Guid playerId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayersId == playerId);

        return GetBattlePassEndsAt(player) > DateTime.UtcNow;
    }

    public async Task<bool> IsShieldActiveAsync(Guid playerId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayersId == playerId);

        return player != null
            && player.ShieldValidTill.HasValue
            && player.ShieldValidTill.Value > DateTime.UtcNow;
    }

    public async Task<decimal> GetTransportSpeedMultiplierAsync(Guid playerId)
        => await IsPremiumActiveAsync(playerId) ? BattlePassSpeedMultiplier : 1m;

    public async Task<decimal> GetMilitarySpeedMultiplierAsync(Guid playerId)
        => await IsPremiumActiveAsync(playerId) ? BattlePassSpeedMultiplier : 1m;

    public async Task<decimal> GetUnitProductionSpeedMultiplierAsync(Guid playerId)
        => await IsPremiumActiveAsync(playerId) ? BattlePassSpeedMultiplier : 1m;

    public async Task<bool> CanUsePremiumShieldAsync(Guid playerId)
        => await IsShieldActiveAsync(playerId);

    public async Task CancelShieldAsync(Guid playerId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var player = await db.Players.FirstOrDefaultAsync(p => p.PlayersId == playerId);
        if (player == null)
        {
            throw new InvalidOperationException("Player not found.");
        }

        if (!player.ShieldValidTill.HasValue)
        {
            throw new InvalidOperationException("Shield package is not active.");
        }

        player.ShieldValidTill = null;
        await db.SaveChangesAsync();
    }

    public async Task<ConfiguredPlan[]> GetPlansFromConfigurationAsync()
    {
        var currentSeason = await seasonService.GetCurrentSeasonAsync();
        var seasonPriceLabel = FormatPriceLabel(GetSeasonPriceAmount(currentSeason.EndsAtUtc), "USD");
        ConfiguredPlan[] plans =
        [
            BuildPlan(BattlePassTrialCode, "Battle Pass Trial", PremiumPackageKind.BattlePass, PremiumDurationKind.Trial24Hours, 24, true, null, "24h free trial", "Access to x5 speed boosts for 24 hours."),
            BuildPlan(BattlePassSeasonCode, "Battle Pass - Current Season", PremiumPackageKind.BattlePass, PremiumDurationKind.Season, SeasonService.SeasonDurationDays * 24, false, null, "Until season ends", "Battle Pass for the current 2-week season.", seasonPriceLabel, true),
            BuildPlan(BattlePassMonthCode, "Battle Pass - 1 Month", PremiumPackageKind.BattlePass, PremiumDurationKind.Month, 24 * 30, false, null, "30 days", "Battle Pass with x5 speed boosts for 1 month.", "7.99 $", true),
            BuildPlan(BattlePassHalfYearCode, "Battle Pass - 6 Months", PremiumPackageKind.BattlePass, PremiumDurationKind.HalfYear, 24 * 30 * 6, false, null, "6 months", "Battle Pass with x5 speed boosts for 6 months.", "29.99 $", true),
            BuildPlan(BattlePassYearCode, "Battle Pass - 1 Year", PremiumPackageKind.BattlePass, PremiumDurationKind.Year, 24 * 365, false, null, "1 year", "Battle Pass with x5 speed boosts for 1 year.", "49.99 $", true),
            BuildPlan(ShieldSeasonCode, "Shield Pass - Current Season", PremiumPackageKind.Shield, PremiumDurationKind.Season, SeasonService.SeasonDurationDays * 24, false, null, "Until season ends", "Shield Pass for the current 2-week season.", seasonPriceLabel, true),
            BuildPlan(ShieldMonthCode, "Shield Pass - 1 Month", PremiumPackageKind.Shield, PremiumDurationKind.Month, 24 * 30, false, null, "30 days", "Shield Pass for 1 month.", "7.99 $", true),
            BuildPlan(ShieldHalfYearCode, "Shield Pass - 6 Months", PremiumPackageKind.Shield, PremiumDurationKind.HalfYear, 24 * 30 * 6, false, null, "6 months", "Shield Pass for 6 months.", "29.99 $", true),
            BuildPlan(ShieldYearCode, "Shield Pass - 1 Year", PremiumPackageKind.Shield, PremiumDurationKind.Year, 24 * 365, false, null, "1 year", "Shield Pass for 1 year.", "49.99 $", true)
        ];

        return plans;
    }

    public bool IsFreePlan(string planCode)
        => string.Equals(planCode, BattlePassTrialCode, StringComparison.Ordinal);

    private async Task<Guid> HandleCheckoutSessionCompletedAsync(AppDbContext db, CheckoutSession session, string stripeEventId)
    {
        var metadata = session.Metadata ?? new Dictionary<string, string>();
        var userId = ParseGuidMetadata(metadata, "userId");
        var planCode = GetMetadata(metadata, "planCode");

        if (string.IsNullOrWhiteSpace(session.CustomerId))
        {
            throw new InvalidOperationException("Stripe checkout session is missing customer ID.");
        }

        var user = await ResolveUserAsync(db, userId, session.CustomerId) ?? throw new InvalidOperationException("Unable to resolve user for Stripe checkout session.");
        user.StripeCustomerId ??= session.CustomerId;

        var existingPaymentEvent = await db.PremiumPaymentEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.StripeObjectId == session.Id && e.Status == "processed");

        if (existingPaymentEvent is not null)
        {
            return existingPaymentEvent.UsersId;
        }

        if (string.Equals(session.Mode, "payment", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(planCode))
            {
                throw new InvalidOperationException("Stripe checkout session is missing plan metadata.");
            }

            await ApplyPlanAsync(db, user, planCode, DateTime.UtcNow);
            return user.UsersId;
        }

        throw new InvalidOperationException("Unsupported Stripe checkout session mode.");
    }

    private async Task ApplyPlanAsync(AppDbContext db, User user, string planCode, DateTime nowUtc)
    {
        var plan = GetPlanOrThrow(planCode);
        var player = user.Player ?? throw new InvalidOperationException("Player not found.");
        DateTime? currentSeasonEndsAtUtc = plan.DurationKind == PremiumDurationKind.Season
            ? (await seasonService.GetCurrentSeasonAsync()).EndsAtUtc
            : null;
        switch (plan.PackageKind)
        {
            case PremiumPackageKind.BattlePass:
                player.BattlePassValidTill = CalculateExpiration(
                    player.BattlePassValidTill,
                    plan.DurationKind,
                    nowUtc,
                    currentSeasonEndsAtUtc);
                if (plan.IsFreePlan)
                {
                    player.BattlePassTrialGrantedAt ??= nowUtc;
                }
                break;
            case PremiumPackageKind.Shield:
                player.ShieldValidTill = CalculateExpiration(player.ShieldValidTill, plan.DurationKind, nowUtc, currentSeasonEndsAtUtc);
                break;
            default:
                throw new InvalidOperationException("Unsupported premium package type.");
        }

        if (plan.PackageKind == PremiumPackageKind.BattlePass)
        {
            await SyncPremiumBadgeAsync(db, player.PlayersId, shouldHaveBadge: true);
        }
    }

    private static DateTime CalculateExpiration(
        DateTime? currentExpiration,
        PremiumDurationKind durationKind,
        DateTime nowUtc,
        DateTime? seasonEndsAtUtc)
    {
        var baseTime = currentExpiration.HasValue && currentExpiration.Value > nowUtc ? currentExpiration.Value : nowUtc;
        return durationKind switch
        {
            PremiumDurationKind.Trial24Hours => nowUtc.AddHours(24),
            PremiumDurationKind.Season => seasonEndsAtUtc ?? nowUtc.AddDays(SeasonService.SeasonDurationDays),
            PremiumDurationKind.Month => baseTime.AddMonths(1),
            PremiumDurationKind.HalfYear => baseTime.AddMonths(6),
            PremiumDurationKind.Year => baseTime.AddYears(1),
            _ => throw new InvalidOperationException("Unsupported premium duration.")
        };
    }

    private static async Task SyncPremiumBadgeAsync(AppDbContext db, Guid playerId, bool shouldHaveBadge)
    {
        var existingBadges = await db.PlayerBadges
            .Where(badge => badge.PlayersId == playerId && badge.BadgeCode == nameof(BadgeTypeCodeEnum.Premium))
            .ToListAsync();

        if (shouldHaveBadge)
        {
            if (existingBadges.Count == 0)
            {
                db.PlayerBadges.Add(new PlayerBadge
                {
                    PlayerBadgesId = Guid.NewGuid(),
                    PlayersId = playerId,
                    BadgeCode = nameof(BadgeTypeCodeEnum.Premium),
                    AwardedAt = DateTime.UtcNow
                });
            }

            return;
        }

        if (existingBadges.Count > 0)
        {
            db.PlayerBadges.RemoveRange(existingBadges);
        }
    }

    private async Task<User> GetUserByPlayerIdAsync(AppDbContext db, Guid playerId)
        => await db.Users
            .Include(u => u.Player)
            .FirstOrDefaultAsync(u => u.Player != null && u.Player.PlayersId == playerId)
           ?? throw new InvalidOperationException("Player user not found.");

    private async Task<string> GetOrCreateStripeCustomerIdAsync(AppDbContext db, User user)
    {
        if (!string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            return user.StripeCustomerId;
        }

        var service = new CustomerService();
        var customer = await service.CreateAsync(new CustomerCreateOptions
        {
            Email = user.Email,
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.UsersId.ToString(),
                ["playerId"] = user.Player?.PlayersId.ToString() ?? string.Empty
            }
        });

        user.StripeCustomerId = customer.Id;
        await db.SaveChangesAsync();
        return customer.Id;
    }

    private async Task<User?> ResolveUserAsync(AppDbContext db, Guid? explicitUserId, string stripeCustomerId)
    {
        if (explicitUserId.HasValue)
        {
            return await db.Users
                .Include(u => u.Player)
                .FirstOrDefaultAsync(u => u.UsersId == explicitUserId.Value);
        }

        return await db.Users
            .Include(u => u.Player)
            .FirstOrDefaultAsync(u => u.StripeCustomerId == stripeCustomerId);
    }

    private ConfiguredPlan GetPlanOrThrow(string planCode)
        => BuildPlanCatalog().FirstOrDefault(q => q.Code == planCode)
           ?? throw new InvalidOperationException("Premium plan was not found.");

    private static DateTime? GetBattlePassEndsAt(Player? player)
        => player?.BattlePassValidTill;

    private void ConfigureStripe()
    {
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }

        StripeConfiguration.ApiKey = secretKey;
    }

    private static string? GetMetadata(IDictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value) ? value : null;

    private static Guid? ParseGuidMetadata(IDictionary<string, string> metadata, string key)
        => Guid.TryParse(GetMetadata(metadata, key), out var value) ? value : null;

    private static string? NormalizeStripePriceId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string? GetStripePriceId(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = NormalizeStripePriceId(configuration[key]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal GetSeasonPriceAmount(DateTime seasonEndsAtUtc)
    {
        var seasonDurationDays = Math.Max(1m, SeasonService.SeasonDurationDays);
        var remainingDays = Math.Max(0.01m, (decimal)(seasonEndsAtUtc - DateTime.UtcNow).TotalDays);
        return Math.Round(CurrentSeasonFullPriceUsd * (remainingDays / seasonDurationDays), 2, MidpointRounding.AwayFromZero);
    }

    private static long ToStripeAmountInMinorUnits(decimal amount)
        => Math.Max(1L, (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero));

    private static CheckoutSessionLineItemOptions BuildCheckoutLineItem(ConfiguredPlan plan, DateTime currentSeasonEndsAtUtc)
    {
        var amount = GetPlanAmount(plan, currentSeasonEndsAtUtc);
        return new CheckoutSessionLineItemOptions
        {
            PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
            {
                Currency = "usd",
                UnitAmount = ToStripeAmountInMinorUnits(amount),
                ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                {
                    Name = plan.Name
                }
            },
            Quantity = 1
        };
    }

    private static decimal GetPlanAmount(ConfiguredPlan plan, DateTime currentSeasonEndsAtUtc)
        => plan.DurationKind switch
        {
            PremiumDurationKind.Season => GetSeasonPriceAmount(currentSeasonEndsAtUtc),
            PremiumDurationKind.Month => 7.99m,
            PremiumDurationKind.HalfYear => 29.99m,
            PremiumDurationKind.Year => 49.99m,
            PremiumDurationKind.Trial24Hours => 0m,
            _ => throw new InvalidOperationException("Unsupported premium duration.")
        };

    private static string FormatPriceLabel(decimal amount, string currency)
    {
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();
        var symbol = normalizedCurrency switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "PLN" => "zł",
            _ => normalizedCurrency
        };

        return symbol switch
        {
            "zł" => $"{amount:0.00} {symbol}",
            "$" or "€" or "£" => $"{symbol}{amount:0.00}",
            _ when string.IsNullOrWhiteSpace(symbol) => amount.ToString("0.00", CultureInfo.InvariantCulture),
            _ => $"{amount:0.00} {symbol}"
        };
    }

    private ConfiguredPlan[] BuildPlanCatalog()
    {
        return
        [
            BuildPlan(BattlePassTrialCode, "Battle Pass Trial", PremiumPackageKind.BattlePass, PremiumDurationKind.Trial24Hours, 24, true, null, "24h free trial", "Access to x5 speed boosts for 24 hours."),
            BuildPlan(BattlePassSeasonCode, "Battle Pass - Current Season", PremiumPackageKind.BattlePass, PremiumDurationKind.Season, SeasonService.SeasonDurationDays * 24, false, NormalizeStripePriceId(configuration["Stripe:PriceIdBattlePassSeason"]), "Until season ends", "Battle Pass for the current 2-week season."),
            BuildPlan(BattlePassMonthCode, "Battle Pass - 1 Month", PremiumPackageKind.BattlePass, PremiumDurationKind.Month, 24 * 30, false, GetStripePriceId("Stripe:PriceIdBattlePassMonth", "Stripe:PriceIdMonth"), "30 days", "Battle Pass with x5 speed boosts for 1 month."),
            BuildPlan(BattlePassHalfYearCode, "Battle Pass - 6 Months", PremiumPackageKind.BattlePass, PremiumDurationKind.HalfYear, 24 * 30 * 6, false, NormalizeStripePriceId(configuration["Stripe:PriceIdBattlePass6Months"]), "6 months", "Battle Pass with x5 speed boosts for 6 months."),
            BuildPlan(BattlePassYearCode, "Battle Pass - 1 Year", PremiumPackageKind.BattlePass, PremiumDurationKind.Year, 24 * 365, false, GetStripePriceId("Stripe:PriceIdBattlePassYear", "Stripe:PriceIdYear"), "1 year", "Battle Pass with x5 speed boosts for 1 year."),
            BuildPlan(ShieldSeasonCode, "Shield Pass - Current Season", PremiumPackageKind.Shield, PremiumDurationKind.Season, SeasonService.SeasonDurationDays * 24, false, NormalizeStripePriceId(configuration["Stripe:PriceIdShieldSeason"]), "Until season ends", "Shield Pass for the current 2-week season."),
            BuildPlan(ShieldMonthCode, "Shield Pass - 1 Month", PremiumPackageKind.Shield, PremiumDurationKind.Month, 24 * 30, false, NormalizeStripePriceId(configuration["Stripe:PriceIdShieldMonth"]), "30 days", "Shield Pass for 1 month."),
            BuildPlan(ShieldHalfYearCode, "Shield Pass - 6 Months", PremiumPackageKind.Shield, PremiumDurationKind.HalfYear, 24 * 30 * 6, false, NormalizeStripePriceId(configuration["Stripe:PriceIdShield6Months"]), "6 months", "Shield Pass for 6 months."),
            BuildPlan(ShieldYearCode, "Shield Pass - 1 Year", PremiumPackageKind.Shield, PremiumDurationKind.Year, 24 * 365, false, NormalizeStripePriceId(configuration["Stripe:PriceIdShieldYear"]), "1 year", "Shield Pass for 1 year.")
        ];
    }

    private static ConfiguredPlan BuildPlan(
        string code,
        string name,
        PremiumPackageKind packageKind,
        PremiumDurationKind durationKind,
        int durationHours,
        bool isFreePlan,
        string? stripePriceId,
        string durationLabel,
        string description,
        string? priceLabel = null,
        bool isPriceConfigured = false)
        => new(code, name, packageKind, durationKind, durationHours, isFreePlan, stripePriceId, durationLabel, description, priceLabel, isPriceConfigured);

    public enum PremiumPackageKind
    {
        BattlePass,
        Shield
    }

    public enum PremiumDurationKind
    {
        Trial24Hours,
        Season,
        Month,
        HalfYear,
        Year
    }

    public sealed record ConfiguredPlan(
        string Code,
        string Name,
        PremiumPackageKind PackageKind,
        PremiumDurationKind DurationKind,
        int DurationHours,
        bool IsFreePlan,
        string? StripePriceId,
        string DurationLabel,
        string Description,
        string? PriceLabel,
        bool IsPriceConfigured);
}
