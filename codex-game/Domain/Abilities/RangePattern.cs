namespace CodexGame.Domain.Abilities;

public enum RangeShape
{
    Single,
    Diamond,
    Line,
    CustomMask
}

public sealed record RangePattern(RangeShape Shape, int Min, int Max, int[]? CustomMask = null);
