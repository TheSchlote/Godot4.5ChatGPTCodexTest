namespace CodexGame.Domain.TurnSystem;

public readonly record struct TurnOrderEntry(string UnitId, float TurnValue, bool IsReady);
