namespace VoltrokUtils.Models;

public sealed record PremiumStatusView(
    bool IsPremiumActive,
    DateTime? PremiumEndsAtUtc,
    bool CanManageSubscription,
    decimal TransportSpeedMultiplier,
    decimal MilitarySpeedMultiplier,
    decimal UnitProductionSpeedMultiplier,
    decimal ShieldFrequencyMultiplier,
    bool ShieldEnabled,
    bool CanUseCustomAvatar,
    bool HasPremiumBadge,
    DateTime? ShieldEndsAtUtc,
    bool IsShieldPackageActive,
    bool CanClaimBattlePassTrial);
