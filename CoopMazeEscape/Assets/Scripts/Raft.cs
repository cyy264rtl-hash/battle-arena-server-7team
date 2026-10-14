using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 협동 장치: 뗏목
/// - 전원이 선착장의 뗏목 칸에 올라타면 탑승 → W/A/S/D 방향을 인원별로 나눠 배정
/// - 각자 자기 담당 방향 키만 눌러 뗏목을 움직인다 (누를 때마다 1칸)
/// - 바위/벽에 부딪히면 출발 선착장으로 리셋, 반대편 선착장에 닿으면 성공
/// - 선착장에 있을 때 육지 쪽으로 방향키를 누르면 그 사람은 내린다 (장치 정지)
/// </summary>
public class Raft : MonoBehaviour
{
    // 인원수별 방향 묶음 (README의 키 배분 규칙)
    static readonly Vector2Int[][] TwoPlayerGroups =
    {
        new[] { Vector2Int.up, Vector2Int.down },
        new[] { Vector2Int.left, Vector2Int.right },
    };
    static readonly Vector2Int[][] ThreePlayerGroups =
    {
        new[] { Vector2Int.up, Vector2Int.down },
        new[] { Vector2Int.left },
        new[] { Vector2Int.right },
    };
    static readonly Vector2Int[][] FourPlayerGroups =
    {
        new[] { Vector2Int.up }, new[] { Vector2Int.left },
        new[] { Vector2Int.down }, new[] { Vector2Int.right },
    };

    public Vector2Int Cell     { get; private set; }
    public bool       IsActive { get; private set; }
    public readonly Dictionary<PlayerController, Vector2Int[]> Assignments = new Dictionary<PlayerController, Vector2Int[]>();

    GameManager gm;
    Vector2Int  departureDock;
    int         boardCount;       // 탑승할 때마다 담당을 순환시키기 위한 카운트
    Vector3     visualTarget;

    public bool AtDock => gm.Maze.Get(Cell) == TileType.Dock;

    public static Raft Create(GameManager gm, Transform parent, Vector2Int dock)
    {
        var sr = SpriteFactory.Create("Raft", parent, SpriteFactory.Square, GameConfig.RaftColor,
                                      gm.CellToWorld(dock), GameConfig.OrderRaft, 0.86f);
        var raft = sr.gameObject.AddComponent<Raft>();
        raft.gm = gm;
        raft.Cell = dock;
        raft.departureDock = dock;
        raft.visualTarget = sr.transform.position;
        return raft;
    }

    public void Board(List<PlayerController> players)
    {
        IsActive = true;
        departureDock = Cell;
        Assignments.Clear();

        var groups = players.Count switch
        {
            2 => TwoPlayerGroups,
            3 => ThreePlayerGroups,
            _ => FourPlayerGroups,
        };
        for (int i = 0; i < players.Count; i++)
            Assignments[players[(i + boardCount) % players.Count]] = groups[i % groups.Length];
        boardCount++;

        gm.ShowMessage("뗏목 탑승! 각자 배정된 방향 키만 눌러 반대편 선착장까지 이동하세요.");
    }

    /// <summary>뗏목 조작 모드에서 매 프레임 호출</summary>
    public void Tick()
    {
        // 선착장에서는 육지 쪽 방향키로 내릴 수 있다
        if (AtDock)
        {
            foreach (var p in gm.Players)
            {
                foreach (var dir in MazeGenerator.Directions)
                {
                    var target = Cell + dir;
                    if (Input.GetKeyDown(p.KeyFor(dir)) && gm.Maze.IsWalkable(target))
                    {
                        StepOff(p, target);
                        return;
                    }
                }
            }
        }

        foreach (var kv in Assignments)
        {
            foreach (var dir in kv.Value)
            {
                if (Input.GetKeyDown(kv.Key.KeyFor(dir)))
                {
                    Move(dir);
                    return;
                }
            }
        }
    }

    void Move(Vector2Int dir)
    {
        var target = Cell + dir;
        switch (gm.Maze.Get(target))
        {
            case TileType.Water:
                SetCell(target, false);
                break;

            case TileType.Dock:
                SetCell(target, false);
                IsActive = false;
                Assignments.Clear();
                gm.ShowMessage("협동 성공! 뗏목이 선착장에 도착했습니다.");
                break;

            default:
                // 선착장에 정박 중일 때는 육지/벽 방향 입력을 무시, 강 위에서는 충돌 = 실패
                if (!AtDock)
                {
                    SetCell(departureDock, true);
                    gm.ShowMessage("쾅! 뗏목이 부딪혀 출발 선착장으로 돌아갔습니다.");
                }
                break;
        }
    }

    void SetCell(Vector2Int cell, bool snap)
    {
        Cell = cell;
        visualTarget = gm.CellToWorld(cell);
        if (snap) transform.position = visualTarget;
        foreach (var p in gm.Players)
            p.SetCell(cell, snap);
    }

    void StepOff(PlayerController player, Vector2Int target)
    {
        IsActive = false;
        Assignments.Clear();
        gm.ShowMessage($"{player.DisplayName}이(가) 뗏목에서 내렸습니다. 다시 모두 올라타야 출발할 수 있어요.");
        gm.MovePlayerTo(player, target);
    }

    public string DescribeAssignments()
    {
        var parts = new List<string>();
        foreach (var kv in Assignments)
        {
            var keys = new List<string>();
            foreach (var dir in kv.Value)
                keys.Add(PlayerController.KeyName(kv.Key.KeyFor(dir)));
            parts.Add($"{kv.Key.DisplayName}: {string.Join("/", keys)}");
        }
        return string.Join("    ", parts);
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, visualTarget, 1f - Mathf.Exp(-20f * Time.deltaTime));
    }
}
