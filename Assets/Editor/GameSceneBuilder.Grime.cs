using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 제작대 강철(M_Steel)의 때: 밝은 강철 바탕에 손때·먼지·녹 점·잔 긁힘·기름 번짐·흘러내린 자국을 그린 색 그림과,
// 녹·때는 거칠고 기름은 번들거리게 하는 금속성(R)·매끈함(A) 그림을 만들어 입힌다 (Assets/Generated/Shelter).
// 재질만 바뀌므로 씬을 다시 만들지 않아도 된다. 게임 씬 전체 생성 때도 같이 만든다.
// 세게 그리면 핏자국·구멍처럼 보이므로(첫 시도) 모두 옅고 작게, 결 방향으로 둔다
public static partial class GameSceneBuilder
{
    const string GrimeDir = "Assets/Generated/Shelter";

    [MenuItem("Tools/핵폭발 생존/제작대 강철 때 입히기")]
    static void ApplySteelGrimeMenu()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameSceneBuilder] 플레이 모드에서는 재질을 바꾸지 않습니다.");
            return;
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Game/M_Steel.mat");
        if (m == null)
        {
            Debug.LogWarning("[GameSceneBuilder] M_Steel.mat이 없습니다. 게임 씬 전체 생성을 먼저 하세요.");
            return;
        }
        SteelGrime(m);
        AssetDatabase.SaveAssets();
        Debug.Log("[GameSceneBuilder] 제작대 강철에 때를 입혔습니다.");
    }

    // 1024×1024, 한 칸이 1.2m (BoxMesh의 tile → 1cm ≈ 8.5px). 가장자리를 넘어도 이어지는 노이즈라 되풀이해도 이음새가 없다
    static void SteelGrime(Material m)
    {
        const int N = 1024;
        var albedo = new Color32[N * N];
        var mask = new Color32[N * N];
        var rnd = new System.Random(2026);

        // 기름 번짐 (x, y, 가로 반지름, 세로 반지름) 셋과 흘러내린 자국 (x, 위 y, 길이, 폭) 넷. 칸 가장자리를 피한다
        var stains = new List<Vector4>();
        for (int i = 0; i < 3; i++)
            stains.Add(new Vector4(120 + (float)rnd.NextDouble() * (N - 240), 120 + (float)rnd.NextDouble() * (N - 240), 28 + (float)rnd.NextDouble() * 40, 14 + (float)rnd.NextDouble() * 22));
        var drips = new List<Vector4>();
        for (int i = 0; i < 4; i++)
            drips.Add(new Vector4(60 + (float)rnd.NextDouble() * (N - 120), 200 + (float)rnd.NextDouble() * (N - 260), 60 + (float)rnd.NextDouble() * 160, 1.5f + (float)rnd.NextDouble() * 2.5f));
        // 잔 긁힘: 대부분 결(가로) 방향으로 짧게, 몇 개는 비스듬히. +면 쇠가 드러난 밝은 선, -면 어두운 선
        var scratch = new float[N * N];
        for (int i = 0; i < 44; i++)
        {
            float x0 = 30 + (float)rnd.NextDouble() * (N - 60), y0 = 30 + (float)rnd.NextDouble() * (N - 60);
            float ang = rnd.NextDouble() < 0.7 ? ((float)rnd.NextDouble() - 0.5f) * 0.5f : (float)rnd.NextDouble() * Mathf.PI;
            float l = 10 + (float)rnd.NextDouble() * 60;
            float x1 = Mathf.Clamp(x0 + Mathf.Cos(ang) * l, 20, N - 20), y1 = Mathf.Clamp(y0 + Mathf.Sin(ang) * l, 20, N - 20);
            float sign = rnd.NextDouble() < 0.7 ? 1f : -1f, width = 0.5f + (float)rnd.NextDouble() * 0.9f;
            GrimeLine(scratch, N, x0, y0, x1, y1, width, sign);
        }

        var wipeColor = new Color(0.3f, 0.28f, 0.25f);
        var oilColor = new Color(0.12f, 0.1f, 0.08f);
        var rustDark = new Color(0.4f, 0.23f, 0.12f);
        var rustLight = new Color(0.58f, 0.36f, 0.18f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float nBase = GrimeFbm(x, y, N, 6, 4, 101);
                float nWipe = GrimeFbm(x, y, N, 3, 3, 202);
                float nRust = GrimeFbm(x, y, N, 14, 4, 303);
                float nRustArea = GrimeFbm(x, y, N, 2, 3, 404);
                float nSpeck = GrimeNoise(x, y, N, 96, 505);
                float brush = GrimeHash(y, x / 9, 55) - 0.5f; // 가로로 간 미세한 결
                float v = 0.57f + (nBase - 0.5f) * 0.14f + brush * 0.04f;
                var c = new Color(v, v * 1.01f, v * 1.03f);
                // 손때·걸레 자국 (넓고 옅게), 먼지 알갱이 (작고 드문드문)
                float wipe = Smooth01(0.48f, 0.8f, nWipe) * 0.4f;
                c = Color.Lerp(c, wipeColor, wipe);
                float speck = Smooth01(0.8f, 0.95f, nSpeck) * 0.35f;
                c = Color.Lerp(c, wipeColor, speck);
                // 녹 점: 몰린 자리에만, 둘레에 옅은 녹물
                float area = Smooth01(0.48f, 0.75f, nRustArea);
                float rust = Smooth01(0.64f, 0.76f, nRust) * area;
                float halo = Smooth01(0.56f, 0.66f, nRust) * area * 0.35f;
                c = Color.Lerp(c, rustLight, halo);
                c = Color.Lerp(c, Color.Lerp(rustDark, rustLight, nBase), Mathf.Clamp01(rust * 1.2f));
                float metallic = 0.5f * (1 - wipe * 0.6f) * (1 - rust) * (1 - speck);
                float smooth = 0.56f * (1 - wipe * 0.5f) * (1 - rust * 0.9f) * (1 - speck * 0.5f) + (nBase - 0.5f) * 0.08f;

                foreach (var s in stains)
                {
                    float dx = Wrap(x - s.x, N) / s.z, dy = Wrap(y - s.y, N) / s.w;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * (0.85f + 0.3f * nRust);
                    float a = Smooth01(1.1f, 0.5f, d) * 0.5f;
                    if (a <= 0) continue;
                    c = Color.Lerp(c, oilColor, a);
                    smooth = Mathf.Lerp(smooth, 0.72f, Mathf.Clamp01(a * 1.6f));
                    metallic *= 1 - Mathf.Clamp01(a * 1.5f);
                }
                foreach (var d in drips)
                {
                    float dx = Mathf.Abs(Wrap(x - d.x, N)) + (nRust - 0.5f) * 2f, dy = d.y - y;
                    if (dy < 0 || dy > d.z) continue;
                    float a = Smooth01(d.w, d.w * 0.3f, dx) * (1 - dy / d.z) * 0.3f;
                    if (a <= 0) continue;
                    c = Color.Lerp(c, wipeColor, a);
                    smooth *= 1 - a * 0.4f;
                }
                float sc = scratch[y * N + x];
                if (sc != 0)
                {
                    float a = Mathf.Min(1f, Mathf.Abs(sc)) * 0.55f;
                    c = Color.Lerp(c, sc > 0 ? new Color(0.72f, 0.73f, 0.75f) : new Color(0.3f, 0.29f, 0.27f), a);
                    if (sc > 0) { smooth = Mathf.Max(smooth, a * 0.7f); metallic = Mathf.Max(metallic, a * 0.6f); }
                    else smooth *= 1 - a * 0.4f;
                }

                int k = y * N + x;
                albedo[k] = new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 255);
                mask[k] = new Color32((byte)(Mathf.Clamp01(metallic) * 255), 0, 0, (byte)(Mathf.Clamp01(smooth) * 255));
            }

        var albedoTex = SaveGrime("steel_grime_albedo", albedo, N, true);
        var maskTex = SaveGrime("steel_grime_mask", mask, N, false);
        m.SetTexture("_BaseMap", albedoTex);
        m.SetTexture("_MainTex", albedoTex);
        m.SetTexture("_MetallicGlossMap", maskTex);
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_SmoothnessTextureChannel", 0);
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_Metallic", 1f);
        m.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.85f));
        m.SetColor("_Color", new Color(0.86f, 0.86f, 0.85f));
        m.SetFloat("_BumpScale", 0.8f);
        EditorUtility.SetDirty(m);
    }

    // PNG로 저장하고 가져오기 설정을 맞춘 뒤 텍스처를 돌려준다 (색 그림은 sRGB, 금속성·매끈함 그림은 선형·무압축)
    static Texture2D SaveGrime(string name, Color32[] px, int n, bool color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GrimeDir)) AssetDatabase.CreateFolder("Assets/Generated", "Shelter");
        string path = $"{GrimeDir}/{name}.png";
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = color;
            imp.alphaSource = color ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 1024;
            imp.textureCompression = color ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 선을 두께 width로 그린다 (값 sign: +면 밝은 긁힘, -면 어두운 긁힘)
    static void GrimeLine(float[] layer, int n, float x0, float y0, float x1, float y1, float width, float sign)
    {
        var a = new Vector2(x0, y0);
        var d = new Vector2(x1 - x0, y1 - y0);
        float len2 = Mathf.Max(1e-6f, d.sqrMagnitude), half = width / 2;
        for (int y = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - half - 1)); y <= Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + half + 1)); y++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - half - 1)); x <= Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + half + 1)); x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float k = Mathf.Clamp01(Vector2.Dot(p - a, d) / len2);
                float dist = Vector2.Distance(p, a + d * k);
                float cov = Mathf.Clamp01(half - dist + 0.5f);
                if (cov > 0) layer[y * n + x] = sign * Mathf.Max(Mathf.Abs(layer[y * n + x]), cov);
            }
    }

    static float Wrap(float d, int n)
    {
        if (d > n / 2) d -= n;
        else if (d < -n / 2) d += n;
        return d;
    }

    static float Smooth01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3 - 2 * t);
    }

    // 되풀이되는 값 노이즈를 octaves겹 쌓은 것 (0~1쯤)
    static float GrimeFbm(int x, int y, int period, int cells, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, total = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += GrimeNoise(x, y, period, cells << o, seed + o * 17) * amp;
            total += amp;
            amp *= 0.5f;
        }
        return sum / total;
    }

    static float GrimeNoise(int x, int y, int period, int cells, int seed)
    {
        float fx = (float)x / period * cells, fy = (float)y / period * cells;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        int xa = ((x0 % cells) + cells) % cells, xb = (((x0 + 1) % cells) + cells) % cells;
        int ya = ((y0 % cells) + cells) % cells, yb = (((y0 + 1) % cells) + cells) % cells;
        float a = GrimeHash(xa, ya, seed), b = GrimeHash(xb, ya, seed), c = GrimeHash(xa, yb, seed), d = GrimeHash(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    static float GrimeHash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
    }
}
