using VoltrokUtils.Engines;
using VoltrokUtils.Enums;

namespace VoltrokTest;

public class BattleSimulationEngineTests
{
    // Przypadek biznesowy: wyspecjalizowana jednostka obronna, która kontruje
    // rodzinę atakującego, powinna być w stanie zatrzymać natarcie i wybić atak.
    [Fact]
    public void InfantryAttack_IntoGarrisonDefense_DefenderWinsAndInfantryIsDestroyed()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 10)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 5)
            ]);

        Assert.False(result.AttackerWon);
        Assert.Equal(0, result.AttackerLocalWins);
        Assert.Equal(1, result.DefenderLocalWins);
        Assert.Empty(result.AttackerUnits);
        AssertUnits(result.AttackerKilledUnits, (MilitaryUnitsEnum.Infantry, 1, 10));
        AssertUnits(result.DefenderUnits, (MilitaryUnitsEnum.Garrison, 1, 3));
        AssertUnits(result.DefenderKilledUnits, (MilitaryUnitsEnum.Garrison, 1, 2));
    }

    // Przypadek biznesowy: silna formacja ofensywna może przebić się przez twardy counter,
    // ale powinna zapłacić za to odczuwalnymi stratami.
    [Fact]
    public void TankAttack_IntoAntiTankDefense_AttackerWinsButTakesLosses()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Tanks, 1, 10)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.AntiTank, 1, 4)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Tanks, 1, 7));
        AssertUnits(result.AttackerKilledUnits, (MilitaryUnitsEnum.Tanks, 1, 3));
        Assert.Empty(result.DefenderUnits);
        AssertUnits(result.DefenderKilledUnits, (MilitaryUnitsEnum.AntiTank, 1, 4));
    }

    // Przypadek biznesowy: jeśli obrońca nie ma jednostek obronnych z tej samej rodziny,
    // atakujący powinien przebić się przez takie jednostki bez counter-damage.
    [Fact]
    public void Units_WithoutMatchingDefense_AreBrokenByAttackerFamily()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Tanks, 1, 10)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 5)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Tanks, 1, 10));
        Assert.Empty(result.AttackerKilledUnits);
        Assert.Empty(result.DefenderKilledUnits);
        AssertUnits(result.DefenderUnits, (MilitaryUnitsEnum.Garrison, 1, 5));
    }

    // Przypadek biznesowy: brak matching defense daje atakującemu local win,
    // nawet jeśli jego damage nie wystarcza jeszcze do zabicia ogromnej armii obrońcy.
    [Fact]
    public void PureDefensiveCounters_GiveAttackerLocalWin_WhenNoMatchingDefenseExists()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 1)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.AntiTank, 1, 100),
                Unit(MilitaryUnitsEnum.AirDefense, 1, 100),
                Unit(MilitaryUnitsEnum.AntiDroneWarfare, 1, 100),
                Unit(MilitaryUnitsEnum.CyberDefense, 1, 100)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Infantry, 1, 1));
        Assert.Empty(result.AttackerKilledUnits);
        AssertUnits(result.DefenderUnits,
            (MilitaryUnitsEnum.AntiTank, 1, 100),
            (MilitaryUnitsEnum.AirDefense, 1, 100),
            (MilitaryUnitsEnum.AntiDroneWarfare, 1, 100),
            (MilitaryUnitsEnum.CyberDefense, 1, 100));
        Assert.Empty(result.DefenderKilledUnits);
    }

    // Przypadek biznesowy: przytłaczająca przewaga liczebna w pasującej rodzinie
    // nadal powinna dostać realny counter-damage, ale mały stos obrony musi zginąć.
    [Fact]
    public void OverwhelmingInfantryWave_BreaksSingleGarrison_ButStillTakesCounterDamage()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 10)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 1)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Infantry, 1, 5));
        AssertUnits(result.AttackerKilledUnits, (MilitaryUnitsEnum.Infantry, 1, 5));
        Assert.Empty(result.DefenderUnits);
        AssertUnits(result.DefenderKilledUnits, (MilitaryUnitsEnum.Garrison, 1, 1));
    }

    // Przypadek biznesowy: atak bez armii nie może tworzyć fikcyjnych strat
    // ani sztucznego zwycięstwa.
    [Fact]
    public void EmptyAttackerArmy_ProducesNoCasualties_AndNoVictory()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 10)
            ]);

        Assert.False(result.AttackerWon);
        Assert.Equal(0, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        Assert.Empty(result.AttackerUnits);
        Assert.Empty(result.AttackerKilledUnits);
        AssertUnits(result.DefenderUnits, (MilitaryUnitsEnum.Garrison, 1, 10));
        Assert.Empty(result.DefenderKilledUnits);
    }

    // Przypadek biznesowy: jeśli obrońca nie ma armii, family atakującego dostaje local win
    // i cała bitwa kończy się czystym zwycięstwem bez strat.
    [Fact]
    public void EmptyDefenderArmy_ProducesCleanAttackerVictory()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 10)
            ],
            defenderUnits: []);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Infantry, 1, 10));
        Assert.Empty(result.AttackerKilledUnits);
        Assert.Empty(result.DefenderUnits);
        Assert.Empty(result.DefenderKilledUnits);
    }

    // Przypadek biznesowy: zduplikowane wpisy wejściowe dla tego samego kodu jednostki
    // i poziomu powinny zostać scalone do jednego stosu przed rozstrzygnięciem bitwy.
    [Fact]
    public void DuplicateStacks_AreMergedBeforeBattleResolution()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 4),
                Unit(MilitaryUnitsEnum.Infantry, 1, 6)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 1)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Infantry, 1, 5));
        AssertUnits(result.AttackerKilledUnits, (MilitaryUnitsEnum.Infantry, 1, 5));
        Assert.Empty(result.DefenderUnits);
        AssertUnits(result.DefenderKilledUnits, (MilitaryUnitsEnum.Garrison, 1, 1));
    }

    // Przypadek biznesowy: ogromna armia powinna zetrzeć mały, pasujący stos obrońcy,
    // ale obrońca nadal powinien zadać ograniczony counter-damage.
    [Fact]
    public void MillionInfantry_AgainstSingleGarrison_KeepsAlmostEntireArmy()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 1_000_000)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 1)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Infantry, 1, 999_995));
        AssertUnits(result.AttackerKilledUnits, (MilitaryUnitsEnum.Infantry, 1, 5));
        Assert.Empty(result.DefenderUnits);
        AssertUnits(result.DefenderKilledUnits, (MilitaryUnitsEnum.Garrison, 1, 1));
    }

    // Przypadek biznesowy: pojedyncza słaba jednostka bez matching defense po stronie obrońcy
    // dostaje local win, ale nie może magicznie wybić ogromnej armii samym damage.
    [Fact]
    public void SingleInfantry_AgainstThousandUnrelatedCounters_DoesNotCauseImpossibleWipe()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 1)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.AntiTank, 1, 1_000),
                Unit(MilitaryUnitsEnum.AirDefense, 1, 1_000),
                Unit(MilitaryUnitsEnum.AntiDroneWarfare, 1, 1_000),
                Unit(MilitaryUnitsEnum.CyberDefense, 1, 1_000)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Infantry, 1, 1));
        Assert.Empty(result.AttackerKilledUnits);
        AssertUnits(result.DefenderUnits,
            (MilitaryUnitsEnum.AntiTank, 1, 1_000),
            (MilitaryUnitsEnum.AirDefense, 1, 1_000),
            (MilitaryUnitsEnum.AntiDroneWarfare, 1, 1_000),
            (MilitaryUnitsEnum.CyberDefense, 1, 1_000));
        Assert.Empty(result.DefenderKilledUnits);
    }

    // Przypadek biznesowy: rodzina bez matching defense też daje attackerowi local win,
    // więc może przechylić wynik całej bitwy nawet przy dużym HP pozostawionym obrońcy.
    [Fact]
    public void MixedArmy_MoreLocalWinsOverrideRemainingHpOfUnmatchedDefenders()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 10),
                Unit(MilitaryUnitsEnum.Tanks, 1, 3)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 1),
                Unit(MilitaryUnitsEnum.AntiTank, 1, 1),
                Unit(MilitaryUnitsEnum.AirDefense, 1, 100)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(2, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits,
            (MilitaryUnitsEnum.Infantry, 1, 5),
            (MilitaryUnitsEnum.Tanks, 1, 3));
        AssertUnits(result.AttackerKilledUnits,
            (MilitaryUnitsEnum.Infantry, 1, 5));
        AssertUnits(result.DefenderUnits, (MilitaryUnitsEnum.AirDefense, 1, 100));
        AssertUnits(result.DefenderKilledUnits,
            (MilitaryUnitsEnum.Garrison, 1, 1),
            (MilitaryUnitsEnum.AntiTank, 1, 1));
        Assert.Equal(800m, result.AttackerRemainingHp);
        Assert.Equal(35000m, result.DefenderRemainingHp);
    }

    // Przypadek biznesowy: jeśli jedna family przegrywa realny match, a druga dostaje
    // local win za brak matching defense, to przy remisie local winów wynik domyka remaining HP.
    [Fact]
    public void UnmatchedFamilyStillFallsBackToHp_WhenLocalWinsAreEqual()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 10),
                Unit(MilitaryUnitsEnum.Tanks, 1, 1)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 5),
                Unit(MilitaryUnitsEnum.AirDefense, 1, 100)
            ]);

        Assert.False(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(1, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Tanks, 1, 1));
        AssertUnits(result.AttackerKilledUnits, (MilitaryUnitsEnum.Infantry, 1, 10));
        AssertUnits(result.DefenderUnits,
            (MilitaryUnitsEnum.Garrison, 1, 3),
            (MilitaryUnitsEnum.AirDefense, 1, 100));
    }

    // Przypadek biznesowy: gdy liczba wygranych local matchów jest równa,
    // o wyniku całej bitwy powinno decydować pozostałe łączne HP.
    [Fact]
    public void MixedFamilies_OverallWinnerIsChosenByRemainingHp()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.Infantry, 1, 10),
                Unit(MilitaryUnitsEnum.Fighters, 1, 1)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 1),
                Unit(MilitaryUnitsEnum.AirDefense, 1, 2)
            ]);

        Assert.False(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(1, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.Infantry, 1, 5));
        AssertUnits(result.AttackerKilledUnits,
            (MilitaryUnitsEnum.Infantry, 1, 5),
            (MilitaryUnitsEnum.Fighters, 1, 1));
        AssertUnits(result.DefenderUnits, (MilitaryUnitsEnum.AirDefense, 1, 1));
        AssertUnits(result.DefenderKilledUnits,
            (MilitaryUnitsEnum.Garrison, 1, 1),
            (MilitaryUnitsEnum.AirDefense, 1, 1));
        Assert.Equal(50m, result.AttackerRemainingHp);
        Assert.Equal(350m, result.DefenderRemainingHp);
    }



    //Prawdziwy przypadek z gry
    [Fact]
    public void Real_Test_1()
    {
        var result = BattleSimulationEngine.SimulateBattle(
            attackerUnits: [
                Unit(MilitaryUnitsEnum.CyberForces, 1, 3)
            ],
            defenderUnits: [
                Unit(MilitaryUnitsEnum.Garrison, 1, 12),
                Unit(MilitaryUnitsEnum.Infantry, 1, 12)
            ]);

        Assert.True(result.AttackerWon);
        Assert.Equal(1, result.AttackerLocalWins);
        Assert.Equal(0, result.DefenderLocalWins);
        AssertUnits(result.AttackerUnits, (MilitaryUnitsEnum.CyberForces, 1, 3));
        Assert.Empty(result.AttackerKilledUnits);
        Assert.Empty(result.DefenderKilledUnits);

        AssertUnits(result.DefenderUnits,
            (MilitaryUnitsEnum.Garrison, 1, 12),
            (MilitaryUnitsEnum.Infantry, 1, 12));

    }

    private static BattleSimulationUnitInput Unit(MilitaryUnitsEnum unitCode, int level, int quantity)
        => new(unitCode.ToString(), level, quantity);


    private static void AssertUnits(
        IReadOnlyList<BattleSimulationUnitInput> actual,
        params (MilitaryUnitsEnum UnitCode, int Level, int Quantity)[] expected)
    {
        var actualOrdered = actual
            .OrderBy(unit => unit.UnitCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(unit => unit.Level)
            .ThenBy(unit => unit.Quantity)
            .ToList();
        var expectedOrdered = expected
            .OrderBy(unit => unit.UnitCode.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(unit => unit.Level)
            .ThenBy(unit => unit.Quantity)
            .ToList();

        Assert.Equal(expectedOrdered.Count, actualOrdered.Count);

        for (var i = 0; i < expectedOrdered.Count; i++)
        {
            Assert.Equal(expectedOrdered[i].UnitCode.ToString(), actualOrdered[i].UnitCode);
            Assert.Equal(expectedOrdered[i].Level, actualOrdered[i].Level);
            Assert.Equal(expectedOrdered[i].Quantity, actualOrdered[i].Quantity);
        }
    }
}
