using System;
using CodexGame.Domain.Maps;
using Godot;
using FileAccess = Godot.FileAccess;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Loads serialized map data from disk/resources into domain MapData.
/// </summary>
public sealed class MapLoader
{
    public bool TryLoad(string resourcePath, out MapData mapData)
    {
        mapData = null!;
        try
        {
            if (!FileAccess.FileExists(resourcePath))
            {
                GD.Print($"Map not found at {resourcePath}");
                return false;
            }

            using var file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            mapData = MapSerializer.FromJson(json);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to load map {resourcePath}: {ex.Message}");
            return false;
        }
    }
}
