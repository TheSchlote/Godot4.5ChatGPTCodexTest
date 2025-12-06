using CodexGame.Infrastructure.Controllers;
using CodexGame.Infrastructure.Scenes;
using Godot;

namespace CodexGame.Infrastructure.Adapters;

public sealed class CursorAdapter : ICursor
{
    private readonly SelectionCursor _cursor;

    public CursorAdapter(SelectionCursor cursor)
    {
        _cursor = cursor;
    }

    public Vector3 GetSelectedTile() => _cursor.GetSelectedTile();

    public void SetOccupied(bool occupied) => _cursor.SetOccupied(occupied);
}
