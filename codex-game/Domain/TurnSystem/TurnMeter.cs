namespace CodexGame.Domain.TurnSystem;

public sealed class TurnMeter
{
    public TurnMeter(string unitId, int speed, int threshold = 1000, float turnRateConstant = 1f)
    {
        UnitId = unitId;
        Speed = speed;
        Threshold = threshold;
        TurnRateConstant = turnRateConstant;
        TurnValue = 0;
    }

    public string UnitId { get; }
    public int Speed { get; }
    public int Threshold { get; }
    public float TurnRateConstant { get; }
    public float TurnValue { get; private set; }

    public void AdvanceStep() => TurnValue += Speed * TurnRateConstant;
    public void AdvanceSteps(int steps) => TurnValue += Speed * TurnRateConstant * steps;

    public bool IsReady => TurnValue >= Threshold;

    public void ConsumeTurn() => TurnValue = 0;

    public TurnMeterState Snapshot() => new(UnitId, TurnValue, Speed, Threshold, TurnRateConstant);
}

public readonly record struct TurnMeterState(string UnitId, float TurnValue, int Speed, int Threshold, float TurnRateConstant);
