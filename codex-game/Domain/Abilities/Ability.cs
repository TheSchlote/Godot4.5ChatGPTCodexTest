using System;
using System.Collections.Generic;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Units;

namespace CodexGame.Domain.Abilities;

public sealed class Ability
{
    public Ability(
        string id,
        string name,
        int mpCost,
        RangePattern range,
        AreaPattern aoe,
        Element element,
        QTEType qte,
        Func<AbilityContext, AbilityResult> damageFormula,
        IEnumerable<AbilityTag>? tags = null)
    {
        Id = id;
        Name = name;
        MPCost = mpCost;
        Range = range;
        AoE = aoe;
        ElementType = element;
        QTE = qte;
        DamageFormula = damageFormula;
        Tags = new List<AbilityTag>(tags ?? Array.Empty<AbilityTag>());
    }

    public string Id { get; }
    public string Name { get; }
    public int MPCost { get; }
    public RangePattern Range { get; }
    public AreaPattern AoE { get; }
    public Element ElementType { get; }
    public QTEType QTE { get; }
    public Func<AbilityContext, AbilityResult> DamageFormula { get; }
    public IReadOnlyList<AbilityTag> Tags { get; }
}
