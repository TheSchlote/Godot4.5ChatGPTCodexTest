using System;
using CodexGame.Domain.Stats;

namespace CodexGame.Domain.Units;

/// <summary>
/// Runtime mutable state for a unit inside the deterministic simulator.
/// </summary>
public sealed class UnitState
{
    public UnitState(string id, StatBlock stats, Element affinity, int moveRange)
    {
        Id = id;
        Stats = stats;
        Affinity = affinity;
        MoveRange = moveRange;
        CurrentHP = stats.MaxHP;
        CurrentMP = stats.MaxMP;
    }

    public string Id { get; }
    public Element Affinity { get; }
    public StatBlock Stats { get; }
    public int MoveRange { get; }

    public int CurrentHP { get; private set; }
    public int CurrentMP { get; private set; }

    public bool IsAlive => CurrentHP > 0;

    public void ApplyDamage(int amount)
    {
        CurrentHP = Math.Max(0, CurrentHP - amount);
    }

    public void SpendMP(int amount)
    {
        if (amount < 0) return;
        CurrentMP = Math.Max(0, CurrentMP - amount);
    }
}
