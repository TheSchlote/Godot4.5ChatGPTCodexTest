using CodexGame.Domain.QTE;

namespace CodexGame.Domain.Abilities;

public sealed record AbilityResult(int Damage, bool ConsumedTurn, QTEResult QTEResult, bool AppliedStatus = false);
