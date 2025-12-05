using System.Collections.Generic;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;
using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Houses demo data used to stand up the scene without bespoke authoring tools.
/// </summary>
public static class DemoContent
{
    public sealed record DemoUnitConfig(UnitBlueprint Blueprint, Vector3 Position, Color Color);

    public static IReadOnlyList<DemoUnitConfig> GetUnits() => new[]
    {
        new DemoUnitConfig(
            Blueprint: new UnitBlueprint(
                id: "Player",
                name: "Pyreblade",
                affinity: Element.Fire,
                baseStats: new StatBlock(30, 10, 15, 5, 8, 6, 12),
                moveRange: 4,
                abilities: new[] { "basic_attack" },
                defaultQTE: new QTEProfile(QTEType.TimingBar, difficulty: 1f, critWindow: 0.15f)),
            Position: new Vector3(2, 0, 2),
            Color: new Color(0.2f, 0.7f, 1f)),
        new DemoUnitConfig(
            Blueprint: new UnitBlueprint(
                id: "Enemy",
                name: "Tideguard",
                affinity: Element.Water,
                baseStats: new StatBlock(25, 8, 10, 4, 7, 5, 10),
                moveRange: 3,
                abilities: new[] { "basic_attack" },
                defaultQTE: new QTEProfile(QTEType.TimingBar, difficulty: 1.1f, critWindow: 0.1f)),
            Position: new Vector3(8, 0, 6),
            Color: new Color(1f, 0.4f, 0.2f))
    };
}
