using System.Collections.Generic;
using System.Linq;
using CodexGame.Domain.Maps;
using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Responsible for constructing simple tile-based maps for prototyping scenes.
/// </summary>
public sealed class MapBuilder
{
    public sealed record MapBuildResult(Vector3 Center, IReadOnlyList<MeshInstance3D> Tiles);

    public MapBuildResult BuildFlatGrid(Node parent, Vector2I mapSize, float tileSize, float tileHeight = 0.2f)
    {
        var tiles = new List<MeshInstance3D>(mapSize.X * mapSize.Y);
        var tileMesh = new BoxMesh
        {
            Size = new Vector3(tileSize, tileHeight, tileSize)
        };
        var material = GetTileMaterial("default");

        for (var x = 0; x < mapSize.X; x++)
        {
            for (var z = 0; z < mapSize.Y; z++)
            {
                var tile = CreateTile(tileMesh, x, z, tileSize, tileHeight, Vector3.Zero, material);
                parent.AddChild(tile);
                tiles.Add(tile);
            }
        }

        var center = GetCenter(mapSize, tileSize);
        return new MapBuildResult(center, tiles);
    }

    public MapBuildResult BuildFromMapData(Node parent, MapData map, float tileSize, float baseTileHeight = 0.2f)
    {
        var tiles = new List<MeshInstance3D>(map.Width * map.Height);
        var tileMesh = new BoxMesh
        {
            Size = new Vector3(tileSize, baseTileHeight, tileSize)
        };

        var cells = map.Cells.ToDictionary(c => (c.X, c.Y));
        var materialCache = new Dictionary<string, StandardMaterial3D>();
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                cells.TryGetValue((x, y), out var cell);
                var elevation = cell?.Elevation ?? 0;
                var type = cell?.Type ?? "default";
                var material = GetOrCreateMaterial(materialCache, type);
                var tile = CreateTile(tileMesh, x, y, tileSize, baseTileHeight, new Vector3(0, elevation * baseTileHeight, 0), material);
                parent.AddChild(tile);
                tiles.Add(tile);
            }
        }

        var center = GetCenter(new Vector2I(map.Width, map.Height), tileSize);
        return new MapBuildResult(center, tiles);
    }

    public Vector3 CellToWorld(Vector2I cell, float tileSize, float tileHeight, int elevation = 0) =>
        new(cell.X * tileSize, elevation * tileHeight, cell.Y * tileSize);

    private static MeshInstance3D CreateTile(BoxMesh mesh, int x, int z, float tileSize, float tileHeight, Vector3 offset, Material material) =>
        new()
        {
            Mesh = mesh,
            MaterialOverride = material,
            Position = new Vector3(x * tileSize, -tileHeight * 0.5f, z * tileSize) + offset,
            Name = $"Tile_{x}_{z}"
        };

    private static Vector3 GetCenter(Vector2I mapSize, float tileSize) =>
        new(MapSizeComponent(mapSize.X) * tileSize, 0, MapSizeComponent(mapSize.Y) * tileSize);

    private static float MapSizeComponent(int size) => (size - 1) * 0.5f;

    private static StandardMaterial3D GetOrCreateMaterial(Dictionary<string, StandardMaterial3D> cache, string type)
    {
        if (cache.TryGetValue(type, out var existing))
            return existing;

        var material = GetTileMaterial(type);
        cache[type] = material;
        return material;
    }

    private static StandardMaterial3D GetTileMaterial(string type)
    {
        var color = type.ToLower() switch
        {
            "grass" => new Color(0.35f, 0.8f, 0.45f),
            "water" => new Color(0.25f, 0.45f, 0.95f),
            "stone" => new Color(0.6f, 0.6f, 0.6f),
            "sand" => new Color(0.9f, 0.8f, 0.55f),
            _ => new Color(0.7f, 0.75f, 0.8f)
        };

        return new StandardMaterial3D
        {
            AlbedoColor = color
        };
    }
}
