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

    // ---------- 입체감 있는 색 아이콘 (상태 계기용) ----------
    // 모양은 부분 번호(0 = 바깥)를 돌려주는 함수로 그리고, 부분마다 바탕색을 둔다.
    // 위·왼쪽 가장자리는 밝은 테, 아래·오른쪽은 그늘, 위쪽이 조금 밝고, 작은 반짝임, 어두운 외곽선과 그림자를 넣어 손에 잡힐 듯한 모양으로 만든다
    public static Sprite Shaded(string name)
    {
        string key = "shaded_" + name;
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
        System.Func<Vector2, int> part;
        Color[] colors;
        switch (name)
        {
            case "heart":
                // 심장: 두 개의 둥근 봉우리 + 아래 뾰족한 끝
                part = p => (Circle(p, 0.35f, 0.62f, 0.2f) || Circle(p, 0.65f, 0.62f, 0.2f)
                    || Poly(p, new[] { new Vector2(0.16f, 0.58f), new Vector2(0.84f, 0.58f), new Vector2(0.5f, 0.1f) })) ? 1 : 0;
                colors = new[] { new Color(0.78f, 0.14f, 0.12f) };
                break;
            case "bottle":
                // 플라스틱 물병: 몸통(연한 파랑, 물이 든 아랫부분은 조금 진하게) + 목 + 파란 뚜껑 + 라벨 띠
                part = p =>
                {
                    if (Box(p, 0.5f, 0.86f, 0.1f, 0.06f)) return 3;                       // 뚜껑
                    if (Box(p, 0.5f, 0.74f, 0.075f, 0.07f)) return 1;                      // 목
                    if (Box(p, 0.5f, 0.4f, 0.17f, 0.3f) || Circle(p, 0.5f, 0.68f, 0.17f) || Circle(p, 0.5f, 0.12f, 0.17f))
                        return Box(p, 0.5f, 0.42f, 0.17f, 0.06f) ? 4 : p.y < 0.5f ? 2 : 1; // 라벨 / 물 / 빈 윗부분
                    return 0;
                };
                colors = new[] { new Color(0.72f, 0.84f, 0.95f), new Color(0.3f, 0.58f, 0.9f), new Color(0.16f, 0.32f, 0.62f), new Color(0.92f, 0.9f, 0.86f) };
                break;
            case "meat":
                // 뼈 붙은 고기: 둥근 살코기 + 비스듬한 뼈대와 끝의 두 혹
                part = p =>
                {
                    var q = Rot(p, 35);
                    if (Circle(q, 0.36f, 0.6f, 0.23f) || Circle(q, 0.5f, 0.52f, 0.16f)) return 1;
                    if (Box(q, 0.62f, 0.34f, 0.05f, 0.2f) || Circle(q, 0.56f, 0.14f, 0.07f) || Circle(q, 0.69f, 0.14f, 0.07f)) return 2;
                    return 0;
                };
                colors = new[] { new Color(0.66f, 0.27f, 0.15f), new Color(0.92f, 0.88f, 0.76f) };
                break;
            case "sun":
                // 맑음: 해 + 여덟 갈래 빛살
                part = p =>
                {
                    if (Circle(p, 0.5f, 0.5f, 0.21f)) return 1;
                    for (int k = 0; k < 8; k++) if (Box(Rot(p, k * 45f), 0.5f, 0.84f, 0.035f, 0.08f)) return 2;
                    return 0;
                };
                colors = new[] { new Color(0.98f, 0.72f, 0.2f), new Color(0.95f, 0.58f, 0.14f) };
                break;
            case "rain":
                // 비: 잿빛 구름 + 비스듬한 빗방울 셋
                part = p =>
                {
                    if (Circle(p, 0.36f, 0.6f, 0.16f) || Circle(p, 0.56f, 0.66f, 0.2f) || Circle(p, 0.74f, 0.58f, 0.14f) || Box(p, 0.55f, 0.5f, 0.33f, 0.1f)) return 1;
                    var q = Rot(p, 20f);
                    if (Box(q, 0.38f, 0.24f, 0.025f, 0.08f) || Box(q, 0.56f, 0.2f, 0.025f, 0.08f) || Box(q, 0.72f, 0.24f, 0.025f, 0.08f)) return 2;
                    return 0;
                };
                colors = new[] { new Color(0.55f, 0.57f, 0.6f), new Color(0.35f, 0.6f, 0.9f) };
                break;
            case "fog":
                // 안개: 끝이 둥근 띠 셋이 어긋나 겹친다
                part = p => (Box(p, 0.5f, 0.68f, 0.3f, 0.05f) || Circle(p, 0.2f, 0.68f, 0.05f) || Circle(p, 0.8f, 0.68f, 0.05f)
                    || Box(p, 0.44f, 0.5f, 0.34f, 0.05f) || Circle(p, 0.1f, 0.5f, 0.05f) || Circle(p, 0.78f, 0.5f, 0.05f)
                    || Box(p, 0.54f, 0.32f, 0.28f, 0.05f) || Circle(p, 0.26f, 0.32f, 0.05f) || Circle(p, 0.82f, 0.32f, 0.05f)) ? 1 : 0;
                colors = new[] { new Color(0.72f, 0.74f, 0.76f) };
                break;
            default:
                // 번개: 스테미너
                part = p => Poly(p, new[] { new Vector2(0.6f, 0.94f), new Vector2(0.24f, 0.46f), new Vector2(0.48f, 0.46f), new Vector2(0.38f, 0.06f), new Vector2(0.78f, 0.56f), new Vector2(0.53f, 0.56f) }) ? 1 : 0;
                colors = new[] { new Color(0.95f, 0.76f, 0.2f) };
                break;
        }

        const int S = 64, SS = 3;
        const float px1 = 1f / S;
        var outline = new Color(0.06f, 0.045f, 0.035f);
        var px = new Color[S * S];
        var counts = new int[colors.Length + 1];
        System.Func<Vector2, bool> near = p =>
        {
            if (part(p) != 0) return true;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4;
                if (part(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * px1 * 1.3f) != 0) return true;
            }
            return false;
        };
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int inside = 0, dilated = 0, shadow = 0;
                System.Array.Clear(counts, 0, counts.Length);
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        var p = new Vector2((x + (sx + 0.5f) / SS) / S, (y + (sy + 0.5f) / SS) / S);
                        int k = part(p);
                        if (k > 0) { inside++; counts[k]++; }
                        if (near(p)) dilated++;
                        if (near(p + new Vector2(-1.6f, 2.2f) * px1)) shadow++;
                    }
                float cover = inside / (float)(SS * SS), coverD = dilated / (float)(SS * SS), coverS = shadow / (float)(SS * SS);
                var c = new Color(0, 0, 0, 0);
                if (coverS > 0) c = new Color(0, 0, 0, coverS * 0.5f);
                if (coverD > 0)
                {
                    var fill = outline;
                    if (cover > 0)
                    {
                        int best = 1;
                        for (int k = 2; k < counts.Length; k++) if (counts[k] > counts[best]) best = k;
                        var baseC = colors[best - 1];
                        var center = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                        float lit = (center.y - 0.5f) * 0.3f;
                        if (part(center + new Vector2(-2.2f, 2.2f) * px1) == 0) lit += 0.4f;
                        if (part(center + new Vector2(2.2f, -2.2f) * px1) == 0) lit -= 0.3f;
                        float spec = Mathf.Exp(-((center.x - 0.38f) * (center.x - 0.38f) + (center.y - 0.68f) * (center.y - 0.68f)) / 0.014f) * 0.45f;
                        var shaded = new Color(baseC.r * (1 + lit), baseC.g * (1 + lit), baseC.b * (1 + lit));
                        shaded = Color.Lerp(shaded, Color.white, spec);
                        fill = Color.Lerp(outline, shaded, cover / coverD);
                    }
                    c = new Color(fill.r, fill.g, fill.b, Mathf.Max(c.a, coverD));
                }
                px[y * S + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
            }
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "HudIcon_" + key };
        tex.SetPixels(px);
        tex.Apply(true, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        cache[key] = sprite;
        return sprite;
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
