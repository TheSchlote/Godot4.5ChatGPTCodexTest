namespace CodexGame.Domain.QTE;

/// <summary>
/// Data that defines how a QTE behaves, independent of UI implementation.
/// </summary>
public sealed class QTEProfile
{
    public QTEProfile(QTEType type, float difficulty = 1f, float critWindow = 0.1f, float durationSeconds = 1.5f)
    {
        Type = type;
        Difficulty = difficulty <= 0 ? 1f : difficulty;
        CritWindow = critWindow;
        DurationSeconds = durationSeconds;
    }

    public QTEType Type { get; }
    public float Difficulty { get; }
    public float CritWindow { get; }
    public float DurationSeconds { get; }
}
