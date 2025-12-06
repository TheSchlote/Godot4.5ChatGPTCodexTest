using Godot;

namespace CodexGame.Infrastructure.Controllers;

public interface ICursor
{
    Vector3 GetSelectedTile();
    void SetOccupied(bool occupied);
}
