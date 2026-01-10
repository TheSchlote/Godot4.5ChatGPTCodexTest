using CodexGame.Application.Battle;
using CodexGame.Domain.Units;
using CodexGame.Infrastructure.Pathfinding;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Owns unit scene nodes, team data, health bars, and ability lists.
/// Keeps Godot-facing state in one place.
/// </summary>
public sealed class UnitPresenter
{
    private readonly Node _unitsRoot;
    private readonly BattleManager _battleManager;
    private readonly Dictionary<string, Node3D> _unitNodes = new();
    private readonly Dictionary<string, int> _unitTeams = new();
    private readonly Dictionary<string, List<string>> _unitAbilities = new();
    private readonly Dictionary<string, Label3D> _healthBars = new();
    private readonly Dictionary<string, Vector3> _unitFacing = new();
    private readonly Dictionary<string, string> _unitNames = new();
    private readonly Dictionary<string, Color> _unitColors = new();
    private readonly Dictionary<string, string?> _unitAiProfiles = new();
    private readonly Dictionary<int, bool> _teamAiControl = new();

    public UnitPresenter(Node unitsRoot, BattleManager battleManager)
    {
        _unitsRoot = unitsRoot;
        _battleManager = battleManager;
    }

    public Node3D CreateUnitNode(string id, Color color, Vector3 position)
    {
        var node = new Node3D { Name = id, Position = position };
        node.AddChild(BuildCapsuleMesh(color));
        node.AddChild(BuildFacingIndicator());
        _unitsRoot.AddChild(node);
        return node;
    }

    public void RegisterUnit(UnitState state, Node3D node, int team, IEnumerable<string> abilities, string displayName, Color color, string? aiProfileId = null)
    {
        _unitNodes[state.Id] = node;
        _unitTeams[state.Id] = team;
        _unitAbilities[state.Id] = abilities.ToList();
        _unitFacing[state.Id] = Vector3.Forward;
        _unitNames[state.Id] = displayName;
        _unitColors[state.Id] = color;
        _unitAiProfiles[state.Id] = aiProfileId;
        if (!_teamAiControl.ContainsKey(team))
            _teamAiControl[team] = team > 1;
        AttachHealthBar(node, state);
        _battleManager.RegisterUnit(node, state);
    }

    public bool TryGetTeam(string unitId, out int team) => _unitTeams.TryGetValue(unitId, out team);

    public bool IsAiControlled(string unitId)
    {
        if (_unitTeams.TryGetValue(unitId, out var team))
            return IsAiTeam(team);
        return false;
    }

    public bool IsAiTeam(int team)
    {
        if (_teamAiControl.TryGetValue(team, out var isAi))
            return isAi;
        return team > 1;
    }

    public IReadOnlyList<string> GetAbilities(string unitId) =>
        _unitAbilities.TryGetValue(unitId, out var abilities) ? abilities : new List<string>();

    public IEnumerable<string> GetUnitsByTeam(int team)
    {
        foreach (var kvp in _unitTeams)
        {
            if (kvp.Value == team)
                yield return kvp.Key;
        }
    }

    public void SetFacing(string unitId, Vector3 facing)
    {
        var normalized = facing.Normalized();
        if (normalized == Vector3.Zero) return;
        _unitFacing[unitId] = normalized;
        if (_unitNodes.TryGetValue(unitId, out var node) && node is not null)
        {
            node.LookAt(node.GlobalPosition + normalized, Vector3.Up);
        }
    }

    public Vector3 GetFacing(string unitId)
    {
        if (_unitFacing.TryGetValue(unitId, out var forward) && forward != Vector3.Zero)
            return forward;
        return Vector3.Forward;
    }

    public string GetDisplayName(string unitId)
    {
        if (_unitNames.TryGetValue(unitId, out var name))
            return name;
        return unitId;
    }

    public Color GetColor(string unitId)
    {
        if (_unitColors.TryGetValue(unitId, out var color))
            return color;
        return Colors.White;
    }

    public string? GetAiProfileId(string unitId) =>
        _unitAiProfiles.TryGetValue(unitId, out var profile) ? profile : null;

    public Node3D? GetNode(string unitId) =>
        _unitNodes.TryGetValue(unitId, out var node) ? node : null;

    public Node3D? GetNodeAtPosition(IPathfinder pathfinding, Vector3 position, string? excludeId = null)
    {
        var cell = pathfinding.WorldToCell(position);
        foreach (var kvp in _unitNodes)
        {
            if (excludeId != null && kvp.Key == excludeId) continue;
            if (kvp.Value is null) continue;
            var nodeCell = pathfinding.WorldToCell(kvp.Value.GlobalPosition);
            if (nodeCell.X == cell.X && nodeCell.Z == cell.Z)
                return kvp.Value;
        }

        return null;
    }

    public bool IsCellOccupied(IPathfinder pathfinding, Vector3I cell, string? ignoreId = null)
    {
        foreach (var kvp in _unitNodes)
        {
            if (ignoreId != null && kvp.Key == ignoreId) continue;
            var node = kvp.Value;
            if (node is null) continue;
            var nodeCell = pathfinding.WorldToCell(node.GlobalPosition);
            if (nodeCell == cell)
                return true;
        }

        return false;
    }

    public void UpdateHealthBar(string unitId)
    {
        if (!_healthBars.TryGetValue(unitId, out var label)) return;
        if (!_battleManager.TryGetUnit(unitId, out var state) || state is null) return;

        label.Text = $"{state.CurrentHP}/{state.Stats.MaxHP} | MP {state.CurrentMP}/{state.Stats.MaxMP}";
    }

    public void UpdateHealthBarFacing(Camera3D? camera)
    {
        if (camera is null) return;
        var camPos = camera.GlobalTransform.Origin;
        foreach (var label in _healthBars.Values)
        {
            if (!GodotObject.IsInstanceValid(label)) continue;
            label.LookAt(camPos, Vector3.Up);
            label.RotateObjectLocal(Vector3.Up, Mathf.Pi);
        }
    }

    public void RemoveUnit(string unitId)
    {
        if (_unitNodes.TryGetValue(unitId, out var node))
            node?.QueueFree();

        if (_healthBars.TryGetValue(unitId, out var label) && GodotObject.IsInstanceValid(label))
        {
            label.QueueFree();
            _healthBars.Remove(unitId);
        }

        _unitTeams.Remove(unitId);
        _unitNodes.Remove(unitId);
        _unitAbilities.Remove(unitId);
        _unitAiProfiles.Remove(unitId);
        _battleManager.RemoveUnit(unitId);
    }

    public IEnumerable<int> GetAliveTeams() => _unitTeams.Values.Distinct();

    public void SetTeamAiControl(int team, bool isAiControlled) => _teamAiControl[team] = isAiControlled;

    public IEnumerable<string> GetAllUnitIds() => _unitNodes.Keys;

    private static MeshInstance3D BuildCapsuleMesh(Color color)
    {
        var mesh = new CapsuleMesh
        {
            Radius = 0.5f,
            Height = 1.6f
        };

        var material = new StandardMaterial3D
        {
            AlbedoColor = color
        };

        return new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            Position = new Vector3(0, 0.8f, 0)
        };
    }

    private void AttachHealthBar(Node3D node, UnitState state)
    {
        var label = new Label3D
        {
            Name = $"{state.Id}_Health",
            Text = $"{state.CurrentHP}/{state.Stats.MaxHP} | MP {state.CurrentMP}/{state.Stats.MaxMP}",
            Position = new Vector3(0, 2.2f, 0),
            PixelSize = 0.01f
        };
        _healthBars[state.Id] = label;
        node.AddChild(label);
    }

    private static MeshInstance3D BuildFacingIndicator()
    {
        var mesh = new CylinderMesh
        {
            BottomRadius = 0.14f,
            TopRadius = 0.04f,
            Height = 0.35f
        };

        return new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.2f, 0.2f) },
            Position = new Vector3(0, 1.1f, 0.35f),
            RotationDegrees = new Vector3(90, 0, 0)
        };
    }
}
