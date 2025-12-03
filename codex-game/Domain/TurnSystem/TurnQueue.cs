using System.Collections.Generic;
using System.Linq;

namespace CodexGame.Domain.TurnSystem;

/// <summary>
/// Pure deterministic turn order calculator; ignores real-time.
/// </summary>
public sealed class TurnQueue
{
    private readonly Dictionary<string, TurnMeter> _meters = new();

    public void Register(TurnMeter meter) => _meters[meter.UnitId] = meter;

    public IReadOnlyCollection<TurnMeter> Meters => _meters.Values;

    /// <summary>
    /// Deterministic advance without reference to real time. Call once per logical step.
    /// </summary>
    public string? AdvanceUntilReady()
    {
        foreach (var meter in _meters.Values)
            meter.AdvanceStep();

        var ready = _meters.Values
            .Where(m => m.IsReady)
            .OrderByDescending(m => m.TurnValue)
            .FirstOrDefault();

        return ready?.UnitId;
    }

    public void Consume(string unitId)
    {
        if (_meters.TryGetValue(unitId, out var meter))
            meter.ConsumeTurn();
    }

    public IReadOnlyList<TurnOrderEntry> GetOrderSnapshot()
    {
        return _meters.Values
            .OrderByDescending(m => m.TurnValue)
            .Select(m => new TurnOrderEntry(m.UnitId, m.TurnValue, m.IsReady))
            .ToList();
    }

    /// <summary>
    /// Advances meters mathematically to the next unit ready state without iterating many steps.
    /// Returns the unitId that became ready.
    /// </summary>
    public string? AdvanceToNextReady()
    {
        var existingReady = _meters.Values
            .Where(m => m.IsReady)
            .OrderByDescending(m => m.TurnValue)
            .FirstOrDefault();
        if (existingReady != null)
            return existingReady.UnitId;

        var minSteps = float.MaxValue;
        foreach (var meter in _meters.Values)
        {
            var remaining = meter.Threshold - meter.TurnValue;
            var stepSize = meter.Speed * meter.TurnRateConstant;
            if (stepSize <= 0) continue;
            var steps = remaining / stepSize;
            if (steps > 0 && steps < minSteps)
                minSteps = steps;
        }

        if (minSteps == float.MaxValue)
            return null;

        var stepCount = (int)MathF.Ceiling(minSteps);
        foreach (var meter in _meters.Values)
            meter.AdvanceSteps(stepCount);

        var ready = _meters.Values
            .Where(m => m.IsReady)
            .OrderByDescending(m => m.TurnValue)
            .FirstOrDefault();
        return ready?.UnitId;
    }
}
