using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// GameSceneBuilder의 한국 길거리 개편 공통 부분 (맵 개편 세션):
//  - 재질·그림: 보도블록(I자 맞물림 블록, 초록·회색 두 톤), 화강석 연석, 점자블록, 차선 페인트(벗겨짐 알파), 빗물받이 격자, 맨홀 뚜껑, 녹색 철제 울타리, 콘크리트 전봇대
//  - 글자 그리기: 글꼴 글자판을 복사해 간판·표지판 그림에 글자를 찍는다 (Pretendard·Do Hyeon)
//  - 메시 도우미: 윗면 UV를 블록 좌표에 맞춘 판(이음새에서 무늬가 이어진다), 회전체(전봇대·나무 줄기), 납작한 다각형(화살표), 기본 도형 합치기(전선·울타리)
// 모든 그림은 Assets/Generated/City에 PNG로 저장한다. 사진 속 상호·브랜드는 쓰지 않는다 (팀 31 → '31'·'삼일' 기준 이름)
public static partial class GameSceneBuilder
{
    // ---------- 재질 ----------

    static void KoreaMaterials()
    {
        KoreaTextures(out var paverA, out var paverN, out var granite, out var tactile, out var paint, out var grate, out var manhole, out var busSign);
        M["paver"] = KLitMat("M_StreetPaver", paverA, paverN, Color.white, 0.18f, 0f, 0.9f);
        M["granite"] = KLitMat("M_StreetGranite", granite, null, Color.white, 0.32f, 0f);
        M["tactile"] = KLitMat("M_StreetTactile", tactile, null, Color.white, 0.14f, 0f);
        M["grate"] = KLitMat("M_StreetGrate", grate, null, Color.white, 0.3f, 0.45f);
        M["manhole"] = KLitMat("M_StreetManhole", manhole, null, Color.white, 0.28f, 0.5f);
        M["busSign"] = KLitMat("M_BusStopSign", busSign, null, Color.white, 0.25f, 0.1f);
        // 차선 페인트: 알파로 벗겨진 자리를 뚫는다 (아래 아스팔트가 보인다)
        M["paintW"] = KPaintMat("M_RoadPaintWhite", paint, new Color(0.82f, 0.82f, 0.8f));
        M["paintY"] = KPaintMat("M_RoadPaintYellow", paint, new Color(0.78f, 0.58f, 0.12f));
        M["fenceGreen"] = TexMat("M_StreetFence", "rusty_metal_02", new Color(0.1f, 0.26f, 0.14f), 0.3f, 0.35f);
        M["poleConcrete"] = TexMat("M_PolePost", "dirty_concrete", new Color(0.58f, 0.58f, 0.56f), 0.08f);
        M["cable"] = TexMat("M_Cable", null, new Color(0.045f, 0.045f, 0.045f), 0.3f);
        M["insulator"] = TexMat("M_Insulator", null, new Color(0.72f, 0.72f, 0.7f), 0.55f);
        M["steelGrey"] = TexMat("M_SteelGrey", "rusty_metal_02", new Color(0.42f, 0.44f, 0.45f), 0.38f, 0.5f);
        M["bark"] = TexMat("M_Bark", "wood_planks_dirt", new Color(0.24f, 0.19f, 0.15f), 0.08f);
        M["treeGrate"] = TexMat("M_TreeGrate", "rusty_metal_02", new Color(0.3f, 0.3f, 0.3f), 0.25f, 0.4f);
        M["lampHead"] = TexMat("M_LampHead", null, new Color(0.3f, 0.3f, 0.3f), 0.4f, 0.4f);
        M["shopGlass"] = TransparentMat("M_ShopGlass", new Color(0.55f, 0.65f, 0.72f, 0.3f), 0.92f); // 비치는 유리 (상점·버스 정류장, GameSceneBuilder.Shelter.cs의 TransparentMat)
        // 아스팔트에도 금 디테일 맵을 얹는다 (벽과 같은 그림, 더 넓게)
        if (M.TryGetValue("asphalt", out var asphalt) && asphalt != null && !KKeep(asphalt)) // 도시 씬만 생성: 있는 M_Asphalt는 그대로
        {
            var detail = AssetDatabase.LoadAssetAtPath<Texture2D>(CityGenDir + "/wall_cracks_detail.png");
            var detailN = AssetDatabase.LoadAssetAtPath<Texture2D>(CityGenDir + "/wall_cracks_normal.png");
            if (detail != null)
            {
                asphalt.SetTexture("_DetailAlbedoMap", detail);
                asphalt.SetTexture("_DetailNormalMap", detailN);
                asphalt.SetFloat("_DetailAlbedoMapScale", 0.8f);
                asphalt.SetFloat("_DetailNormalMapScale", 0.6f);
                asphalt.SetTextureScale("_DetailAlbedoMap", new Vector2(0.18f, 0.18f));
                asphalt.EnableKeyword("_DETAIL_MULX2");
                asphalt.DisableKeyword("_DETAIL_SCALED");
                EditorUtility.SetDirty(asphalt);
            }
        }
    }

    static Material KLitMat(string name, Texture2D albedo, Texture2D normal, Color tint, float smoothness, float metallic, float bumpScale = 1f)
    {
        var m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
        if (KKeep(m)) return m;
        m.SetTexture("_BaseMap", albedo);
        m.SetTexture("_MainTex", albedo);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", bumpScale);
            m.EnableKeyword("_NORMALMAP");
        }
        else
        {
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
        }
        m.SetColor("_BaseColor", tint);
        m.SetColor("_Color", tint);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 알파 클립 페인트: 그림의 알파가 0.5 아래면 그리지 않는다
    static Material KPaintMat(string name, Texture2D tex, Color tint)
    {
        var m = KLitMat(name, tex, null, tint, 0.2f, 0f);
        if (KKeep(m)) return m;
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- 그림 ----------

    // 그림을 전부 먼저 그린 뒤 한꺼번에 PNG로 들여온다 (들여오는 도중 글꼴 글자판이 다시 만들어져 글자가 빠지지 않게)
    static void KoreaTextures(out Texture2D paverA, out Texture2D paverN, out Texture2D granite, out Texture2D tactile, out Texture2D paint, out Texture2D grate, out Texture2D manhole, out Texture2D busSign)
    {
        PaverPixels(out var pa, out var pn, out int paverSize);
        var gr = GranitePixels(out int graniteSize);
        var ta = TactilePixels(out int tactileSize);
        var pt = PaintPixels(out int paintSize);
        var gt = GratePixels(out int grateSize);
        var mh = ManholePixels(out int manholeSize);
        var bs = BusSignPixels(out int bsW, out int bsH);
        paverA = KSaveTex("street_paver", pa, paverSize, paverSize, KTexKind.Color);
        paverN = KSaveTex("street_paver_normal", pn, paverSize, paverSize, KTexKind.Normal);
        granite = KSaveTex("street_granite", gr, graniteSize, graniteSize, KTexKind.Color);
        tactile = KSaveTex("street_tactile", ta, tactileSize, tactileSize, KTexKind.Color);
        paint = KSaveTex("street_paint", pt, paintSize, paintSize, KTexKind.ColorAlpha);
        grate = KSaveTex("street_grate", gt, grateSize, grateSize, KTexKind.Color);
        manhole = KSaveTex("street_manhole", mh, manholeSize, manholeSize, KTexKind.Color);
        busSign = KSaveTex("street_bus_sign", bs, bsW, bsH, KTexKind.Color, false);
        KReleaseFonts();
    }

    enum KTexKind { Color, ColorAlpha, Normal, Linear }

    // coverage: 알파 클립 그림 (창틀 카드). 밉맵에서도 알파 0.5를 넘는 픽셀 비율을 지켜 가는 막대가 멀리서 사라지지 않게 한다
    // maxSize: 들여오는 최대 크기. 그림을 그보다 크게 그렸으면 줄여서 들여온다 (창틀 카드는 1024로 그리고 512로 들여온다. 벙커 정면은 가까이서 흐려져 1024 그대로, 11-20)
    static Texture2D KSaveTex(string name, Color32[] px, int w, int h, KTexKind kind, bool repeat = true, bool coverage = false, int maxSize = 1024, string limitGroup = "")
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(CityGenDir)) AssetDatabase.CreateFolder("Assets/Generated", "City");
        string path = $"{CityGenDir}/{name}.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        var png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        if (KWriteIfChanged(path, png)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); // 같은 그림이면 다시 쓰지도 들여오지도 않는다
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.textureType = kind == KTexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = kind == KTexKind.Color || kind == KTexKind.ColorAlpha;
            imp.alphaSource = kind == KTexKind.ColorAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.alphaIsTransparency = false;
            imp.mipmapEnabled = true;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 6;
            imp.maxTextureSize = maxSize;
            imp.mipmapLimitGroupName = limitGroup; // 밉맵 단계 묶음 ('매우 높음' 전용 2048은 HighRes)
            imp.textureCompression = kind == KTexKind.Linear ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            if (coverage)
            {
                imp.mipMapsPreserveCoverage = true;
                imp.alphaTestReferenceValue = 0.5f;
            }
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Color32 KC(Color c) => new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), (byte)(Mathf.Clamp01(c.a) * 255));

    // 높이 그림 → 노멀 맵 (되풀이되는 그림 기준)
    static Color32[] KNormalFromHeight(float[] h, int n, float strength)
    {
        var nrm = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float l = h[y * n + (x + n - 1) % n], r = h[y * n + (x + 1) % n];
                float d = h[((y + n - 1) % n) * n + x], u = h[((y + 1) % n) * n + x];
                var v = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                nrm[y * n + x] = new Color32((byte)((v.x * 0.5f + 0.5f) * 255), (byte)((v.y * 0.5f + 0.5f) * 255), (byte)((v.z * 0.5f + 0.5f) * 255), 255);
            }
        return nrm;
    }

    // 보도블록: I자(아이형) 맞물림 블록. 1024px = 2m. 블록 25×15cm, 줄마다 반 칸씩 어긋나고 어깨가 옆 줄의 허리에 물린다.
    // 줄 셋 중 하나는 밝은 회색, 나머지는 바랜 초록. 줄눈은 어둡고 모래가 끼었다. 몇 개는 내려앉거나 빠져 흙이 보인다. 먼지·얼룩으로 바랬다
    static void PaverPixels(out Color32[] albedo, out Color32[] normal, out int size)
    {
        const int N = 1024, L = 128, P = 64, D = 14, W = P + D, E = L / 4;
        const float JW = 2.4f;
        size = N;
        var rnd = new System.Random(3131);
        // 블록 번호 (어느 블록 안인가, erode만큼 안쪽으로 줄인 모양 기준). 없으면 -1
        long Block(float x, float y, float erode)
        {
            int r0 = Mathf.FloorToInt(y / P);
            for (int r = r0 - 1; r <= r0 + 1; r++)
            {
                float off = (r & 1) != 0 ? L / 2f : 0f;
                float v = y - r * P - P / 2f;
                int i0 = Mathf.FloorToInt((x - off) / L);
                for (int i = i0 - 1; i <= i0 + 1; i++)
                {
                    float u = x - off - i * L;
                    if (u < erode || u > L - erode) continue;
                    bool shoulder = u < E - erode || u > L - E + erode;
                    float half = (shoulder ? W / 2f : W / 2f - D) - erode;
                    if (Mathf.Abs(v) <= half)
                    {
                        int rr = ((r % (N / P)) + N / P) % (N / P), ii = ((i % (N / L)) + N / L) % (N / L);
                        return (long)rr * 1000 + ii;
                    }
                }
            }
            return -1;
        }
        var height = new float[N * N];
        var px = new Color32[N * N];
        var green = new Color(0.36f, 0.45f, 0.37f);
        var grey = new Color(0.6f, 0.6f, 0.58f);
        var jointC = new Color(0.2f, 0.19f, 0.17f);
        var sand = new Color(0.4f, 0.37f, 0.32f);
        var earth = new Color(0.17f, 0.14f, 0.11f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int k = y * N + x;
                long id = Block(x + 0.5f, y + 0.5f, JW);
                float dust = GrimeFbm(x, y, N, 4, 4, 707);
                float fine = GrimeFbm(x, y, N, 48, 3, 808);
                float stain = GrimeFbm(x, y, N, 3, 3, 909);
                if (id < 0)
                {
                    var jc = Color.Lerp(jointC, sand, Smooth01(0.45f, 0.7f, fine) * 0.7f);
                    px[k] = KC(jc * (0.85f + 0.3f * dust));
                    height[k] = 0f;
                    continue;
                }
                int row = (int)(id / 1000), col = (int)(id % 1000);
                float hb = GrimeHash(row, col, 55);
                float hb2 = GrimeHash(col, row, 66);
                bool sunken = hb2 > 0.985f;
                // 들쭉날쭉한 깊이(블록 가장자리의 모따기) : 안쪽으로 1.2px씩 더 줄여 보며 몇 번이나 안에 남는가
                float hgt = 0f;
                for (int s = 0; s < 6; s++)
                    if (Block(x + 0.5f, y + 0.5f, JW + s * 1.25f) == id) hgt += 1f / 6f;
                var tone = row % 3 == 2 ? grey : green;
                var c = Color.Lerp(tone, tone * 1.15f, hb * 0.5f);
                c = Color.Lerp(c, new Color(0.5f, 0.47f, 0.4f), (hb2 - 0.5f) * 0.12f);
                c *= 0.72f + 0.28f * fine * 0.5f + 0.18f * hb;
                c = Color.Lerp(c, new Color(0.42f, 0.4f, 0.36f), Smooth01(0.5f, 0.8f, dust) * 0.45f); // 먼지
                c = Color.Lerp(c, new Color(0.14f, 0.13f, 0.12f), Smooth01(0.66f, 0.85f, stain) * 0.5f); // 얼룩
                c *= 0.8f + 0.2f * hgt; // 가장자리 모따기 어둡게
                if (sunken) { c = Color.Lerp(earth, c, 0.35f); hgt = 0.1f; }
                px[k] = KC(c);
                height[k] = sunken ? 0.1f : 0.25f + 0.75f * hgt + (fine - 0.5f) * 0.08f;
            }
        albedo = px;
        normal = KNormalFromHeight(height, N, 3.2f);
    }

    // 화강석 연석: 512px = 1m. 잿빛 알갱이에 흰 점·검은 점, 1m마다 어두운 이음새, 아래쪽은 때가 탔다
    static Color32[] GranitePixels(out int size)
    {
        const int N = 512;
        size = N;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float n1 = GrimeFbm(x, y, N, 16, 4, 121);
                float n2 = GrimeNoise(x, y, N, 256, 232);
                float n3 = GrimeNoise(x, y, N, 128, 343);
                float v = 0.5f + (n1 - 0.5f) * 0.2f;
                if (n2 > 0.86f) v += 0.22f;
                if (n3 < 0.12f) v -= 0.2f;
                var c = new Color(v, v * 0.99f, v * 0.96f);
                float grime = GrimeFbm(x, y, N, 3, 3, 454);
                c = Color.Lerp(c, new Color(0.3f, 0.29f, 0.26f), Smooth01(0.55f, 0.85f, grime) * 0.5f);
                if (x < 3 || x >= N - 3) c *= 0.45f; // 이음새
                px[y * N + x] = KC(c);
            }
        return px;
    }

    // 점자블록(점형): 256px = 0.3m. 바랜 노랑에 5×5 돌기, 돌기는 위가 밝고 아래가 어둡다
    static Color32[] TactilePixels(out int size)
    {
        const int N = 256;
        size = N;
        var px = new Color32[N * N];
        var yellow = new Color(0.78f, 0.62f, 0.12f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dirt = GrimeFbm(x, y, N, 3, 4, 565);
                var c = yellow * (0.62f + 0.3f * GrimeFbm(x, y, N, 24, 3, 676));
                c = Color.Lerp(c, new Color(0.3f, 0.26f, 0.18f), Smooth01(0.5f, 0.85f, dirt) * 0.55f);
                float cx = ((x % 51) + 51) % 51 - 25.5f, cy = ((y % 51) + 51) % 51 - 25.5f;
                float d = Mathf.Sqrt(cx * cx + cy * cy);
                if (d < 17f)
                {
                    float shade = Mathf.Clamp(((cy - cx) / 17f) * 0.5f, -0.5f, 0.5f);
                    c *= 1.05f + shade * 0.5f;
                    if (d > 14.5f) c *= 0.82f;
                }
                px[y * N + x] = KC(c);
            }
        return px;
    }

    // 차선 페인트: 512px = 1.5m. 색은 때 묻은 흰색(틴트로 노랗게도 쓴다), 알파 0 = 벗겨져 아스팔트가 보이는 자리 (금을 따라 조각조각)
    static Color32[] PaintPixels(out int size)
    {
        const int N = 512;
        size = N;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float g = 0.78f + 0.22f * GrimeFbm(x, y, N, 6, 4, 787);
                float chips = GrimeFbm(x, y, N, 10, 4, 898);
                float crack = Mathf.Abs(GrimeFbm(x, y, N, 5, 3, 919) - 0.5f);
                bool gone = chips > 0.64f || crack < 0.011f;
                var c = new Color(g, g, g * 0.98f, gone ? 0f : 1f);
                if (chips > 0.56f && !gone) c *= 0.88f; // 벗겨지기 직전의 때
                px[y * N + x] = KC(c);
            }
        return px;
    }

    // 빗물받이 격자: 256px = 0.5m. 어두운 주철 틀에 세로 홈 여섯
    static Color32[] GratePixels(out int size)
    {
        const int N = 256;
        size = N;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float n = GrimeFbm(x, y, N, 12, 3, 1010);
                var c = new Color(0.22f, 0.21f, 0.2f) * (0.8f + 0.4f * n);
                c = Color.Lerp(c, new Color(0.35f, 0.2f, 0.1f), Smooth01(0.6f, 0.9f, GrimeFbm(x, y, N, 4, 3, 1111)) * 0.5f); // 녹
                int sx = x % 42;
                bool slot = sx >= 14 && sx < 30 && y >= 24 && y < N - 24;
                if (slot) c = new Color(0.025f, 0.025f, 0.025f);
                else if (x < 8 || x >= N - 8 || y < 8 || y >= N - 8) c *= 1.1f;
                px[y * N + x] = KC(c);
            }
        return px;
    }

    // 맨홀 뚜껑: 256px, 원 안에 테두리·동심원·방사선 홈 (원 밖은 안 쓴다 — 원판 메시에 입힌다)
    static Color32[] ManholePixels(out int size)
    {
        const int N = 256;
        size = N;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - N / 2f + 0.5f, dy = y - N / 2f + 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / (N / 2f);
                float a = Mathf.Atan2(dy, dx);
                float n = GrimeFbm(x, y, N, 10, 3, 1212);
                var c = new Color(0.27f, 0.25f, 0.23f) * (0.8f + 0.4f * n);
                c = Color.Lerp(c, new Color(0.4f, 0.22f, 0.1f), Smooth01(0.55f, 0.85f, GrimeFbm(x, y, N, 3, 3, 1313)) * 0.45f);
                if (r > 0.93f) c *= 1.15f;
                else if (r > 0.88f) c *= 0.55f;
                else if (Mathf.Abs(r - 0.55f) < 0.02f || Mathf.Abs(r - 0.25f) < 0.02f) c *= 0.55f;
                else if (r > 0.25f && r < 0.88f && Mathf.Abs(Mathf.Repeat(a, Mathf.PI / 6f) - Mathf.PI / 12f) < 0.035f) c *= 0.6f;
                px[y * N + x] = KC(c);
            }
        return px;
    }

    // 버스 정류장 표지판 (512×256): 흰 바탕 위 파란 띠에 '버스' , 정류장 이름과 노선 번호 31·삼일
    static Color32[] BusSignPixels(out int w, out int h)
    {
        w = 512; h = 256;
        var px = new Color[w * h];
        var white = new Color(0.86f, 0.86f, 0.83f);
        var blue = new Color(0.1f, 0.3f, 0.6f);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float grime = GrimeFbm(i, j, 512, 4, 3, 1414);
                var c = j > h * 0.62f ? blue : white;
                c = Color.Lerp(c, new Color(0.35f, 0.33f, 0.3f), Smooth01(0.55f, 0.9f, grime) * 0.45f);
                if (i < 6 || i >= w - 6 || j < 6 || j >= h - 6) c = new Color(0.2f, 0.2f, 0.2f);
                px[j * w + i] = c;
            }
        var pretendard = KFontFor("Fonts/Pretendard-Bold", "버스정류장삼일시장31·0123456789");
        var dohyeon = KFontFor("Fonts/DoHyeon-Regular", "버스정류장삼일시장31·0123456789");
        KText(px, w, h, dohyeon, "버스 정류장", 64, w * 0.5f, h * 0.81f, Color.white, 0.5f);
        KText(px, w, h, dohyeon, "삼일시장", 72, w * 0.5f, h * 0.36f, new Color(0.12f, 0.12f, 0.12f), 0.5f);
        KText(px, w, h, pretendard, "31 · 310 · 3131", 32, w * 0.5f, h * 0.12f, new Color(0.2f, 0.3f, 0.5f), 0.5f);
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // ---------- 글자 ----------

    class KFont
    {
        public Font font;
        public Texture2D atlas;
        public bool red;
        public HashSet<int> inCopy = new HashSet<int>(); // 복사본에 들어 있는 (글자, 크기) 조합: ch * 1000 + size
        public Color32[] pix; // 복사본의 픽셀 (KText가 텍셀을 정수 자리로 바로 읽는다)
        public int pw, ph;
    }

    static readonly Dictionary<string, KFont> kFonts = new Dictionary<string, KFont>();
    // 글자판(동적 글꼴 텍스처)에 올리는 가장 큰 글자 크기 (px). 도시 간판처럼 더 큰 글자는 이 크기로 올린 모양을 KText가 늘려 그린다.
    // 글꼴 텍스처 상한은 4096×4096이고, 넘치면 에디터가 죽는다 (2026-11-06 00:47 크래시 로그: "do not fit onto a single texture (max size 4096)")
    const int KTextMaxRequest = 72;
    // 늘려 그린 글자의 가장자리 기울기 배율 (× 늘린 배율 k). 1.5면 큰 크기로 바로 요청한 글자와 화면의 가장자리 폭이 거의 같다 (오프라인 비교)
    const float KTextEdgeGain = 1.5f;

    // 글꼴을 읽는다. 글자판 요청은 KText가 그리기 직전에 필요한 글자·크기만 하고(동적 글꼴 텍스처 상한 4096 — 상호 전체를 큰 크기로 미리 요청하면 넘쳐 에디터가 죽는다),
    // 글꼴 텍스처가 다시 만들어지면(Font.textureRebuilt) 그 글꼴의 읽을 수 있는 복사본만 새로 뜬다. sizes가 주어지면 그 크기로 chars를 미리 올려 둔다
    static readonly HashSet<Font> kRebuiltFonts = new HashSet<Font>();
    static bool kFontHooked;

    static KFont KFontFor(string resName, string chars, int[] sizes = null)
    {
        if (kFonts.TryGetValue(resName, out var cached)) return cached;
        if (!kFontHooked) { Font.textureRebuilt += f => kRebuiltFonts.Add(f); kFontHooked = true; }
        var kf = new KFont { font = Resources.Load<Font>(resName) };
        kFonts[resName] = kf;
        if (kf.font == null)
        {
            Debug.LogWarning("[GameSceneBuilder] 글꼴이 없어 글자를 생략합니다: " + resName);
            return kf;
        }
        if (sizes != null)
            foreach (int size in sizes)
                kf.font.RequestCharactersInTexture(chars + " 0123456789.·-:/()", Mathf.Min(size, KTextMaxRequest), FontStyle.Normal); // 미리 요청도 상한까지만
        KCopyAtlas(kf);
        if (sizes != null)
            foreach (int size in sizes)
                foreach (char ch in chars + " 0123456789.·-:/()") kf.inCopy.Add(ch * 1000 + Mathf.Min(size, KTextMaxRequest));
        return kf;
    }

    // GPU에만 있는 글꼴 글자판을 읽을 수 있는 복사본으로 뜬다 (R 채널에 글자가 있는 글자판인지도 알아낸다)
    static void KCopyAtlas(KFont kf)
    {
        if (kf.atlas != null) { Object.DestroyImmediate(kf.atlas); kf.atlas = null; }
        kf.pix = null;
        if (kRebuiltFonts.Contains(kf.font)) kf.inCopy.Clear(); // 다시 만들어진 글자판은 옛 글자를 버렸을 수 있다
        kRebuiltFonts.Remove(kf.font);
        var src = kf.font != null && kf.font.material != null ? kf.font.material.mainTexture : null;
        if (src == null) return;
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        kf.atlas = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true) { name = "KoreaFontAtlas" };
        kf.atlas.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        kf.atlas.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var all32 = kf.atlas.GetPixels32();
        double sumA = 0, sumR = 0;
        foreach (var p in all32) { sumA += p.a; sumR += p.r; }
        kf.red = sumA / all32.Length > 150 && sumR < sumA;
        kf.pix = all32; kf.pw = src.width; kf.ph = src.height;
    }

    // 글자 칸 (gw×gh) 안의 텍셀 (u, v)의 덮임 값. 칸 밖은 0 (글자판의 1px 여백과 같다).
    // (ox, oy) = 칸 왼쪽 아래 텍셀, (exx, exy)·(eyx, eyy) = 글자의 x·y 한 칸이 글자판에서 움직이는 방향 (90° 돌려 넣은 글자도 된다)
    static float KGlyphTexel(KFont kf, int gw, int gh, int ox, int oy, int exx, int exy, int eyx, int eyy, int u, int v)
    {
        if (kf.pix == null || u < 0 || v < 0 || u >= gw || v >= gh) return 0f;
        int x = ox + u * exx + v * eyx, y = oy + u * exy + v * eyy;
        if (x < 0 || y < 0 || x >= kf.pw || y >= kf.ph) return 0f;
        var p = kf.pix[y * kf.pw + x];
        return (kf.red ? p.r : p.a) / 255f;
    }

    static void KReleaseFonts()
    {
        foreach (var kf in kFonts.Values)
            if (kf.atlas != null) Object.DestroyImmediate(kf.atlas);
        kFonts.Clear();
    }

    // 글자를 픽셀에 직접 그린다. (x, cy): 기준점, align 0 왼쪽 · 0.5 가운데 · 1 오른쪽. 세로는 글자 높이의 가운데. 폭을 돌려준다.
    // maxRequest > 0이고 size가 그보다 크면 글자판에는 maxRequest 크기로만 올리고(동적 글꼴 텍스처를 작게 유지), 그 모양을 k = size/req배로
    // 늘려(쌍선형) 그린 뒤 가장자리 기울기를 KTextEdgeGain·k배로 다시 세운다. 도시의 큰 간판 글자는 KTextMaxRequest를 넘긴다.
    // maxRequest를 주지 않거나 size가 그 이하이면 k = 1이라 계산이 예전과 비트까지 같다 (방공호 시계·달력은 그대로)
    static float KText(Color[] px, int w, int h, KFont kf, string text, int size, float x, float cy, Color color, float align, int maxRequest = 0)
    {
        if (kf == null || kf.font == null) return 0f;
        int req = maxRequest > 0 ? Mathf.Min(size, maxRequest) : size; // 글자판에 올리는 크기
        float k = size / (float)req;                                       // 그릴 때 늘리는 배율 (1이면 예전과 같다)
        kf.font.RequestCharactersInTexture(text, req, FontStyle.Normal); // 이 글자·크기만 글자판에 올린다
        // 새 글자가 빈자리에 들어가면 textureRebuilt가 오지 않으므로, 복사본에 없는 (글자,크기)가 하나라도 있으면 다시 뜬다
        bool fresh = kf.atlas != null && !kRebuiltFonts.Contains(kf.font);
        if (fresh) foreach (char ch in text) if (!kf.inCopy.Contains(ch * 1000 + req)) { fresh = false; break; }
        if (!fresh) KCopyAtlas(kf);
        if (kf.atlas == null) return 0f;
        foreach (char ch in text) kf.inCopy.Add(ch * 1000 + req);
        float width = 0, top = float.MinValue, bottom = float.MaxValue;
        foreach (char ch in text)
        {
            if (!kf.font.GetCharacterInfo(ch, out CharacterInfo ci, req, FontStyle.Normal)) continue;
            width += ci.advance * k;
            if (ci.maxY > ci.minY)
            {
                top = Mathf.Max(top, ci.maxY * k);
                bottom = Mathf.Min(bottom, ci.minY * k);
            }
        }
        if (top < bottom) return 0f;
        float pen = Mathf.Round(x - width * align), baseline = Mathf.Round(cy - (top + bottom) / 2f);
        float gain = KTextEdgeGain * k; // 늘린 글자는 가장자리가 k배 넓게 번진다 → 기울기를 그만큼 세운다
        foreach (char ch in text)
        {
            if (!kf.font.GetCharacterInfo(ch, out CharacterInfo ci, req, FontStyle.Normal)) continue;
            float gx0 = pen + ci.minX * k, gx1 = pen + ci.maxX * k, gy0 = baseline + ci.minY * k, gy1 = baseline + ci.maxY * k;
            int gw = ci.maxX - ci.minX, gh = ci.maxY - ci.minY;
            if (gx1 > gx0 && gy1 > gy0 && gw > 0 && gh > 0)
            {
                // 글자 칸을 글자판 텍셀에 맞춘다: 모서리 UV에서 정수 원점과 한 칸 방향을 구하고, 칸 안 자리는 글자 기준 좌표(배정밀도)로만 계산한다.
                // 글자가 글자판 어디에 놓였든, 글자판 크기가 얼마든 같은 픽셀이 나온다 (예전 GetPixelBilinear는 놓인 자리에 따라 끝자리가 흔들려
                // 다시 만들 때마다 간판 PNG 몇 픽셀이 한 단계씩 달라졌다 — 11-16 확인)
                int exx = Mathf.RoundToInt((ci.uvBottomRight.x - ci.uvBottomLeft.x) * kf.pw / gw), exy = Mathf.RoundToInt((ci.uvBottomRight.y - ci.uvBottomLeft.y) * kf.ph / gw);
                int eyx = Mathf.RoundToInt((ci.uvTopLeft.x - ci.uvBottomLeft.x) * kf.pw / gh), eyy = Mathf.RoundToInt((ci.uvTopLeft.y - ci.uvBottomLeft.y) * kf.ph / gh);
                int ox = Mathf.RoundToInt(ci.uvBottomLeft.x * kf.pw + 0.5f * (exx + eyx) - 0.5f), oy = Mathf.RoundToInt(ci.uvBottomLeft.y * kf.ph + 0.5f * (exy + eyy) - 0.5f);
                int jMax = Mathf.Min(h, Mathf.CeilToInt(gy1)), iMax = Mathf.Min(w, Mathf.CeilToInt(gx1));
                for (int j = Mathf.Max(0, Mathf.FloorToInt(gy0)); j < jMax; j++)
                    for (int i = Mathf.Max(0, Mathf.FloorToInt(gx0)); i < iMax; i++)
                    {
                        double uo = (i + 0.5 - gx0) / k, vo = (j + 0.5 - gy0) / k; // 예전 s·gw, t·gh
                        if (uo < 0 || uo > gw || vo < 0 || vo > gh) continue; // 예전 s·t가 0~1 밖인 자리
                        // 예전 GetPixelBilinear는 텍셀 n을 가운데가 아니라 좌표 n에 두고 섞었다 (11-16 시험: 텍셀 가운데에서 0.25). 같은 자리를 글자 기준
                        // 좌표로 옮겨 같은 모양을 내되, 칸 밖(이웃 글자)은 0으로 읽는다 — 예전에는 이웃 글자가 끝자리에 섞여 실행마다 1단계씩 흔들렸다
                        double u = uo - 0.5 + 0.5 * (exx + exy), v = vo - 0.5 + 0.5 * (eyx + eyy);
                        int u0 = (int)System.Math.Floor(u), v0 = (int)System.Math.Floor(v);
                        float fu = (float)(u - u0), fv = (float)(v - v0);
                        float a = Mathf.Lerp(Mathf.Lerp(KGlyphTexel(kf, gw, gh, ox, oy, exx, exy, eyx, eyy, u0, v0), KGlyphTexel(kf, gw, gh, ox, oy, exx, exy, eyx, eyy, u0 + 1, v0), fu),
                                             Mathf.Lerp(KGlyphTexel(kf, gw, gh, ox, oy, exx, exy, eyx, eyy, u0, v0 + 1), KGlyphTexel(kf, gw, gh, ox, oy, exx, exy, eyx, eyy, u0 + 1, v0 + 1), fu), fv);
                        if (k > 1f) a = Mathf.Clamp01((a - 0.5f) * gain + 0.5f);
                        a *= color.a;
                        if (a <= 0) continue;
                        int idx = j * w + i;
                        px[idx] = Color.Lerp(px[idx], new Color(color.r, color.g, color.b, 1f), a);
                    }
            }
            pen += ci.advance * k;
        }
        return width;
    }

    // ---------- 메시 도우미 ----------

    // 윗면 UV를 (로컬 자리 + offset) / tile 로 매긴 상자: 나란히 놓은 판끼리 무늬가 이어진다 (옆면은 보통 상자처럼)
    static Mesh KSlabMesh(Vector3 s, float tile, Vector2 uvOffset)
    {
        string key = $"slab_{s.x:0.00}_{s.y:0.00}_{s.z:0.00}_{tile:0.0}_{uvOffset.x:0.00}_{uvOffset.y:0.00}";
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var box = BoxMesh(s, tile);
        var m = new Mesh { name = key };
        m.vertices = box.vertices;
        m.normals = box.normals;
        m.triangles = box.triangles;
        var uv = box.uv;
        var verts = box.vertices;
        for (int i = 0; i < verts.Length; i++)
            if (box.normals[i].y > 0.5f)
                uv[i] = new Vector2((verts[i].x + s.x / 2 + uvOffset.x) / tile, (verts[i].z + s.z / 2 + uvOffset.y) / tile);
        m.uv = uv;
        m.RecalculateTangents();
        m.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(m, meshContainer);
        meshCache[key] = m;
        return m;
    }

    static GameObject KSlab(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile, Vector2 uvOffset, bool collider = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = KSlabMesh(size, tile, uvOffset);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<BoxCollider>().size = size;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    // 회전체: profile = (반지름, 높이) 점들 (아래→위). 옆면 + 위 뚜껑. UV u는 둘레, v는 높이(m 단위 / vTile)
    static Mesh KLathe(string key, Vector2[] profile, int segments, float vTile = 1f)
    {
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        int rows = profile.Length;
        for (int r = 0; r < rows; r++)
        {
            // 법선: 이웃 점으로 기울기
            var p0 = profile[Mathf.Max(0, r - 1)];
            var p1 = profile[Mathf.Min(rows - 1, r + 1)];
            var tangent = new Vector2(p1.x - p0.x, p1.y - p0.y).normalized;
            var n2 = new Vector2(tangent.y, -tangent.x);
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                verts.Add(new Vector3(cos * profile[r].x, profile[r].y, sin * profile[r].x));
                norms.Add(new Vector3(cos * n2.x, n2.y, sin * n2.x).normalized);
                uvs.Add(new Vector2(s / (float)segments * Mathf.Max(1f, Mathf.Round(profile[r].x * Mathf.PI * 2f / vTile)), profile[r].y / vTile));
            }
        }
        for (int r = 0; r < rows - 1; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s, b = a + segments + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1); // 겉면이 앞면이 되는 감기 (KBowlMesh와 같음)
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        // 위 뚜껑
        var top = profile[rows - 1];
        int c = verts.Count;
        verts.Add(new Vector3(0, top.y, 0));
        norms.Add(Vector3.up);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int s = 0; s <= segments; s++)
        {
            float a = s / (float)segments * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(a) * top.x, top.y, Mathf.Sin(a) * top.x));
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int s = 0; s < segments; s++)
        {
            tris.Add(c); tris.Add(c + 1 + s); tris.Add(c + 2 + s);
        }
        var m = new Mesh { name = key };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateTangents();
        m.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(m, meshContainer);
        meshCache[key] = m;
        return m;
    }

    // 납작한 볼록 다각형 (xz 평면, 위를 본다, 두께 thick 만큼 옆면도 둔다). UV는 m 단위
    static Mesh KPolygon(string key, Vector2[] pts, float thick)
    {
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            verts.Add(new Vector3(pts[i].x, thick, pts[i].y));
            norms.Add(Vector3.up);
            uvs.Add(pts[i]);
        }
        for (int i = 1; i < n - 1; i++)
        {
            tris.Add(0); tris.Add(i + 1); tris.Add(i);
        }
        if (thick > 0)
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var a = pts[i]; var b = pts[j];
                var side = new Vector2(b.y - a.y, -(b.x - a.x)).normalized;
                var nrm = new Vector3(side.x, 0, side.y);
                int k = verts.Count;
                verts.Add(new Vector3(a.x, 0, a.y)); verts.Add(new Vector3(b.x, 0, b.y)); verts.Add(new Vector3(b.x, thick, b.y)); verts.Add(new Vector3(a.x, thick, a.y));
                for (int q = 0; q < 4; q++) { norms.Add(nrm); uvs.Add(new Vector2(q < 2 ? 0 : 1, (q == 0 || q == 3) ? 0 : 1)); }
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                tris.Add(k); tris.Add(k + 3); tris.Add(k + 2);
            }
        var m = new Mesh { name = key };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateTangents();
        m.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(m, meshContainer);
        meshCache[key] = m;
        return m;
    }

    // 납작한 원판 (xz 평면, UV 0~1 평면 매핑)
    static Mesh KDisc(string key, float radius, int segments, float thick)
    {
        var pts = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
        var m = KPolygon(key, pts, thick);
        // 평면 UV 0~1
        var uv = new Vector2[m.vertexCount];
        var v = m.vertices;
        for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(v[i].x / (radius * 2f) + 0.5f, v[i].z / (radius * 2f) + 0.5f);
        m.uv = uv;
        return m;
    }

    static Mesh kCylinder, kSphere, kCube, kCylThin, kCyl4, kCyl8, kPrism;

    // 뚜껑 없는 사각 막대 메시 (16정점, 면마다 평평한 법선): 단면 ±0.5, y -1~1
    static Mesh KPrismMesh()
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        var corners = new[] { new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f), new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f) };
        for (int f = 0; f < 4; f++)
        {
            var c0 = corners[f]; var c1 = corners[(f + 1) % 4];
            var n = new Vector3((c0.x + c1.x) * 0.5f, 0, (c0.y + c1.y) * 0.5f).normalized;
            int b0 = verts.Count;
            verts.Add(new Vector3(c0.x, -1f, c0.y)); verts.Add(new Vector3(c1.x, -1f, c1.y)); verts.Add(new Vector3(c0.x, 1f, c0.y)); verts.Add(new Vector3(c1.x, 1f, c1.y));
            for (int k = 0; k < 4; k++) norms.Add(n);
            uvs.Add(new Vector2(f * 0.25f, 0)); uvs.Add(new Vector2(f * 0.25f + 0.25f, 0)); uvs.Add(new Vector2(f * 0.25f, 1)); uvs.Add(new Vector2(f * 0.25f + 0.25f, 1));
            tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 1); // 겉면이 앞면이 되는 감기 (KCylMesh와 같음)
            tris.Add(b0 + 1); tris.Add(b0 + 2); tris.Add(b0 + 3);
        }
        var m = new Mesh { name = "KPrism", hideFlags = HideFlags.HideAndDontSave };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    // 뚜껑 없는 사각 막대 (KPart 대신, 16정점): size의 가장 긴 축이 막대 길이. 끝면이 다른 부재에 묻히는 살·틀·난간에 쓴다
    static void KBar(List<CombineInstance> parts, Vector3 size, Vector3 pos, Quaternion rot)
    {
        Quaternion axis = Quaternion.identity; Vector3 scale;
        if (size.x >= size.y && size.x >= size.z) { axis = Quaternion.Euler(0, 0, -90f); scale = new Vector3(size.y, size.x * 0.5f, size.z); }
        else if (size.z >= size.y) { axis = Quaternion.Euler(90f, 0, 0); scale = new Vector3(size.x, size.z * 0.5f, size.y); }
        else scale = new Vector3(size.x, size.y * 0.5f, size.z);
        parts.Add(new CombineInstance { mesh = kPrism ?? (kPrism = KPrismMesh()), transform = Matrix4x4.TRS(pos, rot * axis, scale) });
    }

    // 가는 막대용 6각 뚜껑 없는 원기둥 (14정점)
    static Mesh KThinCyl() => kCylThin ?? (kCylThin = KCylMesh(6, false));

    // 저폴리 원기둥: 반지름 0.5, y -1~1 (내장 원기둥과 같은 치수). 옆면 2(sides+1) 정점 + 뚜껑마다 가운데 1 + (sides+1)
    static Mesh KCylMesh(int sides, bool caps)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int s = 0; s <= sides; s++)
        {
            float a = s / (float)sides * Mathf.PI * 2f, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            verts.Add(new Vector3(c * 0.5f, -1f, sn * 0.5f)); norms.Add(new Vector3(c, 0, sn)); uvs.Add(new Vector2(s / (float)sides, 0));
            verts.Add(new Vector3(c * 0.5f, 1f, sn * 0.5f)); norms.Add(new Vector3(c, 0, sn)); uvs.Add(new Vector2(s / (float)sides, 1));
        }
        for (int s = 0; s < sides; s++)
        {
            int b0 = s * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3; // KLathe와 같은 감기: (아래 s, 아래 s+1, 위 s), (아래 s+1, 위 s+1, 위 s)
            tris.Add(b0); tris.Add(t0); tris.Add(b1); // 겉면이 앞면이 되는 감기
            tris.Add(b1); tris.Add(t0); tris.Add(t1);
        }
        if (caps)
            foreach (float y in new[] { -1f, 1f })
            {
                int center = verts.Count;
                verts.Add(new Vector3(0, y, 0)); norms.Add(new Vector3(0, y, 0)); uvs.Add(new Vector2(0.5f, 0.5f));
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f, c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    verts.Add(new Vector3(c * 0.5f, y, sn * 0.5f)); norms.Add(new Vector3(0, y, 0)); uvs.Add(new Vector2(0.5f + c * 0.5f, 0.5f + sn * 0.5f));
                }
                for (int s = 0; s < sides; s++)
                {
                    int r0 = center + 1 + s, r1 = r0 + 1; // 위 뚜껑은 KBowlMesh와 같은 감기 (가운데, s, s+1), 아래는 반대
                    if (y > 0) { tris.Add(center); tris.Add(r0); tris.Add(r1); } else { tris.Add(center); tris.Add(r1); tris.Add(r0); }
                }
            }
        var m = new Mesh { name = "KCyl" + sides + (caps ? "c" : ""), hideFlags = HideFlags.HideAndDontSave };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    // 저폴리 구: 반지름 0.5, segs×rings UV 구 (아래 극에서 위 극으로 줄을 쌓아 KLathe와 같은 감기)
    static Mesh KSphereMesh(int segs, int rings)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            float phi = Mathf.PI * (1f - r / (float)rings), y = Mathf.Cos(phi), rad = Mathf.Sin(phi);
            for (int s = 0; s <= segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2f;
                var n = new Vector3(rad * Mathf.Cos(a), y, rad * Mathf.Sin(a));
                verts.Add(n * 0.5f); norms.Add(n); uvs.Add(new Vector2(s / (float)segs, r / (float)rings));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segs; s++)
            {
                int a = r * (segs + 1) + s, b = a + segs + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1); // 겉면이 앞면이 되는 감기 (KBowlMesh와 같음)
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        var m = new Mesh { name = "KSphere" + segs + "x" + rings, hideFlags = HideFlags.HideAndDontSave };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    static Mesh KPrimMesh(PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        var m = go.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(go);
        return m;
    }
    static Mesh KBuiltin(PrimitiveType type)
    {
        switch (type)
        {
            // 내장 원기둥(88정점)·구(515정점) 대신 저폴리 메시(반지름 0.5·높이 2, 치수는 CreatePrimitive와 같다) — 합친 메시 에셋 용량 때문
            case PrimitiveType.Cylinder: return kCylinder ?? (kCylinder = KCylMesh(12, true));
            case PrimitiveType.Sphere: return kSphere ?? (kSphere = KSphereMesh(8, 6));
            default: return kCube ?? (kCube = KPrimMesh(type));
        }
    }

    // 합칠 조각 목록에 상자를 넣는다 (pos·rot는 합친 물체의 부모 기준 로컬)
    static void KPart(List<CombineInstance> parts, Vector3 size, float tile, Vector3 pos, Quaternion rot)
        => parts.Add(new CombineInstance { mesh = BoxMesh(size, tile), transform = Matrix4x4.TRS(pos, rot, Vector3.one) });

    // a→b 원기둥 (반지름 r)을 합칠 목록에 넣는다
    static void KRod(List<CombineInstance> parts, Vector3 a, Vector3 b, float r)
    {
        var d = b - a;
        float len = d.magnitude;
        if (len < 1e-4f) return;
        var rodMesh = r <= 0.02f ? (kCyl4 ?? (kCyl4 = KCylMesh(4, false))) : r <= 0.06f ? KThinCyl() : KBuiltin(PrimitiveType.Cylinder); // 전선은 4각, 가는 막대는 6각 (뚜껑 없음)
        parts.Add(new CombineInstance { mesh = rodMesh, transform = Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / len), new Vector3(r * 2f, len * 0.5f + r * 0.3f, r * 2f)) });
    }

    // 늘어진 전선: a에서 b로, 가운데가 sag만큼 처진다 (포물선)
    static void KWire(List<CombineInstance> parts, Vector3 a, Vector3 b, float sag, float r, int segs)
    {
        var prev = a;
        for (int i = 1; i <= segs; i++)
        {
            float t = i / (float)segs;
            var p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            KRod(parts, prev, p, r);
            prev = p;
        }
    }

    static void KSphereAt(List<CombineInstance> parts, Vector3 pos, float r)
        => parts.Add(new CombineInstance { mesh = KBuiltin(PrimitiveType.Sphere), transform = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * r * 2f) });

    static void KCylAt(List<CombineInstance> parts, Vector3 pos, float r, float height, Quaternion rot)
        => parts.Add(new CombineInstance { mesh = r < 0.1f ? (kCyl8 ?? (kCyl8 = KCylMesh(8, true))) : KBuiltin(PrimitiveType.Cylinder), transform = Matrix4x4.TRS(pos, rot, new Vector3(r * 2f, height * 0.5f, r * 2f)) }); // 작은 원기둥(애자·띠·볼라드)은 8각
}
