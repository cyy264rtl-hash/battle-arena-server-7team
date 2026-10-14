using UnityEngine;

/// <summary>
/// 시야 제한 (Fog of War)
/// - 플레이어 주변(시야 반경)은 밝게
/// - 한 번 탐색한 곳은 어둡게
/// - 가본 적 없는 곳은 완전히 가림
/// 오프라인에서는 한 화면을 같이 보므로 전원의 시야를 합쳐서 보여준다.
/// </summary>
public class FogOfWar : MonoBehaviour
{
    GameManager      gm;
    SpriteRenderer[,] overlays;
    bool[,]          explored;
    float[,]         alpha;

    public static FogOfWar Create(GameManager gm, Transform parent)
    {
        var go = new GameObject("FogOfWar");
        go.transform.SetParent(parent, false);

        var fog = go.AddComponent<FogOfWar>();
        fog.gm = gm;
        int w = gm.Maze.Width, h = gm.Maze.Height;
        fog.overlays = new SpriteRenderer[w, h];
        fog.explored = new bool[w, h];
        fog.alpha    = new float[w, h];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                fog.alpha[x, y] = 1f;
                fog.overlays[x, y] = SpriteFactory.Create($"Fog_{x}_{y}", go.transform, SpriteFactory.Square,
                                                          Color.black, gm.CellToWorld(new Vector2Int(x, y)),
                                                          GameConfig.OrderFog, 1.02f);
            }
        }
        return fog;
    }

    public bool IsVisible(Vector2Int cell)
    {
        foreach (var p in gm.Players)
            if (Vector2.Distance(p.Cell, cell) <= p.VisionRadius)
                return true;
        return false;
    }

    void LateUpdate()
    {
        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        for (int x = 0; x < gm.Maze.Width; x++)
        {
            for (int y = 0; y < gm.Maze.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                bool visible = IsVisible(cell);
                if (visible) explored[x, y] = true;

                float target = visible ? 0f : explored[x, y] ? GameConfig.ExploredFogAlpha : 1f;
                alpha[x, y] = Mathf.Lerp(alpha[x, y], target, blend);
                overlays[x, y].color = new Color(0f, 0f, 0f, alpha[x, y]);
            }
        }
    }
}
