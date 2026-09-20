using Microsoft.EntityFrameworkCore;
using VoltrokEF;
using VoltrokServices.Services.Governance;
using VoltrokServices.Services.Premium;

namespace VoltrokServices.Services.Factions;

public class FactionGovernanceService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<FactionGovernanceSnapshot?> GetFactionGovernanceAsync(Guid countryId, string factionCode, Guid? viewerPlayerId)
    {
        if (string.IsNullOrWhiteSpace(factionCode))
        {
            return null;
        }

        var normalizedFactionCode = factionCode.Trim().ToUpperInvariant();
        var db = await dbContextFactory.CreateDbContextAsync();

        var faction = await db.CountryFactions
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.CountriesId == countryId && f.FactionCode == normalizedFactionCode);
        if (faction == null)
        {
            return null;
        }

        var country = await db.Countries
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CountriesId == countryId);
        if (country == null)
        {
            return null;
        }

        var presidentName = faction.PresidentPlayersId.HasValue
            ? await db.Players
                .AsNoTracking()
                .Where(v => v.PlayersId == faction.PresidentPlayersId.Value)
                .Select(v => v.Name)
                .FirstOrDefaultAsync()
            : null;

        var viewerMembership = viewerPlayerId.HasValue
            ? await db.Players
                .AsNoTracking()
                .Where(v => v.PlayersId == viewerPlayerId.Value)
                .Select(v => new { v.CountriesId, v.FactionCode })
                .FirstOrDefaultAsync()
            : null;

        var members = await db.Players
            .AsNoTracking()
            .Where(player => player.CountriesId == countryId && player.FactionCode == normalizedFactionCode)
            .OrderByDescending(player => player.PlayersId == faction.PresidentPlayersId)
            .ThenByDescending(player => player.Score)
            .ThenBy(player => player.Name)
            .Select(player => new FactionMemberState
            {
                PlayerId = player.PlayersId,
                PlayerName = PremiumFeatures.FormatPlayerName(player.Name, player.CreatedAt),
                IsPresident = player.PlayersId == faction.PresidentPlayersId,
                IsNpc = player.IsNpc,
                Score = player.Score,
                Money = player.Money,
                CreatedAtUtc = player.CreatedAt
            })
            .ToListAsync();

        var inventory = await db.CountryFactionInventories
            .AsNoTracking()
            .Where(item => item.CountriesId == countryId && item.FactionCode == normalizedFactionCode && item.Quantity > 0)
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.Products.Code)
            .Select(item => new FactionInventoryState
            {
                ProductId = item.ProductsId,
                ProductCode = item.Products.Code,
                Quantity = item.Quantity
            })
            .ToListAsync();

        var military = await db.CountryFactionMilitaries
            .AsNoTracking()
            .Where(item => item.CountriesId == countryId && item.FactionCode == normalizedFactionCode && item.Quantity > 0)
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.MilitaryUnits.Code)
            .Select(item => new FactionMilitaryState
            {
                MilitaryUnitId = item.MilitaryUnitsId,
                UnitCode = item.MilitaryUnits.Code,
                Quantity = item.Quantity,
                AttackPowerPerUnit = item.MilitaryUnits.AttackPoints,
                DefensePowerPerUnit = item.MilitaryUnits.DefensePoints,
                TotalAttackPower = item.Quantity * item.MilitaryUnits.AttackPoints,
                TotalDefensePower = item.Quantity * item.MilitaryUnits.DefensePoints
            })
            .ToListAsync();

        return new FactionGovernanceSnapshot
        {
            CountryId = countryId,
            CountryName = country.Name,
            FactionCode = normalizedFactionCode,
            FactionName = FactionCatalog.GetDisplayName(normalizedFactionCode),
            PresidentPlayerId = faction.PresidentPlayersId,
            PresidentPlayerName = presidentName,
            IsViewerInFaction = viewerMembership?.CountriesId == countryId
                && string.Equals(viewerMembership.FactionCode, normalizedFactionCode, StringComparison.OrdinalIgnoreCase),
            IsViewerPresident = viewerPlayerId.HasValue && faction.PresidentPlayersId == viewerPlayerId,
            NextAutomaticPresidentSelectionAtUtc = AutomaticPresidentSelectionSchedule.GetNextRunAtUtc(DateTime.UtcNow),
            FactionTaxPercent = faction.TaxPercent,
            CountryTaxPercent = country.TaxPercent,
            TreasuryMoney = faction.TreasuryMoney,
            LocationX = faction.LocationX,
            LocationY = faction.LocationY,
            MemberCount = members.Count,
            InventoryStackCount = inventory.Count,
            InventoryQuantityTotal = inventory.Sum(item => item.Quantity),
            MilitaryStackCount = military.Count,
            MilitaryQuantityTotal = military.Sum(item => item.Quantity),
            MilitaryAttackPower = military.Sum(item => item.TotalAttackPower),
            MilitaryDefensePower = military.Sum(item => item.TotalDefensePower),
            Members = members,
            Inventory = inventory,
            Military = military
        };
    }
}
