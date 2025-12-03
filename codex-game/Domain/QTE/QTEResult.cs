namespace CodexGame.Domain.QTE;

public readonly record struct QTEResult(QTEScore Score, float Multiplier, bool IsCritical);

public enum QTEScore
{
    Miss,
    Good,
    Great,
    Critical
}
