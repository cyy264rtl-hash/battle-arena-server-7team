using UnityEngine;

/// <summary>
/// 게임 전체 설정값
/// 맵 구성: [왼쪽 미로][강][오른쪽 미로(출구)]
/// </summary>
public static class GameConfig
{
    // ── 맵 ──
    public const int Height     = 21;
    public const int LeftWidth  = 19;
    public const int RiverWidth = 5;
    public const int RightWidth = 17;
    public const int Width      = LeftWidth + RiverWidth + RightWidth;

    public const float ExtraPassageChance = 0.08f;   // 미로에 추가 통로(순환로)를 뚫을 확률
    public const int   MinRocks = 14;
    public const int   MaxRocks = 20;

    // ── 아이템 ──
    public const int LeftCoins        = 12;
    public const int RightCoins       = 8;
    public const int LeftSharedCoins  = 2;
    public const int RightSharedCoins = 2;
    public const int LeftTraps        = 4;
    public const int RightTraps       = 3;

    // ── 점수 ──
    public const int CoinScore        = 10;
    public const int SharedCoinScore  = 30;
    public const int EscapeBonus      = 100;
    public const int SharedCoinPlayers = 2;          // 공동 코인에 필요한 인원

    // ── 시간 / 이동 ──
    public const float TimeLimit      = 240f;
    public const float MoveInterval   = 0.13f;
    public const float SlowMultiplier = 2f;
    public const float MinSpawnDistance = 8f;

    // ── 시야 / 함정 ──
    public const float VisionRadius      = 3.5f;
    public const float BlindVisionRadius = 1.6f;
    public const float PenaltyDuration   = 4f;
    public const float ExploredFogAlpha  = 0.6f;

    // ── 핑 ──
    public const int   PingsPerPlayer = 3;
    public const float PingDuration   = 4f;

    // ── 색상 ──
    public static readonly Color Background = new Color(0.05f, 0.06f, 0.10f);
    public static readonly Color WallColor  = new Color(0.22f, 0.25f, 0.33f);
    public static readonly Color FloorColor = new Color(0.12f, 0.13f, 0.17f);
    public static readonly Color WaterColor = new Color(0.12f, 0.35f, 0.62f);
    public static readonly Color RockColor  = new Color(0.40f, 0.36f, 0.32f);
    public static readonly Color DockColor  = new Color(0.55f, 0.40f, 0.22f);
    public static readonly Color ExitColor  = new Color(0.20f, 0.70f, 0.35f);
    public static readonly Color RaftColor  = new Color(0.72f, 0.52f, 0.28f);
    public static readonly Color CoinColor  = new Color(1.00f, 0.84f, 0.20f);
    public static readonly Color SharedCoinColor = new Color(1.00f, 0.45f, 0.15f);
    public static readonly Color TrapHiddenColor = new Color(0.35f, 0.12f, 0.12f, 0.6f);
    public static readonly Color TrapColor  = new Color(0.95f, 0.20f, 0.20f);

    public static readonly Color[] PlayerColors =
    {
        new Color(0.30f, 0.85f, 1.00f),
        new Color(1.00f, 0.45f, 0.75f),
        new Color(0.60f, 1.00f, 0.40f),
        new Color(1.00f, 0.75f, 0.30f),
    };

    // ── 정렬 순서 ──
    public const int OrderTile   = 0;
    public const int OrderItem   = 5;
    public const int OrderRaft   = 8;
    public const int OrderFog    = 20;
    public const int OrderPlayer = 30;
    public const int OrderPing   = 40;
}
