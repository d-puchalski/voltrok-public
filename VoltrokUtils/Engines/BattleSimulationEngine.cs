using VoltrokUtils.Enums;

namespace VoltrokUtils.Engines;

public static class BattleSimulationEngine
{
    public static BattleSimulationResult SimulateBattle(
        IEnumerable<BattleSimulationUnitInput> attackerUnits,
        IEnumerable<BattleSimulationUnitInput> defenderUnits,
        bool includeSiegePower = false)
    {
        var attackerState = Normalize(attackerUnits);
        var defenderState = Normalize(defenderUnits);

        decimal attackerPower = 0m;
        decimal defenderPower = 0m;

        var attackerFamilies = attackerState
            .Select(unit => CombatBalanceEngine.NormalizeCombatFamily(unit.UnitCode))
            .Where(family => !string.IsNullOrWhiteSpace(family))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(family => family, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var familiesWithMatchingDefense = new HashSet<string>(
            attackerFamilies.Where(family => defenderState.Any(unit =>
                string.Equals(CombatBalanceEngine.NormalizeCombatFamily(unit.UnitCode), family, StringComparison.OrdinalIgnoreCase))),
            StringComparer.OrdinalIgnoreCase);

        var attackerLocalWins = 0;
        var defenderLocalWins = 0;

        foreach (var family in attackerFamilies.Where(familiesWithMatchingDefense.Contains))
        {
            var attackerFamilyUnits = attackerState
                .Where(unit => unit.SurvivedQuantity > 0)
                .Where(unit => string.Equals(CombatBalanceEngine.NormalizeCombatFamily(unit.UnitCode), family, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var defenderFamilyUnits = defenderState
                .Where(unit => unit.SurvivedQuantity > 0)
                .Where(unit => string.Equals(CombatBalanceEngine.NormalizeCombatFamily(unit.UnitCode), family, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (attackerFamilyUnits.Count == 0 || defenderFamilyUnits.Count == 0)
            {
                continue;
            }

            var attackerStacks = attackerFamilyUnits
                .Select(ToCombatStack)
                .Where(stack => stack.HasValue)
                .Select(stack => stack!.Value)
                .ToList();
            var defenderStacks = defenderFamilyUnits
                .Select(ToCombatStack)
                .Where(stack => stack.HasValue)
                .Select(stack => stack!.Value)
                .ToList();

            var attackerFamilyPower = MilitaryCombatPowerEngine.CalculateAttackerPower(attackerStacks, includeSiegePower);
            var defenderFamilyPower = MilitaryCombatPowerEngine.CalculateDefenderPower(defenderStacks, attackerStacks);
            attackerPower += attackerFamilyPower;
            defenderPower += defenderFamilyPower;

            ApplyDamage(attackerFamilyUnits, defenderFamilyPower);
            ApplyDamage(defenderFamilyUnits, attackerFamilyPower);

            var attackerFamilyRemainingHp = CalculateRemainingHp(attackerFamilyUnits);
            var defenderFamilyRemainingHp = CalculateRemainingHp(defenderFamilyUnits);
            if (attackerFamilyRemainingHp > defenderFamilyRemainingHp)
            {
                attackerLocalWins++;
            }
            else
            {
                defenderLocalWins++;
            }
        }

        foreach (var family in attackerFamilies.Where(family => !familiesWithMatchingDefense.Contains(family)))
        {
            var attackerFamilyUnits = attackerState
                .Where(unit => unit.SurvivedQuantity > 0)
                .Where(unit => string.Equals(CombatBalanceEngine.NormalizeCombatFamily(unit.UnitCode), family, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (attackerFamilyUnits.Count == 0)
            {
                continue;
            }

            attackerLocalWins++;

            var attackerStacks = attackerFamilyUnits
                .Select(ToCombatStack)
                .Where(stack => stack.HasValue)
                .Select(stack => stack!.Value)
                .ToList();
            if (attackerStacks.Count == 0)
            {
                continue;
            }

            var attackerFamilyPower = MilitaryCombatPowerEngine.CalculateAttackerPower(attackerStacks, includeSiegePower);
            attackerPower += attackerFamilyPower;

            var remainingDefenderUnits = defenderState
                .Where(unit => unit.SurvivedQuantity > 0)
                .ToList();
            if (remainingDefenderUnits.Count == 0)
            {
                continue;
            }

            //ApplyDamage(remainingDefenderUnits, attackerFamilyPower);
        }

        var attackerRemainingHp = CalculateRemainingHp(attackerState);
        var defenderRemainingHp = CalculateRemainingHp(defenderState);
        var attackerWon = attackerLocalWins > defenderLocalWins
                          || (attackerLocalWins == defenderLocalWins && attackerRemainingHp > defenderRemainingHp);

        return new BattleSimulationResult(
            attackerWon,
            attackerPower,
            defenderPower,
            attackerLocalWins,
            defenderLocalWins,
            attackerRemainingHp,
            defenderRemainingHp,
            BuildUnitInputs(attackerState, includeKilled: false),
            BuildUnitInputs(defenderState, includeKilled: false),
            BuildUnitInputs(attackerState, includeKilled: true),
            BuildUnitInputs(defenderState, includeKilled: true),
            BuildSideOutcome(attackerState),
            BuildSideOutcome(defenderState));
    }

    private static List<MutableBattleUnit> Normalize(IEnumerable<BattleSimulationUnitInput> units)
        => units
            .Where(unit => unit.Quantity > 0 && !string.IsNullOrWhiteSpace(unit.UnitCode))
            .GroupBy(unit => new BattleUnitKey(unit.UnitCode.Trim(), Math.Max(1, unit.Level)))
            .Select(group => new MutableBattleUnit(group.Key.UnitCode, group.Key.Level, group.Sum(unit => unit.Quantity)))
            .OrderByDescending(unit => unit.Level)
            .ThenBy(unit => unit.UnitCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static CombatUnitStack? ToCombatStack(MutableBattleUnit unit)
        => MilitaryCombatPowerEngine.CreateStack(unit.Definition.Code.ToString(), unit.Level, unit.SurvivedQuantity);

    private static void ApplyDamage(List<MutableBattleUnit> units, decimal incomingDamage)
    {
        var remainingDamage = Math.Max(0m, incomingDamage);

        foreach (var unit in units
                     .OrderBy(unit => unit.Definition.GetDurability(unit.Level))
                     .ThenByDescending(unit => unit.Level)
                     .ThenBy(unit => unit.UnitCode, StringComparer.OrdinalIgnoreCase))
        {
            if (remainingDamage <= 0m)
            {
                break;
            }

            var durability = Math.Max(1m, unit.Definition.GetDurability(unit.Level));
            var possibleKills = (int)Math.Floor(remainingDamage / durability);
            if (possibleKills <= 0)
            {
                continue;
            }

            var kills = Math.Min(unit.SurvivedQuantity, possibleKills);
            unit.SurvivedQuantity -= kills;
            remainingDamage -= kills * durability;
        }
    }

    private static BattleSimulationSideOutcome BuildSideOutcome(IEnumerable<MutableBattleUnit> units)
    {
        var results = units
            .Select(unit => new BattleSimulationUnitOutcome(
                unit.UnitCode,
                unit.Level,
                unit.InitialQuantity,
                unit.SurvivedQuantity,
                unit.InitialQuantity - unit.SurvivedQuantity))
            .ToList();
        var initialTotalHp = units.Sum(unit => unit.InitialQuantity * unit.Definition.GetDurability(unit.Level));
        var survivedTotalHp = units.Sum(unit => unit.SurvivedQuantity * unit.Definition.GetDurability(unit.Level));

        return new BattleSimulationSideOutcome(
            results.Sum(unit => unit.InitialQuantity),
            results.Sum(unit => unit.SurvivedQuantity),
            results.Sum(unit => unit.KilledQuantity),
            initialTotalHp,
            survivedTotalHp,
            Math.Max(0m, initialTotalHp - survivedTotalHp),
            results);
    }

    private static decimal CalculateRemainingHp(IEnumerable<MutableBattleUnit> units)
        => units.Sum(unit => unit.SurvivedQuantity * unit.Definition.GetDurability(unit.Level));

    private static IReadOnlyList<BattleSimulationUnitInput> BuildUnitInputs(IEnumerable<MutableBattleUnit> units, bool includeKilled)
        => units
            .Select(unit => new BattleSimulationUnitInput(
                unit.UnitCode,
                unit.Level,
                includeKilled ? unit.InitialQuantity - unit.SurvivedQuantity : unit.SurvivedQuantity))
            .Where(unit => unit.Quantity > 0)
            .OrderByDescending(unit => unit.Level)
            .ThenBy(unit => unit.UnitCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class MutableBattleUnit(string unitCode, int level, int quantity)
    {
        public MilitaryUnit Definition { get; } = MilitaryUnitEngine.FindByCode(unitCode)
            ?? throw new InvalidOperationException($"Military unit '{unitCode}' not found.");
        public string UnitCode => Definition.Code.ToString();
        public int Level { get; } = level;
        public int InitialQuantity { get; } = quantity;
        public int SurvivedQuantity { get; set; } = quantity;
    }

    private readonly record struct BattleUnitKey(string UnitCode, int Level);
}

public sealed record BattleSimulationUnitInput(string UnitCode, int Level, int Quantity);

public sealed record BattleSimulationUnitOutcome(
    string UnitCode,
    int Level,
    int InitialQuantity,
    int SurvivedQuantity,
    int KilledQuantity);

public sealed record BattleSimulationSideOutcome(
    int InitialTotalUnits,
    int SurvivedTotalUnits,
    int KilledTotalUnits,
    decimal InitialTotalHp,
    decimal SurvivedTotalHp,
    decimal LostTotalHp,
    IReadOnlyList<BattleSimulationUnitOutcome> Units);

public sealed record BattleSimulationResult(
    bool AttackerWon,
    decimal AttackerPower,
    decimal DefenderPower,
    int AttackerLocalWins,
    int DefenderLocalWins,
    decimal AttackerRemainingHp,
    decimal DefenderRemainingHp,
    IReadOnlyList<BattleSimulationUnitInput> AttackerUnits,
    IReadOnlyList<BattleSimulationUnitInput> DefenderUnits,
    IReadOnlyList<BattleSimulationUnitInput> AttackerKilledUnits,
    IReadOnlyList<BattleSimulationUnitInput> DefenderKilledUnits,
    BattleSimulationSideOutcome Attacker,
    BattleSimulationSideOutcome Defender);
