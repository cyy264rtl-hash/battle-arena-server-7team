using UnityEngine;

/// <summary>
/// 이미지 에셋 없이 코드로 기본 도형 스프라이트를 만든다 (크기 1 유닛)
/// </summary>
public static class SpriteFactory
{
    static Sprite square;
    static Sprite circle;
    static Sprite ring;

    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            }
            return square;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null) circle = CreateDisc(64, 0f);
            return circle;
        }
    }

    public static Sprite Ring
    {
        get
        {
            if (ring == null) ring = CreateDisc(64, 0.75f);
            return ring;
        }
    }

    /// <summary>innerRatio가 0이면 꽉 찬 원, 0보다 크면 고리</summary>
    static Sprite CreateDisc(int size, float innerRatio)
    {
        var tex = new Texture2D(size, size) { filterMode = FilterMode.Bilinear };
        float r      = size / 2f;
        float inner  = r * innerRatio;
        var pixels   = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float outerAlpha = Mathf.Clamp01(r - d);
                float innerAlpha = innerRatio > 0f ? Mathf.Clamp01(d - inner) : 1f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Min(outerAlpha, innerAlpha));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public static SpriteRenderer Create(string name, Transform parent, Sprite sprite, Color color,
                                        Vector3 position, int order, float scale = 1f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position   = position;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.color        = color;
        sr.sortingOrder = order;
        return sr;
    }
}
