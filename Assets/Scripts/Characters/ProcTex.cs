using System.Collections.Generic;
using UnityEngine;

// 캐릭터 재질을 실행 중에 만든다: 색 무늬(얼룩·핏자국·때)와 울퉁불퉁한 면(노멀맵: 천의 결·가죽 주름·피부 모공·털)을
// 이어 붙여도 이음매가 보이지 않는 무늬로 그린다. 다운로드한 그림 없이 전부 코드로 만든다.
// 텍스처 한 장은 tile m 크기의 몸 표면을 덮는다 (메시 UV가 미터 단위)
public static class ProcTex
{
    public enum Kind { Skin, ZombieSkin, Cloth, Denim, Knit, Leather, Fur, Hair, Wood, Metal, Rubber }

    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static Shader lit;

    // 발광 재질을 만들 때 복사할 원본 (빌드에서 발광 셰이더가 빠지지 않게 씬·프리팹에 있는 발광 재질을 쓴다)
    public static Material GlowTemplate;

    // kind 무늬의 재질. blood = 핏자국 수, dirt = 때·얼룩 정도(0~1), seed로 무늬를 바꾼다
    public static Material Get(Kind kind, Color color, int seed = 0, int blood = 0, float dirt = 0.3f, Color? shade = null)
    {
        string key = $"{kind}/{color}/{seed}/{blood}/{dirt:0.00}/{shade}";
        if (materials.TryGetValue(key, out var m) && m != null) return m;
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        var s = Settings(kind);
        int size = blood > 0 ? 256 : s.size;
        float tile = blood > 0 ? 0.6f : s.tile;
        var dark = shade ?? color * 0.72f;
        Make(kind, size, tile, color, dark, seed, blood, dirt, s, out var albedo, out var normal);
        m = new Material(lit) { name = key };
        m.SetTexture("_BaseMap", albedo);
        m.SetTexture("_BumpMap", normal);
        m.SetFloat("_BumpScale", s.bump);
        m.EnableKeyword("_NORMALMAP");
        m.SetTextureScale("_BaseMap", Vector2.one / tile);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", s.smooth);
        m.SetFloat("_Metallic", kind == Kind.Metal ? 0.7f : 0f);
        materials[key] = m;
        return m;
    }

    // 무늬 없는 단색 (눈알·이빨·입속 등 작은 부품)
    public static Material Plain(Color color, float smoothness = 0.3f, float metallic = 0f)
    {
        string key = $"plain/{color}/{smoothness}/{metallic}";
        if (materials.TryGetValue(key, out var m) && m != null) return m;
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(lit) { name = key };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        materials[key] = m;
        return m;
    }

    // 스스로 빛나는 재질 (좀비·들개 눈, 등불)
    public static Material Glow(Color color, Color emission)
    {
        string key = $"glow/{color}/{emission}";
        if (materials.TryGetValue(key, out var m) && m != null) return m;
        if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
        m = GlowTemplate != null ? new Material(GlowTemplate) : new Material(lit);
        m.name = key;
        m.SetTexture("_BaseMap", null);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetColor("_BaseColor", color);
        m.SetColor("_EmissionColor", emission);
        m.SetFloat("_Smoothness", 0.8f);
        materials[key] = m;
        return m;
    }

    struct Look { public int size; public float tile, bump, smooth; }

    static Look Settings(Kind k)
    {
        switch (k)
        {
            case Kind.Skin: return new Look { size = 128, tile = 0.25f, bump = 0.35f, smooth = 0.38f };
            case Kind.ZombieSkin: return new Look { size = 256, tile = 0.45f, bump = 0.32f, smooth = 0.4f };
            case Kind.Cloth: return new Look { size = 256, tile = 0.2f, bump = 0.25f, smooth = 0.08f };
            case Kind.Denim: return new Look { size = 256, tile = 0.16f, bump = 0.3f, smooth = 0.1f };
            case Kind.Knit: return new Look { size = 128, tile = 0.12f, bump = 1f, smooth = 0.05f };
            case Kind.Leather: return new Look { size = 256, tile = 0.3f, bump = 0.3f, smooth = 0.4f };
            case Kind.Fur: return new Look { size = 256, tile = 0.3f, bump = 1f, smooth = 0.12f };
            case Kind.Hair: return new Look { size = 128, tile = 0.15f, bump = 1f, smooth = 0.25f };
            case Kind.Wood: return new Look { size = 128, tile = 0.35f, bump = 0.6f, smooth = 0.3f };
            case Kind.Metal: return new Look { size = 128, tile = 0.3f, bump = 0.4f, smooth = 0.55f };
            default: return new Look { size = 64, tile = 0.2f, bump = 0.3f, smooth = 0.3f };
        }
    }

    static void Make(Kind kind, int size, float tile, Color a, Color b, int seed, int blood, float dirt, Look look, out Texture2D albedo, out Texture2D normal)
    {
        var height = new float[size * size];
        var col = new Color[size * size];
        // 핏자국 자리 (텍스처 한 장 안에 이어지게)
        var stains = new Vector4[blood];
        var rng = new System.Random(seed * 7919 + (int)kind * 31 + 17);
        for (int i = 0; i < blood; i++)
            stains[i] = new Vector4((float)rng.NextDouble(), (float)rng.NextDouble(), 0.05f + 0.1f * (float)rng.NextDouble(), (float)rng.NextDouble());
        var bloodDark = new Color(0.24f, 0.05f, 0.04f);
        var bloodFresh = new Color(0.46f, 0.1f, 0.08f);
        // 무늬 주기(텍스처 한 장에 몇 번): 천의 결은 촘촘하게. 한 주기가 8픽셀보다 좁으면 멀리서 바둑판처럼 자글거린다
        int weave = Mathf.Clamp(Mathf.RoundToInt(tile / 0.004f), 4, size / 8);
        // 핏자국 때문에 한 장이 넓은 면(tile)을 덮으면 결이 굵어지므로 결을 거의 지운다
        float weaveAmt = tile <= 0.25f ? 0.35f : 0.08f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float h = 0, t = 0;
                float big = Fbm(u, v, 4, 4, seed);
                float mid = Fbm(u, v, 12, 3, seed + 5);
                float fine = Noise(u * 48, v * 48, 48, seed + 9);
                switch (kind)
                {
                    case Kind.Skin:
                        h = mid * 0.5f + fine * 0.5f;
                        t = 0.5f + (big - 0.5f) * 0.5f + (fine - 0.5f) * 0.15f;
                        break;
                    case Kind.ZombieSkin:
                    {
                        // 퍼런 핏줄(가는 능선), 멍과 썩은 얼룩, 갈라진 살갗
                        float vein = 1 - Mathf.Abs(Fbm(u * 1.0f, v * 1.0f, 6, 3, seed + 3) * 2 - 1);
                        vein = Mathf.Pow(Mathf.Clamp01(vein), 14);
                        float blotch = Step(0.55f, 0.75f, Fbm(u, v, 3, 4, seed + 11));
                        h = mid * 0.2f + fine * 0.6f - vein * 0.35f + blotch * 0.15f;
                        t = 0.55f + (big - 0.5f) * 0.6f - blotch * 0.35f - vein * 0.25f + (fine - 0.5f) * 0.18f;
                        break;
                    }
                    case Kind.Cloth:
                    {
                        float wx = Mathf.Sin(u * weave * Mathf.PI * 2), wy = Mathf.Sin(v * weave * Mathf.PI * 2);
                        float cloth = (wx * 0.5f + 0.5f) * (wy > 0 ? 1 : 0.6f) * 0.5f + (wy * 0.5f + 0.5f) * (wx > 0 ? 0.6f : 1) * 0.5f;
                        // 결은 약하게, 대신 잔잔한 보풀·구김
                        h = cloth * weaveAmt + fine * 0.35f + mid * (0.65f - weaveAmt);
                        t = 0.55f + (big - 0.5f) * 0.4f + (fine - 0.5f) * 0.08f;
                        break;
                    }
                    case Kind.Denim:
                    {
                        float d = Mathf.Sin((u + v) * weave * Mathf.PI * 2);
                        h = (d * 0.5f + 0.5f) * weaveAmt + fine * 0.35f + mid * (0.65f - weaveAmt);
                        float fade = Step(0.45f, 0.8f, Fbm(u, v, 3, 3, seed + 21));
                        t = 0.5f + (h - 0.5f) * 0.08f + fade * 0.3f + (fine - 0.5f) * 0.1f;
                        break;
                    }
                    case Kind.Knit:
                    {
                        float cu = u * weave * 0.5f, cv = v * weave * 0.5f;
                        float loop = Mathf.Abs(Mathf.Sin(cu * Mathf.PI * 2 + Mathf.Sin(cv * Mathf.PI * 2) * 0.8f));
                        h = loop;
                        t = 0.5f + (loop - 0.5f) * 0.35f + (big - 0.5f) * 0.3f;
                        break;
                    }
                    case Kind.Leather:
                    {
                        // 잔주름(셀 무늬) + 긁힌 자국 + 닳아 밝아진 곳
                        float cell = Cells(u, v, 56, seed + 2);
                        float scratch = Mathf.Pow(1 - Mathf.Abs(Fbm(u * 2f, v * 0.5f, 8, 2, seed + 13) * 2 - 1), 22);
                        float worn = Step(0.5f, 0.8f, Fbm(u, v, 4, 4, seed + 7));
                        h = cell * 0.6f + mid * 0.4f - scratch * 0.5f;
                        t = 0.45f + (big - 0.5f) * 0.4f + worn * 0.35f + scratch * 0.3f + (cell - 0.5f) * 0.04f;
                        break;
                    }
                    case Kind.Fur:
                    case Kind.Hair:
                    {
                        // 결을 따라 길게 늘어난 털 가닥 (v 방향)
                        int strands = kind == Kind.Fur ? 64 : 48;
                        float strand = Noise(u * strands, v * 4, strands, seed + 3) * 0.6f + Noise(u * strands * 2, v * 8, strands * 2, seed + 4) * 0.4f;
                        h = strand;
                        // 털 가닥 색 차이가 크면 멀리서 나뭇결 줄무늬처럼 보인다
                        t = 0.45f + (strand - 0.5f) * (kind == Kind.Fur ? 0.35f : 0.7f) + (big - 0.5f) * 0.4f;
                        break;
                    }
                    case Kind.Wood:
                    {
                        float ring = Mathf.Sin((u * 3 + Fbm(u, v, 4, 3, seed) * 2.5f) * Mathf.PI * 6);
                        h = ring * 0.3f + 0.5f + (fine - 0.5f) * 0.3f;
                        t = 0.5f + ring * 0.18f + (big - 0.5f) * 0.3f;
                        break;
                    }
                    case Kind.Metal:
                    {
                        float rust = Step(0.5f, 0.7f, Fbm(u, v, 4, 4, seed + 1));
                        h = rust * 0.6f + fine * 0.3f;
                        t = 0.6f - rust * 0.4f + (fine - 0.5f) * 0.1f;
                        break;
                    }
                    default:
                        h = fine * 0.5f + mid * 0.5f;
                        t = 0.5f + (big - 0.5f) * 0.3f;
                        break;
                }
                var c = Color.Lerp(b, a, Mathf.Clamp01(t * 1.25f - 0.1f));
                if (kind == Kind.Metal) c = Color.Lerp(c, new Color(0.42f, 0.2f, 0.1f), Step(0.5f, 0.7f, Fbm(u, v, 4, 4, seed + 1)) * 0.8f);

                // 때·흙먼지 (아래로 갈수록 짙게)
                float grime = Step(0.45f, 0.75f, Fbm(u, v, 3, 4, seed + 31)) * dirt;
                c = Color.Lerp(c, new Color(0.18f, 0.15f, 0.11f), grime * 0.55f);

                // 핏자국: 가장자리가 들쭉날쭉한 얼룩, 가운데는 검붉게 마르고 가장자리는 선홍
                for (int i = 0; i < stains.Length; i++)
                {
                    var st = stains[i];
                    float dx = Wrap(u - st.x), dy = Wrap(v - st.y) * 0.8f;
                    // 아래로 흘러내린 자국
                    float drip = dy < 0 ? Mathf.Clamp01(1 - Mathf.Abs(dx) / (st.z * 0.25f)) * Mathf.Clamp01(1 + dy / (st.z * 2.5f)) * (Noise(dx * 40 + i, 0, 64, seed + i) > 0.55f ? 1 : 0) : 0;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / st.z + (Fbm(u * 2, v * 2, 8, 3, seed + 40 + i) - 0.5f) * 0.9f;
                    float k = Mathf.Max(Step(1f, 0.7f, r), drip * 0.85f);
                    if (k <= 0) continue;
                    var bc = Color.Lerp(bloodFresh, bloodDark, Step(0.9f, 0.2f, r) * st.w + 0.3f);
                    c = Color.Lerp(c, bc, k * 0.92f);
                    h = Mathf.Lerp(h, 0.55f, k * 0.6f);
                }
                c.a = 1;
                col[y * size + x] = c;
                height[y * size + x] = h;
            }

        albedo = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "CharAlbedo_" + kind };
        albedo.SetPixels(col);
        albedo.Apply(true, true);

        // 높이 차이로 노멀맵을 만든다 (선형 색공간, RGB = 법선, A = 1)
        var nrm = new Color[size * size];
        // 천·털은 결이 너무 도드라지면 멀리서 줄무늬·바둑판처럼 깨져 보인다
        float strength = size / 64f * (kind == Kind.Cloth || kind == Kind.Denim ? 0.5f : kind == Kind.Fur ? 0.6f : 1f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float l = height[y * size + (x + size - 1) % size], r = height[y * size + (x + 1) % size];
                float d = height[((y + size - 1) % size) * size + x], u = height[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1).normalized;
                nrm[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
        normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "CharNormal_" + kind };
        normal.SetPixels(nrm);
        normal.Apply(true, true);
    }

    static float Wrap(float d) => d - Mathf.Round(d);

    // e0에서 e1로 갈수록 0에서 1로 부드럽게 (e0 > e1이면 거꾸로)
    static float Step(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3 - 2 * t);
    }

    // ---------- 이어지는(주기) 잡음 ----------

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    // period 칸마다 반복되는 값 잡음 (u, v는 0~1을 period칸으로 본다)
    static float Noise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int p = Mathf.Max(1, period);
        int ax = ((x0 % p) + p) % p, ay = ((y0 % p) + p) % p, bx = (ax + 1) % p, by = (ay + 1) % p;
        float a = Hash(ax, ay, seed), b = Hash(bx, ay, seed), c = Hash(ax, by, seed), d = Hash(bx, by, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        int p = period;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(u * p, v * p, p, seed + i * 13) * amp;
            norm += amp;
            amp *= 0.5f;
            p *= 2;
        }
        return sum / norm;
    }

    // 세포(보로노이) 무늬: 가죽 잔주름. 경계에서 0, 가운데에서 1에 가깝다
    static float Cells(float u, float v, int period, int seed)
    {
        float x = u * period, y = v * period;
        int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
        float d1 = 9, d2 = 9;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                int wx = ((gx % period) + period) % period, wy = ((gy % period) + period) % period;
                float px = gx + Hash(wx, wy, seed), py = gy + Hash(wx, wy, seed + 1);
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < d1) { d2 = d1; d1 = d; }
                else if (d < d2) d2 = d;
            }
        return Mathf.Clamp01((Mathf.Sqrt(d2) - Mathf.Sqrt(d1)) * 2.5f);
    }
}
