using VoltrokUtils.Enums;

namespace VoltrokUtils.Engines;

public static class MilitaryCombatPowerEngine
{
    public static CombatUnitStack? CreateStack(string? unitCode, int level, int quantity)
    {
        if (quantity <= 0)
        {
            return null;
        }

        var definition = MilitaryUnitEngine.FindByCode(unitCode);
        if (definition == null)
        {
            return null;
        }

        return new CombatUnitStack(definition, Math.Max(1, level), Math.Max(0, quantity));
    }

    public static decimal CalculateAttackerPower(IEnumerable<CombatUnitStack> attackerUnits, bool includeSiegePower = false)
        => attackerUnits.Sum(unit =>
        {
            var unitPower = unit.Definition.GetOffensivePoints(unit.Level);
            if (includeSiegePower)
            {
                unitPower += unit.Definition.GetSiegePoints(unit.Level);
            }

            return unitPower * unit.Quantity;
        });

    public static decimal CalculateDefenderPower(IEnumerable<CombatUnitStack> defenderUnits, IEnumerable<CombatUnitStack> attackerUnits)
    {
        var attackerTypes = attackerUnits
            .Where(unit => unit.Quantity > 0)
            .Select(unit => unit.Definition.Code)
            .ToHashSet();

        return defenderUnits.Sum(unit =>
            IsDefenseEffective(unit.Definition, attackerTypes)
                ? unit.Definition.GetDefensivePoints(unit.Level) * unit.Quantity
                : 0m);
    }

    public static bool IsDefenseEffective(MilitaryUnit unit, IReadOnlySet<MilitaryUnitsEnum> attackerTypes)
        => unit.DefensiveAgainst == null || attackerTypes.Contains(unit.DefensiveAgainst.Value);
}

public readonly record struct CombatUnitStack(MilitaryUnit Definition, int Level, int Quantity);
