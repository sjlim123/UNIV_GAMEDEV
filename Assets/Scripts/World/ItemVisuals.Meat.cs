using UnityEngine;

// 고기 (닭다리 모양): 뼈 끝(-x)을 쥐고 살이 +x로 뻗는다. 축은 두꺼운 살과 뼈 끝이 함께 바닥에 닿도록 살짝(MeatTilt) 들려 있다.
// 생고기: 어두운 붉은 살에 길이 방향 근육 결·힘줄·지방막, 두꺼운 쪽은 잘린 단면(분홍 살·지방 마블링·뼈 단면)이고 거기서 뼈가 더 나와 있다 (막 잡은 상태).
// 구운 고기: 노릇하게 탄 껍질(얼룩·바삭한 돌기·후추 점, 법선 그림으로 울퉁불퉁), 두꺼운 쪽은 둥글게 닫혀 뼈가 안 보이고 쥐는 쪽 뼈만 나와 있다.
// 바닥·손·아이콘이 같은 모양을 쓴다 (HeldItems는 MeatGripPoint·MeatAxis·MeatUp으로 뼈 자루를 쥔다)
public static partial class ItemVisuals
{
    const float MeatTilt = 8.6f; // 도. 뼈 끝(반지름 0.024)과 살(반지름 0.054)이 둘 다 바닥에 닿는 기울기
    static readonly Quaternion MeatRot = Quaternion.Euler(0, 0, -90f + MeatTilt); // 회전체의 +y(뼈 끝→살)를 +x로
    static readonly Vector3 MeatRoot = new Vector3(-0.21f, 0.024f, 0);            // 뼈 끝 자리
    public static readonly Vector3 MeatAxis = MeatRot * Vector3.up;               // 뼈 끝 → 살 방향 (손에 들 때 엄지 쪽)
    public static readonly Vector3 MeatUp = MeatRot * Vector3.left;               // 축에 수직인 위쪽
    public static readonly Vector3 MeatGripPoint = MeatRoot + MeatAxis * 0.062f;  // 뼈 자루 가운데

    static void Meat(Transform t, bool cooked)
    {
        var leg = new GameObject("Leg").transform;
        leg.SetParent(t, false);
        leg.localPosition = MeatRoot;
        leg.localRotation = MeatRot;
        leg.localScale = new Vector3(1f, 1f, 0.9f); // 살짝 납작하게 (바닥에 눌린 느낌)

        // 뼈 자루: 끝의 둥근 관절 혹 + 자루 (살 속까지 이어진다)
        var bone = TexMat(cooked ? "meat_bone_cooked" : "meat_bone_raw", () => BoneTexture(cooked), 0.35f, 0f, Vector2.one, Vector2.zero);
        MeshPart(leg, "Bone", Lathe("meat_bone", 20,
            0f, 0f, 0.012f, 0f, 0.02f, 0.006f, 0.024f, 0.014f, 0.023f, 0.022f, 0.018f, 0.03f, 0.0135f, 0.038f, 0.0125f, 0.05f, 0.0125f, 0.1f, 0f, 0.1f), bone);

        if (cooked)
        {
            var skin = BumpMat("meat_cooked", CookedSkinTexture, CookedSkinNormal, 0.5f);
            MeshPart(leg, "Meat", Lathe("meat_cooked", 28,
                0.0125f, 0.088f, 0.019f, 0.092f, 0.03f, 0.105f, 0.042f, 0.13f, 0.05f, 0.165f, 0.054f, 0.2f, 0.052f, 0.232f, 0.044f, 0.252f,
                0.03f, 0.264f, 0.014f, 0.27f, 0f, 0.271f), skin);
        }
        else
        {
            var skin = BumpMat("meat_raw", RawSkinTexture, RawSkinNormal, 0.58f);
            MeshPart(leg, "Meat", Lathe("meat_raw", 28,
                0.0125f, 0.088f, 0.018f, 0.09f, 0.03f, 0.1f, 0.042f, 0.125f, 0.05f, 0.16f, 0.054f, 0.195f, 0.053f, 0.225f, 0.049f, 0.245f, 0.046f, 0.255f), skin);
            // 잘린 단면과 거기서 나온 뼈
            MeshPart(leg, "Cut", Disc("meat_cut", 0f, 0.046f, 0.255f, 28, true), TexMat("meat_cut", CutFaceTexture, 0.45f, 0f, Vector2.one, Vector2.zero));
            MeshPart(leg, "BoneTop", Lathe("meat_bone_top", 16, 0f, 0.24f, 0.0125f, 0.24f, 0.0125f, 0.29f, 0.011f, 0.296f, 0.006f, 0.3f, 0f, 0.3f), bone);
        }
    }

    // 생고기 껍질 (256×256, 가로가 둘레·세로가 뼈 끝→단면): 어두운 붉은 살, 길이 방향으로 길게 늘어진 밝은 근육 결, 큼직한 어두운 핏자국, 두꺼운 쪽에 많은 연한 지방막, 가는 힘줄
    static Texture2D RawSkinTexture()
    {
        return MakeTexture("meat_raw", 256, 256, new Color(0.46f, 0.12f, 0.1f), delegate (Bitmap b)
        {
            var fiber = new Color(0.64f, 0.2f, 0.16f);
            var dark = new Color(0.3f, 0.06f, 0.06f);
            var fat = new Color(0.88f, 0.74f, 0.7f);
            var sinew = new Color(0.74f, 0.42f, 0.4f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float f = StreakNoise(x, y, 256, 40, 6, 51);
                    float blot = TileNoise(x, y, 256, 5, 52);
                    float marb = TileNoise(x, y, 256, 14, 53) * 0.6f + TileNoise(x, y, 256, 44, 56) * 0.4f; // 지방막: 큰 덩어리에 잔 무늬를 섞어 네모지지 않게
                    b.Blend(x, y, fiber, Mathf.Clamp01((f - 0.55f) * 2.2f) * 0.7f);
                    b.Blend(x, y, dark, Mathf.Clamp01((blot - 0.6f) * 2.5f) * 0.6f);
                    float fatK = Mathf.Clamp01((marb - 0.62f) * 5f) * Mathf.Lerp(0.3f, 1f, y / 255f);
                    b.Blend(x, y, fat, fatK * 0.55f);
                }
            var rnd = new System.Random(54);
            for (int i = 0; i < 9; i++)
            {
                float x0 = (float)rnd.NextDouble() * 256, y0 = (float)rnd.NextDouble() * 200, l = 30 + (float)rnd.NextDouble() * 50;
                b.Line(x0, y0, x0 + ((float)rnd.NextDouble() - 0.5f) * 10, y0 + l, 1.7f, sinew);
            }
            b.Noise(0.05f, 55);
        });
    }

    // 생고기 표면 법선: 근육 결을 따라 얕게 울퉁불퉁
    static Texture2D RawSkinNormal() => NormalTexture("meat_raw_n", 256, 256,
        (x, y) => StreakNoise(x, y, 256, 40, 6, 51) * 0.7f + TileNoise(x, y, 256, 14, 53) * 0.3f, 1.0f);

    // 가로·세로 칸 수가 다른 값 노이즈 (0~1): 세로(v, 뼈를 따라) 칸이 적으면 길게 늘어진 근육 결이 된다. 가로(u, 둘레)는 되풀이된다
    static float StreakNoise(int x, int y, int period, int cellsX, int cellsY, int seed)
    {
        float fx = (float)x / period * cellsX, fy = (float)y / period * cellsY;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        int xa = (x0 % cellsX + cellsX) % cellsX, xb = ((x0 + 1) % cellsX + cellsX) % cellsX;
        float a = Hash(xa, y0, seed), b = Hash(xb, y0, seed), c = Hash(xa, y0 + 1, seed), d = Hash(xb, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    // 구운 껍질 (256×256): 노릇한 바탕에 큼직한 탄 얼룩, 바삭하게 튀어나온 밝은 부분, 뼈 쪽은 덜 익어 불그스름, 후추·탄 점
    static Texture2D CookedSkinTexture()
    {
        return MakeTexture("meat_cooked", 256, 256, new Color(0.7f, 0.4f, 0.14f), delegate (Bitmap b)
        {
            var roast = new Color(0.42f, 0.18f, 0.05f);
            var crisp = new Color(0.88f, 0.62f, 0.3f);
            var pale = new Color(0.78f, 0.52f, 0.24f);
            var red = new Color(0.6f, 0.28f, 0.1f);
            var burnt = new Color(0.18f, 0.08f, 0.03f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float big = TileNoise(x, y, 256, 6, 61), mid = TileNoise(x, y, 256, 18, 62), fine = TileNoise(x, y, 256, 48, 63);
                    b.Blend(x, y, roast, Mathf.Clamp01((big - 0.52f) * 2.8f) * 0.95f);
                    b.Blend(x, y, crisp, Mathf.Clamp01((mid - 0.6f) * 3f) * 0.7f);
                    b.Blend(x, y, pale, Mathf.Clamp01((fine - 0.62f) * 3f) * 0.5f);
                    b.Blend(x, y, red, Mathf.Clamp01(1f - y / 70f) * 0.45f);
                }
            var rnd = new System.Random(64);
            for (int i = 0; i < 420; i++)
            {
                int x = rnd.Next(256), y = rnd.Next(256);
                float r = 0.6f + (float)rnd.NextDouble() * 1.2f;
                b.Ellipse(x, y, r, r, burnt);
            }
            b.Noise(0.06f, 65);
        });
    }

    // 구운 껍질 법선: 크고 작은 혹이 겹쳐 쭈글쭈글
    static Texture2D CookedSkinNormal() => NormalTexture("meat_cooked_n", 256, 256,
        (x, y) => TileNoise(x, y, 256, 9, 71) * 0.55f + TileNoise(x, y, 256, 26, 72) * 0.3f + TileNoise(x, y, 256, 64, 73) * 0.15f, 2.6f);

    // 잘린 단면 (128×128, 원판 평면 좌표): 분홍 살에 결 무늬·지방 마블링, 가장자리 지방층과 겉껍질 테, 가운데 뼈 단면(겉뼈 테 + 골수)
    static Texture2D CutFaceTexture()
    {
        return MakeTexture("meat_cut", 128, 128, new Color(0.84f, 0.46f, 0.46f), delegate (Bitmap b)
        {
            const float C = 64f, R = 64f;
            var fiberDark = new Color(0.7f, 0.26f, 0.28f);
            var fiberLight = new Color(0.92f, 0.6f, 0.58f);
            var fat = new Color(0.96f, 0.9f, 0.86f);
            var rim = new Color(0.5f, 0.14f, 0.12f);
            var boneEdge = new Color(0.82f, 0.76f, 0.62f);
            var bone = new Color(0.93f, 0.9f, 0.8f);
            var marrow = new Color(0.72f, 0.5f, 0.46f);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float dx = x + 0.5f - C, dy = y + 0.5f - C;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / R;
                    if (d > 1.02f) continue;
                    float n1 = TileNoise(x, y, 128, 6, 81), n2 = TileNoise(x, y, 128, 16, 82);
                    b.Blend(x, y, fiberDark, Mathf.Clamp01((n1 - 0.5f) * 3f) * 0.7f);
                    b.Blend(x, y, fiberLight, Mathf.Clamp01((n2 - 0.6f) * 3f) * 0.6f);
                    b.Blend(x, y, fat, Mathf.Clamp01((n2 - 0.72f) * 5f));
                    b.Blend(x, y, fat, Mathf.Clamp01((d - 0.86f) * 6f) * 0.9f);
                    b.Blend(x, y, rim, Mathf.Clamp01((d - 0.95f) * 12f));
                }
            var rnd = new System.Random(83);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, r0 = 14 + (float)rnd.NextDouble() * 30, l = 10 + (float)rnd.NextDouble() * 22;
                float x0 = C + Mathf.Cos(a) * r0, y0 = C + Mathf.Sin(a) * r0;
                float a2 = a + ((float)rnd.NextDouble() - 0.5f) * 2f;
                b.Line(x0, y0, x0 + Mathf.Cos(a2) * l, y0 + Mathf.Sin(a2) * l, 1.5f, fat);
            }
            b.Ellipse(C, C, 18.5f, 18.5f, boneEdge);
            b.Ellipse(C, C, 16f, 16f, bone);
            b.Ellipse(C, C, 7.5f, 7.5f, marrow);
            b.Noise(0.04f, 84);
        }, false);
    }

    // 뼈 (64×256): 미색에 잿빛 얼룩. 생고기는 관절 끝이 연골빛, 구운 고기는 살 가까운 쪽이 그을렸다
    static Texture2D BoneTexture(bool cooked)
    {
        return MakeTexture(cooked ? "meat_bone_cooked" : "meat_bone_raw", 64, 256, new Color(0.9f, 0.86f, 0.75f), delegate (Bitmap b)
        {
            var grey = new Color(0.78f, 0.74f, 0.64f);
            var pink = new Color(0.88f, 0.72f, 0.7f);
            var scorch = new Color(0.55f, 0.38f, 0.22f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = TileNoise(x, y, 64, 8, 91) * 0.5f + TileNoise(x, y, 256, 20, 93) * 0.5f; // 두 결을 섞어 같은 무늬가 되풀이되지 않게
                    b.Blend(x, y, grey, Mathf.Clamp01((n - 0.55f) * 2.5f) * 0.4f);
                    if (cooked) b.Blend(x, y, scorch, Mathf.Clamp01((y - 150f) / 80f) * 0.7f);
                    else b.Blend(x, y, pink, Mathf.Clamp01(1f - y / 40f) * 0.6f);
                }
            b.Noise(0.04f, 92);
        });
    }

    // ================= 법선 그림 도우미 =================

    // 높이 함수(0~1)에서 접공간 법선 그림을 만든다 (가로로 되풀이). strength: 기울기 배율. 선형 색공간, RGB에 법선(알파 1)
    static Texture2D NormalTexture(string key, int w, int h, System.Func<int, int, float> height, float strength)
    {
        if (textures.TryGetValue(key, out var t) && t != null) return t;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float l = height((x + w - 1) % w, y), r = height((x + 1) % w, y);
                float d = height(x, Mathf.Max(0, y - 1)), u = height(x, Mathf.Min(h - 1, y + 1));
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                px[y * w + x] = new Color32((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255), (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255), (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255), 255);
            }
        t = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { name = "Item_" + key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        t.SetPixels32(px);
        t.Apply(true, true);
        textures[key] = t;
        return t;
    }

    // 그림 + 법선 그림을 입힌 URP Lit 재질 (key로 한 번만 만든다)
    static Material BumpMat(string key, System.Func<Texture2D> albedo, System.Func<Texture2D> normal, float smoothness)
    {
        if (texMats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Item " + key };
        m.SetTexture("_BaseMap", albedo());
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        var n = normal != null ? normal() : null;
        if (n != null)
        {
            m.SetTexture("_BumpMap", n);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        texMats[key] = m;
        return m;
    }
}
