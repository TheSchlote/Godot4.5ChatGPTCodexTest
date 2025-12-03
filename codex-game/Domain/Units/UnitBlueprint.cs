using System.Collections.Generic;
using CodexGame.Domain.Abilities;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;

namespace CodexGame.Domain.Units;

public sealed class UnitBlueprint
{
    public UnitBlueprint(
        string id,
        string name,
        Element affinity,
        StatBlock baseStats,
        int moveRange,
        IEnumerable<string> abilities,
        QTEProfile defaultQTE,
        string? aiProfileId = null)
    {
        Id = id;
        Name = name;
        Affinity = affinity;
        BaseStats = baseStats;
        MoveRange = moveRange;
        Abilities = new List<string>(abilities);
        DefaultQTE = defaultQTE;
        AIProfileId = aiProfileId;
    }

    public string Id { get; }
    public string Name { get; }
    public Element Affinity { get; }
    public StatBlock BaseStats { get; }
    public int MoveRange { get; }
    public IReadOnlyList<string> Abilities { get; }
    public QTEProfile DefaultQTE { get; }
    public string? AIProfileId { get; }
}
