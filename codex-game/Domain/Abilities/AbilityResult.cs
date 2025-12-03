namespace CodexGame.Domain.Abilities;

public sealed record AbilityResult(int Damage, bool ConsumedTurn, bool AppliedStatus = false);
