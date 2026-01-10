using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;
using Godot;
using FileAccess = Godot.FileAccess;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Loads unit blueprints and presentation data from JSON for scene setup.
/// </summary>
public sealed class UnitContentLoader
{
    public sealed record UnitDefinition(UnitBlueprint Blueprint, Color Color, int Team, Vector2I SpawnCell);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool TryLoad(string resourcePath, out IReadOnlyList<UnitDefinition> units)
    {
        units = Array.Empty<UnitDefinition>();
        try
        {
            if (!FileAccess.FileExists(resourcePath))
            {
                GD.Print($"Unit config not found at {resourcePath}");
                return false;
            }

            using var file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            var dto = JsonSerializer.Deserialize<UnitFileDto>(json, Options);
            if (dto?.Units is null || dto.Units.Count == 0)
                return false;

            units = dto.Units.Select(ToUnitDefinition).ToList();
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to load units from {resourcePath}: {ex.Message}");
            return false;
        }
    }

    private static UnitDefinition ToUnitDefinition(UnitDto dto)
    {
        var blueprint = new UnitBlueprint(
            dto.Id,
            dto.Name,
            ParseEnum(dto.Affinity, Element.Neutral),
            new StatBlock(dto.Stats.MaxHP, dto.Stats.MaxMP, dto.Stats.PhysicalAttack, dto.Stats.SpecialAttack, dto.Stats.PhysicalDefense, dto.Stats.SpecialDefense, dto.Stats.Speed),
            dto.MoveRange,
            dto.Abilities ?? Array.Empty<string>(),
            new QTEProfile(ParseEnum(dto.Qte.Type, QTEType.None), dto.Qte.Difficulty, dto.Qte.CritWindow, dto.Qte.DurationSeconds),
            dto.AiProfileId);

        var color = new Color(dto.Color.R, dto.Color.G, dto.Color.B, dto.Color.A ?? 1f);
        var spawn = dto.Spawn ?? new SpawnDto();
        return new UnitDefinition(blueprint, color, dto.Team, new Vector2I(spawn.X, spawn.Y));
    }

    private static T ParseEnum<T>(string value, T defaultValue) where T : struct, Enum =>
        Enum.TryParse(value, true, out T parsed) ? parsed : defaultValue;

    private sealed class UnitFileDto
    {
        public List<UnitDto> Units { get; init; } = new();
    }

    private sealed class UnitDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Affinity { get; init; } = nameof(Element.Neutral);
        public StatDto Stats { get; init; } = new();
        public int MoveRange { get; init; }
        public string[] Abilities { get; init; } = Array.Empty<string>();
        public QteDto Qte { get; init; } = new();
        public ColorDto Color { get; init; } = new();
        public int Team { get; init; }
        public SpawnDto? Spawn { get; init; }
        public string? AiProfileId { get; init; }
    }

    private sealed class StatDto
    {
        public int MaxHP { get; init; }
        public int MaxMP { get; init; }
        public int PhysicalAttack { get; init; }
        public int SpecialAttack { get; init; }
        public int PhysicalDefense { get; init; }
        public int SpecialDefense { get; init; }
        public int Speed { get; init; }
    }

    private sealed class QteDto
    {
        public string Type { get; init; } = nameof(QTEType.None);
        public float Difficulty { get; init; } = 1f;
        public float CritWindow { get; init; } = 0.1f;
        public float DurationSeconds { get; init; } = 1.5f;
    }

    private sealed class ColorDto
    {
        public float R { get; init; }
        public float G { get; init; }
        public float B { get; init; }
        public float? A { get; init; }
    }

    private sealed class SpawnDto
    {
        public int X { get; init; }
        public int Y { get; init; }
    }
}
