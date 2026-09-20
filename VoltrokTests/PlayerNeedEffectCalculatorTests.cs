using VoltrokServices.Utils;

namespace VoltrokTests;

public class PlayerNeedEffectCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsPenalty_WhenProductionNeedsAreLow()
    {
        var effects = PlayerNeedEffectCalculator.Calculate(CreateNeeds(
            ("FOOD", 15m),
            ("WATER", 18m),
            ("WARMTH", 22m),
            ("CLOTHING", 25m),
            ("HYGIENE", 19m),
            ("HEALTH", 17m),
            ("SAFETY", 50m),
            ("ENTERTAINMENT", 50m),
            ("RELIGION", 50m),
            ("PRESTIGE", 50m),
            ("EDUCATION", 50m)));

        Assert.Equal(0.55m, effects.ProductionSpeedMultiplier);
        Assert.Equal(0.70m, effects.MilitaryRecruitmentSpeedMultiplier);
        Assert.Equal("Critical shortage", effects.ProductionTier.Label);
        Assert.Equal("Low readiness", effects.MilitaryTier.Label);
    }

    [Fact]
    public void Calculate_ReturnsHighTierBonus_WhenNeedsAreExcellent()
    {
        var effects = PlayerNeedEffectCalculator.Calculate(CreateNeeds(
            ("FOOD", 95m),
            ("WATER", 96m),
            ("WARMTH", 94m),
            ("CLOTHING", 92m),
            ("HYGIENE", 93m),
            ("HEALTH", 97m),
            ("SAFETY", 94m),
            ("ENTERTAINMENT", 86m),
            ("RELIGION", 82m),
            ("PRESTIGE", 88m),
            ("EDUCATION", 90m)));

        Assert.Equal(1.22m, effects.ProductionSpeedMultiplier);
        Assert.Equal(1.18m, effects.MilitaryRecruitmentSpeedMultiplier);
        Assert.True(effects.ProductionTier.IsPeakBonus);
        Assert.True(effects.MilitaryTier.IsPeakBonus);
    }

}
