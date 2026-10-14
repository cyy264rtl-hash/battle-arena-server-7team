using UnityEngine;

/// <summary>
/// 플레이어 1명. 칸 단위로 이동하고, 화면에서는 부드럽게 따라간다.
/// 실제 이동 가능 여부 판정은 GameManager가 한다 (나중에 서버 판정으로 교체할 자리).
/// </summary>
public class PlayerController : MonoBehaviour
{
    // 같은 칸에 여러 명이 서도 겹쳐 보이지 않도록 살짝 어긋나게 그린다
    static readonly Vector3[] VisualOffsets =
    {
        new Vector3(-0.15f,  0.12f), new Vector3( 0.15f, -0.12f),
        new Vector3( 0.15f,  0.12f), new Vector3(-0.15f, -0.12f),
    };

    public int    Index       { get; private set; }
    public string DisplayName { get; private set; }
    public Color  Color       { get; private set; }
    public Vector2Int Cell    { get; private set; }

    public KeyCode UpKey, DownKey, LeftKey, RightKey, PingKey;
    public string  KeyLabel;      // HUD 표시용 (예: "WASD")

    public int   Score;
    public int   PingsLeft = GameConfig.PingsPerPlayer;
    public float SlowUntil;
    public float BlindUntil;

    GameManager    gm;
    SpriteRenderer body;
    float          nextMoveTime;
    Vector3        visualTarget;

    public bool  IsSlowed     => Time.time < SlowUntil;
    public bool  IsBlind      => Time.time < BlindUntil;
    public float VisionRadius => IsBlind ? GameConfig.BlindVisionRadius : GameConfig.VisionRadius;

    public static PlayerController Create(GameManager gm, Transform parent, int index, string name,
                                          KeyCode[] keys, string keyLabel, Vector2Int cell)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var p = go.AddComponent<PlayerController>();
        p.gm          = gm;
        p.Index       = index;
        p.DisplayName = name;
        p.Color       = GameConfig.PlayerColors[index];
        p.UpKey = keys[0]; p.DownKey = keys[1]; p.LeftKey = keys[2]; p.RightKey = keys[3]; p.PingKey = keys[4];
        p.KeyLabel = keyLabel;

        SpriteFactory.Create("Outline", go.transform, SpriteFactory.Circle, Color.black,
                             Vector3.zero, GameConfig.OrderPlayer, 0.62f);
        p.body = SpriteFactory.Create("Body", go.transform, SpriteFactory.Circle, p.Color,
                                      Vector3.zero, GameConfig.OrderPlayer + 1, 0.5f);

        p.SetCell(cell, snap: true);
        return p;
    }

    public void SetCell(Vector2Int cell, bool snap = false)
    {
        Cell = cell;
        visualTarget = gm.CellToWorld(cell) + VisualOffsets[Index];
        if (snap) transform.position = visualTarget;
    }

    public KeyCode KeyFor(Vector2Int dir)
    {
        if (dir == Vector2Int.up)   return UpKey;
        if (dir == Vector2Int.down) return DownKey;
        if (dir == Vector2Int.left) return LeftKey;
        return RightKey;
    }

    /// <summary>개별 이동 모드에서 매 프레임 호출</summary>
    public void Tick()
    {
        var dir = ReadHeldDirection();
        if (dir == Vector2Int.zero || Time.time < nextMoveTime)
            return;

        if (gm.TryMovePlayer(this, dir))
        {
            float interval = GameConfig.MoveInterval * (IsSlowed ? GameConfig.SlowMultiplier : 1f);
            nextMoveTime = Time.time + interval;
        }
    }

    public static string KeyName(KeyCode key) => key switch
    {
        KeyCode.UpArrow    => "↑",
        KeyCode.DownArrow  => "↓",
        KeyCode.LeftArrow  => "←",
        KeyCode.RightArrow => "→",
        KeyCode.Slash      => "/",
        _                  => key.ToString(),
    };

    Vector2Int ReadHeldDirection()
    {
        if (Input.GetKey(UpKey))    return Vector2Int.up;
        if (Input.GetKey(DownKey))  return Vector2Int.down;
        if (Input.GetKey(LeftKey))  return Vector2Int.left;
        if (Input.GetKey(RightKey)) return Vector2Int.right;
        return Vector2Int.zero;
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, visualTarget, 1f - Mathf.Exp(-25f * Time.deltaTime));

        // 함정 페널티 중이면 깜빡임
        bool penalized = IsSlowed || IsBlind;
        float alpha = penalized ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 8f)) : 1f;
        body.color = new Color(Color.r, Color.g, Color.b, alpha);
    }
}
