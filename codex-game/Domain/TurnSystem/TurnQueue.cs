using System;
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

    public void Remove(string unitId)
    {
        if (_meters.ContainsKey(unitId))
            _meters.Remove(unitId);
    }

    public IReadOnlyList<TurnOrderEntry> GetOrderSnapshot()
    {
        return _meters.Values
            .OrderByDescending(m => m.TurnValue)
            .Select(m => new TurnOrderEntry(m.UnitId, m.TurnValue, m.IsReady))
            .ToList();
    }

    public IReadOnlyList<TurnOrderEntry> PredictOrder(int count)
    {
        var states = _meters.Values.Select(m => m.Snapshot()).ToList();
        var order = new List<TurnOrderEntry>(count);

        while (order.Count < count && states.Count > 0)
        {
            var ready = states
                .Where(s => s.TurnValue >= s.Threshold)
                .OrderByDescending(s => s.TurnValue)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(ready.UnitId))
            {
                order.Add(new TurnOrderEntry(ready.UnitId, ready.TurnValue, true));
                var idx = states.FindIndex(s => s.UnitId == ready.UnitId);
                if (idx >= 0) states[idx] = states[idx] with { TurnValue = 0 };
                continue;
            }

            var minSteps = float.MaxValue;
            foreach (var state in states)
            {
                var remaining = state.Threshold - state.TurnValue;
                var step = state.Speed * state.TurnRateConstant;
                if (step <= 0) continue;
                var steps = remaining / step;
                if (steps > 0 && steps < minSteps)
                    minSteps = steps;
            }

            if (minSteps == float.MaxValue)
                break;

            var stepCount = (int)MathF.Ceiling(minSteps);
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                var advance = state.Speed * state.TurnRateConstant * stepCount;
                states[i] = state with { TurnValue = state.TurnValue + advance };
            }
        }

        return order;
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
