using System.Collections.Generic;
using UnityEngine;

public enum GameState { Playing, Escaped, TimeOver }

/// <summary>
/// 협동 미로 탈출 — 오프라인 프로토타입
/// 지금은 이 클래스가 "서버가 해야 할 일"(이동 검증, 코인 판정, 협동 장치, 타이머, 탈출 판정)을 직접 한다.
/// 나중에 서버 연동 시 이 판정 로직을 서버로 옮기고, 클라이언트는 패킷 결과만 반영하면 된다.
/// </summary>
public class GameManager : MonoBehaviour
{
    class Coin
    {
        public Vector2Int     Cell;
        public bool           IsShared;
        public SpriteRenderer Renderer;
        public bool           HintShown;
    }

    class Trap
    {
        public Vector2Int     Cell;
        public bool           Triggered;
        public SpriteRenderer Renderer;
    }

    class Ping
    {
        public SpriteRenderer Renderer;
        public float          ExpireAt;
    }

    static readonly KeyCode[][] PlayerKeys =
    {
        new[] { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Q },
        new[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Slash },
    };
    static readonly string[] PlayerKeyLabels = { "WASD 이동 / Q 핑", "방향키 이동 / '/' 핑" };

    public MazeData  Maze     { get; private set; }
    public GameState State    { get; private set; }
    public float     TimeLeft { get; private set; }
    public readonly List<PlayerController> Players = new List<PlayerController>();

    readonly List<Coin> coins = new List<Coin>();
    readonly List<Trap> traps = new List<Trap>();
    readonly List<Ping> pings = new List<Ping>();
    Raft          raft;
    Transform     world;
    System.Random rng;
    Camera        cam;

    string message;
    float  messageUntil;
    GUIStyle hudStyle, messageStyle, titleStyle;

    void Start()
    {
        SetupCamera();
        NewGame();
    }

    // ─────────────────────────────────────────
    // 게임 생성
    // ─────────────────────────────────────────

    void NewGame()
    {
        if (world != null) Destroy(world.gameObject);
        world = new GameObject("World").transform;
        Players.Clear();
        coins.Clear();
        traps.Clear();
        pings.Clear();

        rng  = new System.Random();
        Maze = MazeGenerator.Generate(rng);
        BuildTiles();

        var occupied = new HashSet<Vector2Int> { Maze.LeftDock, Maze.RightDock, Maze.Exit };
        SpawnPlayers(occupied);
        SpawnItems(occupied);

        raft = Raft.Create(this, world, Maze.LeftDock);
        FogOfWar.Create(this, world);

        TimeLeft = GameConfig.TimeLimit;
        State    = GameState.Playing;
        ShowMessage("서로 다른 곳에서 시작했습니다! 코인을 모으고, 뗏목으로 강을 건너 함께 출구로 탈출하세요.", 5f);
    }

    public Vector3 CellToWorld(Vector2Int cell) =>
        new Vector3(cell.x - Maze.Width / 2f + 0.5f, cell.y - Maze.Height / 2f + 0.5f, 0f);

    void BuildTiles()
    {
        var root = new GameObject("Tiles").transform;
        root.SetParent(world, false);

        for (int x = 0; x < Maze.Width; x++)
        {
            for (int y = 0; y < Maze.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                var type = Maze.Get(cell);
                SpriteFactory.Create($"{type}_{x}_{y}", root, SpriteFactory.Square, TileColor(type),
                                     CellToWorld(cell), GameConfig.OrderTile);

                if (type == TileType.Exit)
                    SpriteFactory.Create("ExitMark", root, SpriteFactory.Ring, Color.white,
                                         CellToWorld(cell), GameConfig.OrderTile + 1, 0.7f);
            }
        }
    }

    static Color TileColor(TileType type) => type switch
    {
        TileType.Floor => GameConfig.FloorColor,
        TileType.Water => GameConfig.WaterColor,
        TileType.Rock  => GameConfig.RockColor,
        TileType.Dock  => GameConfig.DockColor,
        TileType.Exit  => GameConfig.ExitColor,
        _              => GameConfig.WallColor,
    };

    List<Vector2Int> FloorCells(bool leftSide, HashSet<Vector2Int> exclude)
    {
        var cells = new List<Vector2Int>();
        for (int x = 0; x < Maze.Width; x++)
        {
            for (int y = 0; y < Maze.Height; y++)
            {
                var c = new Vector2Int(x, y);
                if (Maze.Get(c) == TileType.Floor && Maze.IsLeftSide(c) == leftSide && !exclude.Contains(c))
                    cells.Add(c);
            }
        }
        // 섞기 (Fisher-Yates)
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
        return cells;
    }

    /// <summary>플레이어마다 왼쪽 미로의 서로 멀리 떨어진 랜덤 위치에 스폰</summary>
    void SpawnPlayers(HashSet<Vector2Int> occupied)
    {
        var candidates = FloorCells(true, occupied);
        var spawns = new List<Vector2Int>();

        foreach (var c in candidates)
        {
            bool farEnough = spawns.TrueForAll(s => Vector2Int.Distance(s, c) >= GameConfig.MinSpawnDistance);
            if (farEnough) spawns.Add(c);
            if (spawns.Count == PlayerKeys.Length) break;
        }
        // 거리 조건을 못 맞추면 아무 칸이나
        for (int i = 0; spawns.Count < PlayerKeys.Length; i++)
            if (!spawns.Contains(candidates[i])) spawns.Add(candidates[i]);

        for (int i = 0; i < PlayerKeys.Length; i++)
        {
            occupied.Add(spawns[i]);
            Players.Add(PlayerController.Create(this, world, i, $"P{i + 1}", PlayerKeys[i], PlayerKeyLabels[i], spawns[i]));
        }
    }

    void SpawnItems(HashSet<Vector2Int> occupied)
    {
        var root = new GameObject("Items").transform;
        root.SetParent(world, false);

        foreach (bool left in new[] { true, false })
        {
            var cells = new Queue<Vector2Int>(FloorCells(left, occupied));
            int coinCount   = left ? GameConfig.LeftCoins       : GameConfig.RightCoins;
            int sharedCount = left ? GameConfig.LeftSharedCoins : GameConfig.RightSharedCoins;
            int trapCount   = left ? GameConfig.LeftTraps       : GameConfig.RightTraps;

            for (int i = 0; i < coinCount && cells.Count > 0; i++)
                coins.Add(CreateCoin(root, cells.Dequeue(), false));
            for (int i = 0; i < sharedCount && cells.Count > 0; i++)
                coins.Add(CreateCoin(root, cells.Dequeue(), true));
            for (int i = 0; i < trapCount && cells.Count > 0; i++)
            {
                var cell = cells.Dequeue();
                traps.Add(new Trap
                {
                    Cell = cell,
                    Renderer = SpriteFactory.Create("Trap", root, SpriteFactory.Square, GameConfig.TrapHiddenColor,
                                                    CellToWorld(cell), GameConfig.OrderItem, 0.35f),
                });
            }
        }
    }

    Coin CreateCoin(Transform root, Vector2Int cell, bool shared)
    {
        var sr = SpriteFactory.Create(shared ? "SharedCoin" : "Coin", root, SpriteFactory.Circle,
                                      shared ? GameConfig.SharedCoinColor : GameConfig.CoinColor,
                                      CellToWorld(cell), GameConfig.OrderItem, shared ? 0.55f : 0.35f);
        if (shared)
            SpriteFactory.Create("Ring", sr.transform, SpriteFactory.Ring, Color.white,
                                 sr.transform.position, GameConfig.OrderItem + 1, 1.35f);
        return new Coin { Cell = cell, IsShared = shared, Renderer = sr };
    }

    // ─────────────────────────────────────────
    // 게임 루프
    // ─────────────────────────────────────────

    void Update()
    {
        if (State != GameState.Playing)
        {
            if (Input.GetKeyDown(KeyCode.R)) NewGame();
            return;
        }

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            EndGame(GameState.TimeOver);
            return;
        }

        // 1) 입력 → 2) 검증 → 3) 상태 갱신 (협동 장치 탑승 중이면 장치 조작 모드)
        if (raft.IsActive)
            raft.Tick();
        else
            foreach (var p in Players) p.Tick();

        foreach (var p in Players)
            if (Input.GetKeyDown(p.PingKey)) TryPing(p);

        // 4) 판정
        CheckSharedCoins();
        CheckEscape();

        AnimateItems();
    }

    public bool TryMovePlayer(PlayerController player, Vector2Int dir)
    {
        var target = player.Cell + dir;
        if (!Maze.IsWalkable(target))
            return false;

        MovePlayerTo(player, target);
        return true;
    }

    public void MovePlayerTo(PlayerController player, Vector2Int target)
    {
        player.SetCell(target);
        OnPlayerEntered(player, target);
    }

    void OnPlayerEntered(PlayerController player, Vector2Int cell)
    {
        // 개인 코인
        for (int i = coins.Count - 1; i >= 0; i--)
        {
            var c = coins[i];
            if (c.Cell != cell) continue;

            if (!c.IsShared)
            {
                player.Score += GameConfig.CoinScore;
                RemoveCoin(i);
            }
            else if (!c.HintShown)
            {
                c.HintShown = true;
                ShowMessage($"공동 코인 발견! {GameConfig.SharedCoinPlayers}명이 함께 있어야 먹을 수 있어요. 핑으로 친구를 불러보세요.");
            }
        }

        // 함정
        foreach (var t in traps)
        {
            if (t.Triggered || t.Cell != cell) continue;
            t.Triggered = true;
            t.Renderer.color = GameConfig.TrapColor;
            t.Renderer.transform.localScale = Vector3.one * 0.6f;
            player.SlowUntil  = Time.time + GameConfig.PenaltyDuration;
            player.BlindUntil = Time.time + GameConfig.PenaltyDuration;
            ShowMessage($"{player.DisplayName}이(가) 함정을 밟았습니다! {GameConfig.PenaltyDuration:0}초간 이동 속도 감소 + 시야 축소");
        }

        // 협동 장치 탑승: 전원이 뗏목 칸에 모이면 탑승
        if (!raft.IsActive && cell == raft.Cell && Players.TrueForAll(p => p.Cell == raft.Cell))
            raft.Board(Players);
        else if (!raft.IsActive && cell == raft.Cell)
            ShowMessage("뗏목은 모든 플레이어가 올라타야 움직입니다.");
    }

    void CheckSharedCoins()
    {
        for (int i = coins.Count - 1; i >= 0; i--)
        {
            var c = coins[i];
            if (!c.IsShared) continue;

            var nearby = Players.FindAll(p => Mathf.Max(Mathf.Abs(p.Cell.x - c.Cell.x), Mathf.Abs(p.Cell.y - c.Cell.y)) <= 1);
            bool someoneOn = Players.Exists(p => p.Cell == c.Cell);
            if (!someoneOn || nearby.Count < GameConfig.SharedCoinPlayers) continue;

            foreach (var p in nearby) p.Score += GameConfig.SharedCoinScore;
            RemoveCoin(i);
            ShowMessage($"공동 코인 획득! 함께한 {nearby.Count}명 모두 +{GameConfig.SharedCoinScore}점");
        }
    }

    void RemoveCoin(int index)
    {
        Destroy(coins[index].Renderer.gameObject);
        coins.RemoveAt(index);
    }

    void CheckEscape()
    {
        if (Players.TrueForAll(p => p.Cell == Maze.Exit))
            EndGame(GameState.Escaped);
        else if (Players.Exists(p => p.Cell == Maze.Exit))
            ShowMessage("출구 도착! 모든 플레이어가 출구에 모여야 탈출합니다.", 0.5f);
    }

    void EndGame(GameState result)
    {
        State = result;
        if (result == GameState.Escaped)
            foreach (var p in Players) p.Score += GameConfig.EscapeBonus;
    }

    void TryPing(PlayerController player)
    {
        if (player.PingsLeft <= 0)
        {
            ShowMessage($"{player.DisplayName}의 핑을 모두 사용했습니다.");
            return;
        }
        player.PingsLeft--;

        var sr = SpriteFactory.Create("Ping", world, SpriteFactory.Ring, player.Color,
                                      CellToWorld(player.Cell), GameConfig.OrderPing);
        pings.Add(new Ping { Renderer = sr, ExpireAt = Time.time + GameConfig.PingDuration });
        ShowMessage($"{player.DisplayName}: \"여기 코인 있어!\" (남은 핑 {player.PingsLeft})");
    }

    void AnimateItems()
    {
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 5f);
        foreach (var c in coins)
            c.Renderer.transform.localScale = Vector3.one * (c.IsShared ? 0.55f : 0.35f) * pulse;

        for (int i = pings.Count - 1; i >= 0; i--)
        {
            var ping = pings[i];
            float remain = ping.ExpireAt - Time.time;
            if (remain <= 0f)
            {
                Destroy(ping.Renderer.gameObject);
                pings.RemoveAt(i);
                continue;
            }
            float t = (Time.time * 1.5f) % 1f;
            ping.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.2f, t);
            var col = ping.Renderer.color;
            ping.Renderer.color = new Color(col.r, col.g, col.b, (1f - t) * Mathf.Clamp01(remain));
        }
    }

    public void ShowMessage(string text, float duration = 3f)
    {
        message = text;
        messageUntil = Time.time + duration;
    }

    // ─────────────────────────────────────────
    // 카메라 / HUD
    // ─────────────────────────────────────────

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        cam.orthographic    = true;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = GameConfig.Background;
        cam.transform.position = new Vector3(0f, 0.9f, -10f);
    }

    void LateUpdate()
    {
        // 화면 비율이 바뀌어도 맵 전체가 보이도록
        float halfWidth = GameConfig.Width / 2f + 0.5f;
        cam.orthographicSize = Mathf.Max(GameConfig.Height / 2f + 1.6f, halfWidth / cam.aspect);
    }

    void OnGUI()
    {
        if (hudStyle == null)
        {
            hudStyle     = new GUIStyle(GUI.skin.label) { richText = true };
            messageStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            titleStyle   = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold };
        }
        int fs = Mathf.Max(12, Screen.height / 40);
        hudStyle.fontSize     = fs;
        messageStyle.fontSize = fs;
        titleStyle.fontSize   = fs * 2;

        // 상단 정보
        int minutes = Mathf.FloorToInt(TimeLeft / 60f);
        int seconds = Mathf.FloorToInt(TimeLeft % 60f);
        string timeColor = TimeLeft < 30f ? "#ff6060" : "#ffffff";
        GUI.Label(new Rect(16, 8, 400, fs * 1.6f), $"<color={timeColor}>남은 시간 {minutes}:{seconds:00}</color>", hudStyle);

        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            string hex = ColorUtility.ToHtmlStringRGB(p.Color);
            string status = p.IsSlowed ? " <color=#ff6060>[함정]</color>" : "";
            GUI.Label(new Rect(16 + (i + 1) * Screen.width * 0.22f, 8, Screen.width * 0.22f, fs * 1.6f),
                      $"<color=#{hex}>● {p.DisplayName}</color>  {p.Score}점  핑 {p.PingsLeft}{status}", hudStyle);
        }

        string mode = raft.IsActive
            ? $"<color=#ffd060>뗏목 조작 중 — {raft.DescribeAssignments()}</color>"
            : $"P1: {Players[0].KeyLabel}     P2: {Players[1].KeyLabel}     남은 코인 {coins.Count}";
        GUI.Label(new Rect(16, 8 + fs * 1.6f, Screen.width - 32, fs * 1.6f), mode, hudStyle);

        if (Time.time < messageUntil && State == GameState.Playing)
            GUI.Label(new Rect(0, Screen.height - fs * 3f, Screen.width, fs * 2f), message, messageStyle);

        // 결과 화면
        if (State != GameState.Playing)
        {
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string title = State == GameState.Escaped ? "<color=#60ff90>탈출 성공!</color>" : "<color=#ff6060>시간 초과 — 탈출 실패</color>";
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, fs * 3f), title, titleStyle);

            var ranking = new List<PlayerController>(Players);
            ranking.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (int i = 0; i < ranking.Count; i++)
            {
                var p = ranking[i];
                GUI.Label(new Rect(0, Screen.height * 0.45f + i * fs * 1.8f, Screen.width, fs * 1.8f),
                          $"{i + 1}위  <color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.DisplayName}</color>  {p.Score}점", messageStyle);
            }
            GUI.Label(new Rect(0, Screen.height * 0.7f, Screen.width, fs * 2f), "R 키를 눌러 다시 시작", messageStyle);
        }
    }
}
