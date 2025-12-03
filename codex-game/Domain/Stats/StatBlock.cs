using System.Collections.Generic;

namespace CodexGame.Domain.Stats;

/// <summary>
/// Immutable snapshot of stats used throughout combat calculations.
/// </summary>
public record StatBlock(
    int MaxHP,
    int MaxMP,
    int PhysicalAttack,
    int SpecialAttack,
    int PhysicalDefense,
    int SpecialDefense,
    int Speed)
{
    public IReadOnlyDictionary<StatType, int> AsDictionary() =>
        new Dictionary<StatType, int>
        {
            { StatType.MaxHP, MaxHP },
            { StatType.MaxMP, MaxMP },
            { StatType.PhysicalAttack, PhysicalAttack },
            { StatType.SpecialAttack, SpecialAttack },
            { StatType.PhysicalDefense, PhysicalDefense },
            { StatType.SpecialDefense, SpecialDefense },
            { StatType.Speed, Speed }
        };
}
