using System.Collections.Generic;
using UnityEngine;

// HUD 상태 줄 아이콘 (흰색, 코드로 그린다): 체력 +, 갈증 물방울, 배고픔 포크·나이프 X, 스테미너 번개,
// 남은 시간 모래시계, 붉은 밤 달, 방공호 집, 무거운 짐 추
public static class HudIcons
{
    const int Size = 64, Samples = 4;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string name)
    {
        if (cache.TryGetValue(name, out var s) && s != null) return s;
        System.Func<Vector2, bool> inside;
        switch (name)
        {
            case "plus": inside = p => Box(p, 0.5f, 0.5f, 0.11f, 0.34f) || Box(p, 0.5f, 0.5f, 0.34f, 0.11f); break;
            case "drop": inside = p => Circle(p, 0.5f, 0.4f, 0.24f) || Poly(p, new[] { new Vector2(0.5f, 0.92f), new Vector2(0.27f, 0.48f), new Vector2(0.73f, 0.48f) }); break;
            case "bolt":
                inside = p => Poly(p, new[] { new Vector2(0.6f, 0.94f), new Vector2(0.24f, 0.46f), new Vector2(0.48f, 0.46f), new Vector2(0.38f, 0.06f), new Vector2(0.78f, 0.56f), new Vector2(0.53f, 0.56f) });
                break;
            case "food":
                // 포크와 나이프를 X자로 겹친 모양
                inside = p =>
                {
                    var a = Rot(p, 45);
                    var b = Rot(p, -45);
                    bool knife = Box(a, 0.5f, 0.5f, 0.07f, 0.36f) || Box(a, 0.5f, 0.68f, 0.1f, 0.2f);
                    bool fork = Box(b, 0.5f, 0.42f, 0.06f, 0.3f) || Box(b, 0.5f, 0.74f, 0.13f, 0.03f)
                        || Box(b, 0.39f, 0.82f, 0.025f, 0.1f) || Box(b, 0.5f, 0.82f, 0.025f, 0.1f) || Box(b, 0.61f, 0.82f, 0.025f, 0.1f);
                    return knife || fork;
                };
                break;
            case "hourglass":
                inside = p => Box(p, 0.5f, 0.89f, 0.3f, 0.04f) || Box(p, 0.5f, 0.11f, 0.3f, 0.04f)
                    || Poly(p, new[] { new Vector2(0.26f, 0.84f), new Vector2(0.74f, 0.84f), new Vector2(0.54f, 0.5f), new Vector2(0.46f, 0.5f) })
                    || Poly(p, new[] { new Vector2(0.46f, 0.5f), new Vector2(0.54f, 0.5f), new Vector2(0.74f, 0.16f), new Vector2(0.26f, 0.16f) });
                break;
            case "moon": inside = p => Circle(p, 0.5f, 0.5f, 0.36f) && !Circle(p, 0.66f, 0.62f, 0.3f); break;
            case "home":
                inside = p => Poly(p, new[] { new Vector2(0.5f, 0.88f), new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f) })
                    || (Box(p, 0.5f, 0.32f, 0.3f, 0.2f) && !Box(p, 0.5f, 0.24f, 0.08f, 0.12f));
                break;
            case "weight":
                inside = p => Poly(p, new[] { new Vector2(0.3f, 0.62f), new Vector2(0.7f, 0.62f), new Vector2(0.86f, 0.12f), new Vector2(0.14f, 0.12f) })
                    || (Circle(p, 0.5f, 0.74f, 0.13f) && !Circle(p, 0.5f, 0.74f, 0.06f));
                break;
            default: inside = p => Circle(p, 0.5f, 0.5f, 0.3f); break;
        }
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "HudIcon_" + name };
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < Samples; sy++)
                    for (int sx = 0; sx < Samples; sx++)
                        if (inside(new Vector2((x + (sx + 0.5f) / Samples) / Size, (y + (sy + 0.5f) / Samples) / Size))) hit++;
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(255 * hit / (Samples * Samples)));
            }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
        cache[name] = s;
        return s;
    }

    static Vector2 Rot(Vector2 p, float deg)
    {
        var q = Quaternion.Euler(0, 0, deg) * (p - new Vector2(0.5f, 0.5f));
        return new Vector2(q.x + 0.5f, q.y + 0.5f);
    }

    static bool Box(Vector2 p, float cx, float cy, float hx, float hy) => Mathf.Abs(p.x - cx) <= hx && Mathf.Abs(p.y - cy) <= hy;

    static bool Circle(Vector2 p, float cx, float cy, float r) => (p.x - cx) * (p.x - cx) + (p.y - cy) * (p.y - cy) <= r * r;

    static bool Poly(Vector2 p, Vector2[] v)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                inside = !inside;
        return inside;
    }
}
