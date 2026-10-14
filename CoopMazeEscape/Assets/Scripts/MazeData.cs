using UnityEngine;

public enum TileType { Wall, Floor, Water, Rock, Dock, Exit }

/// <summary>
/// 미로 타일 정보 (서버로 옮겨도 그대로 쓸 수 있도록 Unity 오브젝트와 분리)
/// </summary>
public class MazeData
{
    public readonly int Width;
    public readonly int Height;
    public readonly TileType[,] Tiles;

    public Vector2Int LeftDock;
    public Vector2Int RightDock;
    public Vector2Int Exit;

    public MazeData(int width, int height)
    {
        Width  = width;
        Height = height;
        Tiles  = new TileType[width, height];
    }

    public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;

    public TileType Get(Vector2Int p) => InBounds(p) ? Tiles[p.x, p.y] : TileType.Wall;

    public void Set(Vector2Int p, TileType type) => Tiles[p.x, p.y] = type;

    /// <summary>플레이어가 걸어서 갈 수 있는 타일인가</summary>
    public bool IsWalkable(Vector2Int p)
    {
        var t = Get(p);
        return t == TileType.Floor || t == TileType.Dock || t == TileType.Exit;
    }

    public bool IsLeftSide(Vector2Int p) => p.x < GameConfig.LeftWidth;
}
