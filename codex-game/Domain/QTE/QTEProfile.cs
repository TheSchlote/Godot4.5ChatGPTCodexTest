namespace CodexGame.Domain.QTE;

/// <summary>
/// Data that defines how a QTE behaves, independent of UI implementation.
/// </summary>
public sealed class QTEProfile
{
    public QTEProfile(QTEType type, float difficulty = 1f, float critWindow = 0.1f)
    {
        Type = type;
        Difficulty = difficulty;
        CritWindow = critWindow;
    }

    public QTEType Type { get; }
    public float Difficulty { get; }
    public float CritWindow { get; }
}
