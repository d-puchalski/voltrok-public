using Microsoft.EntityFrameworkCore;

using Npgsql;
using Serilog;

using VoltrokEF;
using VoltrokUtils.Engines;
using VoltrokUtils.Enums;

namespace VoltrokServices.Services.Countries;

public class CountryGovernanceService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<Country?> GetCountryDetailsAsync(Guid countryId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        return await db.Countries
            .AsNoTracking()
            .Include(country => country.Players)
            .Include(country => country.CountryBuildings)
            .Include(country => country.CountryTradePolicySourceCountries)
                .ThenInclude(policy => policy.TargetCountries)
            .Include(country => country.CountryWarCountryFroms)
                .ThenInclude(war => war.CountryTo)
            .Include(country => country.CountryWarCountryTos)
                .ThenInclude(war => war.CountryFrom)
            .FirstOrDefaultAsync(country => country.CountriesId == countryId);
    }

    public async Task UpsertCountryTradePolicyAsync(Guid presidentPlayerId, Guid targetCountryId, bool isEmbargo, decimal tariffPercent)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var sourceCountryId = await db.Players.Where(p => p.PlayersId == presidentPlayerId).Select(p => p.CountriesId).FirstAsync();
        var policy = await db.CountryTradePolicies.FirstOrDefaultAsync(p => p.SourceCountriesId == sourceCountryId && p.TargetCountriesId == targetCountryId);
        if (policy == null)
        {
            policy = new CountryTradePolicy
            {
                CountryTradePoliciesId = Guid.NewGuid(),
                SourceCountriesId = sourceCountryId,
                TargetCountriesId = targetCountryId
            };
            db.CountryTradePolicies.Add(policy);
        }
        policy.IsEmbargo = isEmbargo;
        policy.TariffPercent = tariffPercent;
        await db.SaveChangesAsync();
    }

    public async Task RemoveCountryTradePolicyAsync(Guid presidentPlayerId, Guid targetCountryId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var sourceCountryId = await db.Players.Where(p => p.PlayersId == presidentPlayerId).Select(p => p.CountriesId).FirstOrDefaultAsync();
        var policy = await db.CountryTradePolicies.FirstOrDefaultAsync(p => p.SourceCountriesId == sourceCountryId && p.TargetCountriesId == targetCountryId);
        if (policy == null) return;
        db.CountryTradePolicies.Remove(policy);
        await db.SaveChangesAsync();
    }

    public async Task<CountryWar> DeclareCountryWarAsync(Guid presidentPlayerId, Guid defenderCountryId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var attackerCountryId = await db.Players.Where(p => p.PlayersId == presidentPlayerId).Select(p => p.CountriesId).FirstAsync();

        if (attackerCountryId == defenderCountryId)
        {
            throw new InvalidOperationException("A country cannot declare war on itself.");
        }

        var existingActiveWar = await db.CountryWars.FirstOrDefaultAsync(war =>
            war.Status == CountryWarStatus.Active.ToDbValue()
            && ((war.CountryFromId == attackerCountryId && war.CountryToId == defenderCountryId)
                || (war.CountryFromId == defenderCountryId && war.CountryToId == attackerCountryId)));

        if (existingActiveWar != null)
        {
            if (existingActiveWar.CountryFromId == attackerCountryId && existingActiveWar.CountryToId == defenderCountryId)
            {
                return existingActiveWar;
            }

            throw new InvalidOperationException("There is already an active war between these countries.");
        }

        var existingWar = await db.CountryWars.FirstOrDefaultAsync(war =>
            war.CountryFromId == attackerCountryId
            && war.CountryToId == defenderCountryId);

        if (existingWar != null)
        {
            existingWar.Status = CountryWarStatus.Active.ToDbValue();
            existingWar.StartedAt = DateTime.UtcNow;
            existingWar.EndedAt = null;
            await db.SaveChangesAsync();
            return existingWar;
        }

        var war = new CountryWar
        {
            CountryWarsId = Guid.NewGuid(),
            CountryFromId = attackerCountryId,
            CountryToId = defenderCountryId,
            Status = CountryWarStatus.Active.ToDbValue(),
            StartedAt = DateTime.UtcNow
        };

        try
        {
            db.CountryWars.Add(war);
            await db.SaveChangesAsync();
            return war;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                                                  {
                                                      SqlState: PostgresErrorCodes.UniqueViolation,
                                                      ConstraintName: "country_wars_country_from_id_country_to_id_key"
                                                  })
        {
            Log.Warning(
                exception,
                "Concurrent country war declaration detected between {AttackerCountryId} and {DefenderCountryId}. Returning persisted war if available.",
                attackerCountryId,
                defenderCountryId);

            var persistedWar = await db.CountryWars.FirstOrDefaultAsync(existing =>
                existing.CountryFromId == attackerCountryId
                && existing.CountryToId == defenderCountryId);

            if (persistedWar != null)
            {
                return persistedWar;
            }

            throw;
        }
    }

    public async Task CancelCountryWarAsync(Guid presidentPlayerId, Guid warId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var requesterCountryId = await db.Players
            .Where(p => p.PlayersId == presidentPlayerId)
            .Select(p => p.CountriesId)
            .FirstAsync();

        var war = await db.CountryWars.FirstOrDefaultAsync(w =>
            w.CountryWarsId == warId
            && w.Status == CountryWarStatus.Active.ToDbValue()
            && (w.CountryFromId == requesterCountryId || w.CountryToId == requesterCountryId))
            ?? throw new InvalidOperationException("Active war not found.");

        await RecallWarMissionsAsync(db, war.CountryFromId, war.CountryToId);

        war.Status = CountryWarStatus.Cancelled.ToDbValue();
        war.EndedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task SetCountryTaxAsync(Guid presidentPlayerId, decimal taxPercent)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var countryId = await db.Players.Where(p => p.PlayersId == presidentPlayerId).Select(p => p.CountriesId).FirstAsync();
        var country = await db.Countries.FirstOrDefaultAsync(c => c.CountriesId == countryId) ?? throw new InvalidOperationException("Country not found.");
        country.TaxPercent = Math.Clamp(taxPercent, 0m, 20m);
        await db.SaveChangesAsync();
    }

    public async Task SetCountryCombatPolicyAsync(Guid presidentPlayerId, bool allowToAttackInsideCountry, bool allowToAttackWithoutWarDeclaration)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        var countryId = await db.Players.Where(p => p.PlayersId == presidentPlayerId).Select(p => p.CountriesId).FirstAsync();
        var country = await db.Countries.FirstOrDefaultAsync(c => c.CountriesId == countryId) ?? throw new InvalidOperationException("Country not found.");
        country.IsAllowToAttackInsideCountry = allowToAttackInsideCountry;
        country.IsAllowToAttackWithoutWarDeclaration = allowToAttackWithoutWarDeclaration;
        await db.SaveChangesAsync();
    }

    public async Task<(bool AllowToAttackInsideCountry, bool AllowToAttackWithoutWarDeclaration)> GetCountryCombatPolicyAsync(Guid countryId)
    {
        var db = await dbContextFactory.CreateDbContextAsync();
        return await db.Countries
            .Where(country => country.CountriesId == countryId)
            .Select(country => new ValueTuple<bool, bool>(
                country.IsAllowToAttackInsideCountry,
                country.IsAllowToAttackWithoutWarDeclaration))
            .FirstOrDefaultAsync();
    }

    public async Task UpgradeCountryBuildingAsync(Guid presidentPlayerId, string buildingCode)
    {
        var normalizedBuildingCode = NormalizeCountryBuildingCode(buildingCode);
        var db = await dbContextFactory.CreateDbContextAsync();
        var managedCountry = await ResolveManagedCountryAsync(db, presidentPlayerId);

        var existingBuilding = await db.CountryBuildings
            .Where(building => building.CountriesId == managedCountry.CountriesId
                               && building.BuildingCode == normalizedBuildingCode)
            .OrderByDescending(building => building.Level)
            .ThenByDescending(building => building.StartedAt)
            .FirstOrDefaultAsync();

        var currentLevel = existingBuilding?.Level ?? 0;
        var upgradeCost = GetCountryBuildingUpgradeCost(normalizedBuildingCode, currentLevel + 1);
        if (managedCountry.Money < upgradeCost)
        {
            throw new InvalidOperationException("Country treasury does not have enough funds for this upgrade.");
        }

        managedCountry.Money -= upgradeCost;

        if (existingBuilding == null)
        {
            db.CountryBuildings.Add(new CountryBuilding
            {
                CountryBuildingsId = Guid.NewGuid(),
                CountriesId = managedCountry.CountriesId,
                BuildingCode = normalizedBuildingCode,
                Level = 1,
                Status = CountryBuildingStatus.Active.ToDbValue(),
                StartedAt = DateTime.UtcNow,
                EndedAt = null
            });
        }
        else
        {
            existingBuilding.Level += 1;
            existingBuilding.Status = CountryBuildingStatus.Active.ToDbValue();
            existingBuilding.StartedAt = DateTime.UtcNow;
            existingBuilding.EndedAt = null;
        }

        await db.SaveChangesAsync();
    }

    public async Task<int> RefreshAutomaticCountryPresidentsAsync(CancellationToken cancellationToken = default)
    {
        var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var countries = await db.Countries.ToListAsync(cancellationToken);
        var players = await db.Players
            .Where(q => !q.IsNpc)
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.PlayersId)
            .ToListAsync(cancellationToken);

        var playersByCountry = players
            .GroupBy(p => p.CountriesId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var changedCountries = 0;

        foreach (var country in countries)
        {
            playersByCountry.TryGetValue(country.CountriesId, out var members);
            members ??= [];

            var councilIds = members
                .Take(10)
                .Select(p => p.PlayersId)
                .ToHashSet();

            var nextPresidentId = members.FirstOrDefault()?.PlayersId;
            var hasCountryChanges = country.PresidentPlayersId != nextPresidentId;

            country.PresidentPlayersId = nextPresidentId;

            foreach (var member in members)
            {
                var shouldBeCouncilMember = councilIds.Contains(member.PlayersId);
                if (member.IsCountryCouncilMember != shouldBeCouncilMember)
                {
                    member.IsCountryCouncilMember = shouldBeCouncilMember;
                    hasCountryChanges = true;
                }
            }

            if (hasCountryChanges)
            {
                changedCountries++;
            }
        }

        if (changedCountries > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return changedCountries;
    }

    private static string NormalizeCountryBuildingCode(string buildingCode)
    {
        return BuildingUnitEngine.NormalizeCode(buildingCode)
            ?? throw new InvalidOperationException("Unsupported country building.");
    }

    private static decimal GetCountryBuildingUpgradeCost(string buildingCode, int targetLevel)
        => BuildingUnitEngine.GetUpgradeCost(buildingCode, targetLevel);

    private static async Task<Country> ResolveManagedCountryAsync(AppDbContext db, Guid presidentPlayerId)
    {
        var player = await db.Players
            .Where(p => p.PlayersId == presidentPlayerId)
            .Select(p => new { p.PlayersId, p.CountriesId })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Player not found.");

        var country = await db.Countries.FirstOrDefaultAsync(c => c.CountriesId == player.CountriesId)
            ?? throw new InvalidOperationException("Country not found.");

        if (country.PresidentPlayersId != presidentPlayerId)
        {
            throw new InvalidOperationException("Only the country president can manage country buildings.");
        }

        return country;
    }

    private static async Task RecallWarMissionsAsync(AppDbContext db, Guid firstCountryId, Guid secondCountryId)
    {
        var now = DateTime.UtcNow;
        var countryPlayerIds = await db.Players
            .Where(player => player.CountriesId == firstCountryId || player.CountriesId == secondCountryId)
            .Select(player => new { player.PlayersId, player.CountriesId })
            .ToListAsync();

        var firstCountryPlayerIds = countryPlayerIds
            .Where(player => player.CountriesId == firstCountryId)
            .Select(player => player.PlayersId)
            .ToHashSet();
        var secondCountryPlayerIds = countryPlayerIds
            .Where(player => player.CountriesId == secondCountryId)
            .Select(player => player.PlayersId)
            .ToHashSet();

        if (firstCountryPlayerIds.Count == 0 || secondCountryPlayerIds.Count == 0)
        {
            return;
        }

        var affectedTransports = await db.PlayerMilitaryTransports
            .Include(transport => transport.PlayerMilitaryTransportUnits)
            .Where(transport =>
                (transport.MissionType == PlayerMilitaryTransportMissionType.Attack.ToDbValue() || transport.MissionType == PlayerMilitaryTransportMissionType.Siege.ToDbValue())
                && (transport.Status == PlayerTransportStatus.InProgress.ToDbValue() || transport.Status == PlayerTransportStatus.InSiege.ToDbValue())
                && ((firstCountryPlayerIds.Contains(transport.PlayerFromId) && secondCountryPlayerIds.Contains(transport.PlayerToId))
                    || (secondCountryPlayerIds.Contains(transport.PlayerFromId) && firstCountryPlayerIds.Contains(transport.PlayerToId))))
            .ToListAsync();

        if (affectedTransports.Count > 0)
        {
            var sourcePlayerIds = affectedTransports
                .Select(transport => transport.PlayerFromId)
                .Distinct()
                .ToList();

            var rosterEntries = await db.PlayerMilitaryUnits
                .Where(unit => sourcePlayerIds.Contains(unit.PlayersId))
                .ToListAsync();

            foreach (var transport in affectedTransports)
            {
                foreach (var transportUnit in transport.PlayerMilitaryTransportUnits.Where(unit => unit.Quantity > 0))
                {
                    var rosterEntry = rosterEntries.FirstOrDefault(unit =>
                        unit.PlayersId == transport.PlayerFromId
                        && unit.Level == transportUnit.Level
                        && string.Equals(unit.MilitaryUnitsCode, transportUnit.MilitaryUnitsCode, StringComparison.OrdinalIgnoreCase));

                    if (rosterEntry == null)
                    {
                        rosterEntry = new PlayerMilitaryUnit
                        {
                            PlayerMilitaryUnitsId = Guid.NewGuid(),
                            PlayersId = transport.PlayerFromId,
                            MilitaryUnitsCode = transportUnit.MilitaryUnitsCode,
                            Level = transportUnit.Level,
                            Quantity = 0,
                            CreatedAt = now
                        };
                        rosterEntries.Add(rosterEntry);
                        db.PlayerMilitaryUnits.Add(rosterEntry);
                    }

                    rosterEntry.Quantity += transportUnit.Quantity;
                }

                transport.Status = PlayerTransportStatus.Completed.ToDbValue();
                transport.EndTime = now;
            }
        }

        var activeSieges = await db.PlayerSieges
            .Where(siege => siege.Status == PlayerSiegesStatus.Active.ToDbValue()
                            && ((firstCountryPlayerIds.Contains(siege.PlayerFromId) && secondCountryPlayerIds.Contains(siege.PlayerToId))
                                || (secondCountryPlayerIds.Contains(siege.PlayerFromId) && firstCountryPlayerIds.Contains(siege.PlayerToId))))
            .ToListAsync();

        foreach (var siege in activeSieges)
        {
            siege.Status = PlayerSiegesStatus.Cancelled.ToDbValue();
            siege.EndedAt = now;
        }
    }
}
