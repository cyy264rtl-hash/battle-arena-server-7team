using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미로 생성기
/// 1) 왼쪽/오른쪽 미로를 각각 재귀 백트래킹으로 생성
/// 2) 가운데 강에 바위를 배치 (선착장 사이 뱃길이 반드시 존재하도록 BFS로 검사)
/// 3) 오른쪽 선착장에서 가장 먼 칸을 출구로 지정
/// </summary>
public static class MazeGenerator
{
    public static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public static MazeData Generate(System.Random rng)
    {
        var maze = new MazeData(GameConfig.Width, GameConfig.Height);
        int h          = GameConfig.Height;
        int riverStart = GameConfig.LeftWidth;
        int rightStart = riverStart + GameConfig.RiverWidth;

        CarveMaze(maze, 0, GameConfig.LeftWidth, rng);
        CarveMaze(maze, rightStart, GameConfig.RightWidth, rng);

        // 선착장은 서로 다른 높이에 두어 강을 가로질러 조작하게 만든다
        int lowRow  = RandomOddRow(rng, 1, h / 2);
        int highRow = RandomOddRow(rng, h / 2, h - 1);
        bool leftLow = rng.Next(2) == 0;

        maze.LeftDock  = new Vector2Int(riverStart - 1, leftLow ? lowRow : highRow);
        maze.RightDock = new Vector2Int(rightStart, leftLow ? highRow : lowRow);
        maze.Set(maze.LeftDock, TileType.Dock);
        maze.Set(maze.RightDock, TileType.Dock);

        PlaceRiver(maze, rng);

        maze.Exit = FarthestCell(maze, maze.RightDock, p => p.x >= rightStart && maze.IsWalkable(p));
        maze.Set(maze.Exit, TileType.Exit);
        return maze;
    }

    static int RandomOddRow(System.Random rng, int min, int max)
    {
        int row;
        do { row = rng.Next(min, max); } while (row % 2 == 0);
        return row;
    }

    /// <summary>x0부터 width 폭의 영역에 미로를 판다. 홀수 좌표가 방, 짝수 좌표가 벽.</summary>
    static void CarveMaze(MazeData maze, int x0, int width, System.Random rng)
    {
        int h = maze.Height;
        var visited   = new bool[width, h];
        var stack     = new Stack<Vector2Int>();
        var neighbors = new List<Vector2Int>(4);

        var start = new Vector2Int(1, 1);
        visited[start.x, start.y] = true;
        maze.Tiles[x0 + start.x, start.y] = TileType.Floor;
        stack.Push(start);

        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            neighbors.Clear();
            foreach (var d in Directions)
            {
                var n = cur + d * 2;
                if (n.x > 0 && n.x < width - 1 && n.y > 0 && n.y < h - 1 && !visited[n.x, n.y])
                    neighbors.Add(n);
            }

            if (neighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var next = neighbors[rng.Next(neighbors.Count)];
            int bx = (cur.x + next.x) / 2;
            int by = (cur.y + next.y) / 2;
            maze.Tiles[x0 + bx, by]         = TileType.Floor;
            maze.Tiles[x0 + next.x, next.y] = TileType.Floor;
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        // 막다른 길만 있으면 답답하므로 벽 일부를 뚫어 순환로를 만든다
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < h - 1; y++)
            {
                bool isPassageWall = (x % 2 == 1) != (y % 2 == 1);
                if (isPassageWall && maze.Tiles[x0 + x, y] == TileType.Wall
                    && rng.NextDouble() < GameConfig.ExtraPassageChance)
                {
                    maze.Tiles[x0 + x, y] = TileType.Floor;
                }
            }
        }
    }

    static void PlaceRiver(MazeData maze, System.Random rng)
    {
        int x0 = GameConfig.LeftWidth;
        int x1 = x0 + GameConfig.RiverWidth - 1;
        int h  = maze.Height;
        var from = new Vector2Int(x0, maze.LeftDock.y);
        var to   = new Vector2Int(x1, maze.RightDock.y);

        for (int attempt = 0; attempt < 100; attempt++)
        {
            FillRiver(maze, x0, x1);

            int rocks = rng.Next(GameConfig.MinRocks, GameConfig.MaxRocks + 1);
            for (int i = 0; i < rocks; i++)
            {
                var p = new Vector2Int(rng.Next(x0, x1 + 1), rng.Next(1, h - 1));
                if (p != from && p != to)
                    maze.Set(p, TileType.Rock);
            }

            var dist = Distances(maze, from, p => maze.Get(p) == TileType.Water);
            if (dist.ContainsKey(to))
                return;
        }

        FillRiver(maze, x0, x1);   // 실패하면 바위 없는 강
    }

    static void FillRiver(MazeData maze, int x0, int x1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = 0; y < maze.Height; y++)
                maze.Tiles[x, y] = (y == 0 || y == maze.Height - 1) ? TileType.Rock : TileType.Water;
    }

    /// <summary>start에서 passable 칸만 지나 도달 가능한 칸까지의 거리 (BFS)</summary>
    public static Dictionary<Vector2Int, int> Distances(MazeData maze, Vector2Int start, Func<Vector2Int, bool> passable)
    {
        var dist  = new Dictionary<Vector2Int, int> { [start] = 0 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var d in Directions)
            {
                var n = cur + d;
                if (!maze.InBounds(n) || dist.ContainsKey(n) || !passable(n))
                    continue;
                dist[n] = dist[cur] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    static Vector2Int FarthestCell(MazeData maze, Vector2Int start, Func<Vector2Int, bool> passable)
    {
        var best = start;
        int bestDist = -1;
        foreach (var kv in Distances(maze, start, passable))
        {
            if (kv.Value > bestDist && maze.Get(kv.Key) == TileType.Floor)
            {
                best = kv.Key;
                bestDist = kv.Value;
            }
        }
        return best;
    }
}
