namespace CodexGame.Domain.Abilities;

public enum AreaShape
{
    Single,
    Diamond,
    Line,
    Cross,
    CustomMask
}

public sealed record AreaPattern(AreaShape Shape, int Size, int[]? CustomMask = null);
