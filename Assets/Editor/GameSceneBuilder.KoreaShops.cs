using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// GameSceneBuilder의 한국 상가 파사드 (맵 개편 세션):
//  1층 통유리 상점(알루미늄 창틀·유리 또는 깨진 유리·내린 셔터·찢어진 어닝), 가로 간판(31·삼일 상호, 기운 것·떨어진 것·깜빡이는 것),
//  돌출 간판, 2층 이상 창문(흰 새시·유리·실외기), 옥상(난간·물탱크·옥탑방·안테나), 빗물 홈통, 계량기함, 찢어진 현수막.
// 상호는 팀 31 기준으로 새로 지었다 (참고 사진의 상호·브랜드는 쓰지 않는다)
public static partial class GameSceneBuilder
{
    // ---------- 상호 ----------

    struct KShop
    {
        public string name, sub;
        public Color bg, fg;
        public int logo; // 0 없음, 1 '31' 배지, 2 약국 십자, 3 둥근 점(컵·그릇)
        public KShop(string name, string sub, Color bg, Color fg, int logo) { this.name = name; this.sub = sub; this.bg = bg; this.fg = fg; this.logo = logo; }
    }

    static readonly KShop[] KShops =
    {
        new KShop("31편의점", "24시간 · 택배 · ATM", new Color(0.08f, 0.5f, 0.24f), Color.white, 1),
        new KShop("삼일약국", "처방 조제 · 의약품 · 건강식품", new Color(0.9f, 0.9f, 0.86f), new Color(0.05f, 0.4f, 0.2f), 2),
        new KShop("삼일국밥", "돼지국밥 · 순대국 · 24시", new Color(0.62f, 0.1f, 0.08f), new Color(0.96f, 0.9f, 0.78f), 3),
        new KShop("삼일커피", "SAMIL COFFEE · 디저트", new Color(0.16f, 0.14f, 0.13f), new Color(0.9f, 0.85f, 0.74f), 3),
        new KShop("삼일학원", "수학 · 영어 · 초중고 전문", new Color(0.1f, 0.25f, 0.55f), Color.white, 0),
        new KShop("31세탁", "드라이 · 수선 · 당일 완성", new Color(0.55f, 0.75f, 0.88f), new Color(0.1f, 0.2f, 0.4f), 1),
        new KShop("삼일공인중개사", "아파트 · 상가 · 원룸 임대", new Color(0.92f, 0.92f, 0.9f), new Color(0.12f, 0.2f, 0.5f), 0),
        new KShop("삼일치킨", "후라이드 · 양념 · 배달", new Color(0.9f, 0.72f, 0.12f), new Color(0.6f, 0.08f, 0.06f), 3),
        new KShop("삼일마트", "식료품 · 생활용품 · 야채", new Color(0.85f, 0.42f, 0.08f), Color.white, 0),
        new KShop("삼일PC", "PC · 게임 · 24시 오픈", new Color(0.08f, 0.06f, 0.12f), new Color(0.6f, 0.4f, 0.95f), 0),
        new KShop("삼일미용실", "커트 · 펌 · 염색", new Color(0.88f, 0.6f, 0.7f), new Color(0.3f, 0.1f, 0.2f), 0),
        new KShop("삼일분식", "떡볶이 · 김밥 · 라면", new Color(0.9f, 0.68f, 0.1f), new Color(0.65f, 0.1f, 0.08f), 3),
        new KShop("삼일철물", "공구 · 페인트 · 열쇠", new Color(0.35f, 0.38f, 0.36f), new Color(0.95f, 0.85f, 0.4f), 0),
        new KShop("31은행", "SAMIL BANK · 365 코너", new Color(0.1f, 0.15f, 0.38f), Color.white, 1),
        new KShop("삼일노래방", "노래 · 연습 · 24시", new Color(0.3f, 0.08f, 0.4f), new Color(0.98f, 0.8f, 0.3f), 0),
        new KShop("삼일정육점", "한우 · 돼지 · 닭", new Color(0.75f, 0.12f, 0.1f), Color.white, 0),
        new KShop("삼일만물상", "뭐든지 삽니다 · 팝니다", new Color(0.55f, 0.42f, 0.14f), new Color(0.98f, 0.92f, 0.7f), 0), // 잡상인 건물 (마지막 칸, 무작위 선택에서는 뺀다)
    };
    const int KTraderShop = 16;

    // 돌출 간판 (세로): 글자와 층
    static readonly string[] KSideSigns = { "학원", "노래방", "PC방", "치과", "당구장", "헬스" };
    // 현수막 글귀
    static readonly string[][] KBanners =
    {
        new[] { "민방위 대피소 → 300m", "유사시 즉시 대피 · 삼일동 주민센터" },
        new[] { "삼일 1구역 재개발 확정", "삼일동 재개발 추진위원회" },
        new[] { "전 품목 31% 세일", "삼일마트 개업 기념 · 11월 한정" },
        new[] { "임대 문의 031-3131-0031", "1층 상가 · 사무실 · 원룸" },
    };

    static string KAllChars()
    {
        var sb = new System.Text.StringBuilder("버스정류장삼일시장0123456789·FABCNKSMIL%→-");
        foreach (var s in KShops) { sb.Append(s.name); sb.Append(s.sub); }
        foreach (var s in KSideSigns) sb.Append(s);
        foreach (var b in KBanners) foreach (var line in b) sb.Append(line);
        return sb.ToString();
    }

    // ---------- 재질·그림 ----------

    static void KoreaShopMaterials()
    {
        // 그림을 모두 먼저 그린다 (글꼴 글자판 때문에 들여오기는 뒤에)
        var pretendard = KFontFor("Fonts/Pretendard-Bold", KAllChars());
        var dohyeon = KFontFor("Fonts/DoHyeon-Regular", KAllChars());
        var signPx = new List<Color32[]>();
        for (int i = 0; i < KShops.Length; i++) signPx.Add(SignPixels(KShops[i], i, dohyeon, pretendard));
        var sidePx = new List<Color32[]>();
        for (int i = 0; i < KSideSigns.Length; i++) sidePx.Add(SideSignPixels(KSideSigns[i], i, dohyeon));
        var bannerPx = new List<Color32[]>();
        for (int i = 0; i < KBanners.Length; i++) bannerPx.Add(BannerPixels(KBanners[i], i, dohyeon, pretendard));
        ShutterPixels(out var shA, out var shN);
        KReleaseFonts();
        for (int i = 0; i < KShops.Length; i++)
        {
            var tex = KSaveTex("sign_" + i, signPx[i], 1024, 256, KTexKind.Color, false);
            M["sign" + i] = KLitMat("M_Sign_" + i, tex, null, Color.white, 0.3f, 0f);
            var lit = KLitMat("M_SignLit_" + i, tex, null, Color.white, 0.3f, 0f);
            lit.EnableKeyword("_EMISSION");
            lit.SetTexture("_EmissionMap", tex);
            lit.SetColor("_EmissionColor", new Color(1.1f, 1.05f, 0.95f));
            lit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(lit);
            M["signLit" + i] = lit;
        }
        for (int i = 0; i < KSideSigns.Length; i++)
            M["sideSign" + i] = KLitMat("M_SideSign_" + i, KSaveTex("sign_side_" + i, sidePx[i], 256, 1024, KTexKind.Color, false), null, Color.white, 0.3f, 0f);
        for (int i = 0; i < KBanners.Length; i++)
            M["banner" + i] = KPaintMat("M_Banner_" + i, KSaveTex("banner_" + i, bannerPx[i], 1024, 192, KTexKind.ColorAlpha, false), Color.white);
        M["shutter"] = KLitMat("M_Shutter", KSaveTex("street_shutter", shA, 256, 256, KTexKind.Color), KSaveTex("street_shutter_normal", shN, 256, 256, KTexKind.Normal), new Color(0.6f, 0.6f, 0.58f), 0.3f, 0.45f);
        M["whitePaint"] = TexMat("M_WhitePaint", "dirty_concrete", new Color(0.8f, 0.8f, 0.78f), 0.3f);
        M["frameDark"] = TexMat("M_FrameDark", "rusty_metal_02", new Color(0.2f, 0.19f, 0.18f), 0.45f, 0.5f);
        M["acUnit"] = TexMat("M_AcUnit", "dirty_concrete", new Color(0.7f, 0.7f, 0.68f), 0.25f, 0.2f);
        M["awningRed"] = TexMat("M_AwningRed", "dirty_concrete", new Color(0.42f, 0.08f, 0.06f), 0.05f);
        M["awningGreen"] = TexMat("M_AwningGreen", "dirty_concrete", new Color(0.08f, 0.26f, 0.14f), 0.05f);
        M["tileStone"] = TexMat("M_TileStone", "concrete_floor_worn_001", new Color(0.3f, 0.3f, 0.3f), 0.35f);
    }

    // 가로 간판 1024×256: 바탕색 + 왼쪽 로고 + 큰 상호 + 작은 설명. 때·긁힘·바램
    static Color32[] SignPixels(KShop shop, int index, KFont big, KFont small)
    {
        const int w = 1024, h = 256;
        var px = new Color[w * h];
        var r = new System.Random(900 + index);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var c = shop.bg;
                float n = GrimeFbm(i, j, 1024, 6, 3, 2100 + index);
                c *= 0.9f + 0.2f * n;
                if (i < 8 || i >= w - 8 || j < 8 || j >= h - 8) c = new Color(0.12f, 0.12f, 0.12f);
                px[j * w + i] = c;
            }
        float textX = 60f;
        if (shop.logo == 1)
        {
            KFillRect(px, w, h, 50, 48, 210, 208, new Color(0.96f, 0.96f, 0.94f));
            KText(px, w, h, big, "31", 128, 130f, 128f, shop.bg * 0.9f, 0.5f);
            textX = 250f;
        }
        else if (shop.logo == 2)
        {
            KFillRect(px, w, h, 90, 48, 170, 208, new Color(0.08f, 0.55f, 0.25f));
            KFillRect(px, w, h, 50, 88, 210, 168, new Color(0.08f, 0.55f, 0.25f));
            textX = 250f;
        }
        else if (shop.logo == 3)
        {
            for (int j = 48; j < 208; j++)
                for (int i = 50; i < 210; i++)
                {
                    float dx = (i - 130f) / 80f, dy = (j - 128f) / 80f;
                    float d = dx * dx + dy * dy;
                    if (d < 1f && d > 0.55f) px[j * w + i] = shop.fg;
                    else if (d <= 0.55f && d > 0.3f && j < 128) px[j * w + i] = shop.fg;
                }
            textX = 250f;
        }
        float align = shop.logo == 0 ? 0.5f : 0f;
        float tx = shop.logo == 0 ? w * 0.5f : textX;
        KText(px, w, h, big, shop.name, shop.name.Length > 5 ? 112 : 128, tx, 150f, shop.fg, align);
        KText(px, w, h, small, shop.sub, 32, tx, 54f, Color.Lerp(shop.fg, shop.bg, 0.25f), align);
        KWear(px, w, h, r, 0.55f);
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // 돌출 간판 256×1024: 세로로 쌓은 글자, 맨 위 층 표시
    static Color32[] SideSignPixels(string text, int index, KFont big)
    {
        const int w = 256, h = 1024;
        var px = new Color[w * h];
        var bgs = new[] { new Color(0.12f, 0.2f, 0.5f), new Color(0.5f, 0.1f, 0.5f), new Color(0.06f, 0.06f, 0.1f), new Color(0.9f, 0.9f, 0.88f), new Color(0.1f, 0.4f, 0.25f), new Color(0.75f, 0.15f, 0.1f) };
        var fgs = new[] { Color.white, new Color(0.98f, 0.8f, 0.3f), new Color(0.3f, 0.9f, 1f), new Color(0.1f, 0.3f, 0.6f), Color.white, Color.white };
        var bg = bgs[index % bgs.Length];
        var fg = fgs[index % fgs.Length];
        var r = new System.Random(1200 + index);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var c = bg * (0.9f + 0.2f * GrimeFbm(i, j, 1024, 5, 3, 2300 + index));
                if (i < 8 || i >= w - 8 || j < 8 || j >= h - 8) c = new Color(0.12f, 0.12f, 0.12f);
                px[j * w + i] = c;
            }
        KText(px, w, h, big, (2 + index % 3) + "F", 64, w * 0.5f, h - 90f, fg, 0.5f);
        float top = h - 200f, step = (h - 300f) / Mathf.Max(1, text.Length);
        for (int k = 0; k < text.Length; k++)
            KText(px, w, h, big, text[k].ToString(), 150, w * 0.5f, top - k * step - step * 0.25f, fg, 0.5f);
        KWear(px, w, h, r, 0.5f);
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // 현수막 1024×192: 두 줄 글귀, 가장자리와 찢긴 자리는 알파 0
    static Color32[] BannerPixels(string[] lines, int index, KFont big, KFont small)
    {
        const int w = 1024, h = 192;
        var px = new Color[w * h];
        var bgs = new[] { new Color(0.92f, 0.9f, 0.82f), new Color(0.2f, 0.3f, 0.65f), new Color(0.85f, 0.15f, 0.12f), new Color(0.95f, 0.85f, 0.2f) };
        var fgs = new[] { new Color(0.1f, 0.1f, 0.12f), Color.white, Color.white, new Color(0.15f, 0.1f, 0.1f) };
        var bg = bgs[index % bgs.Length];
        var fg = fgs[index % fgs.Length];
        var r = new System.Random(1500 + index);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var c = bg * (0.85f + 0.25f * GrimeFbm(i, j, 1024, 5, 3, 2500 + index));
                c.a = 1f;
                px[j * w + i] = c;
            }
        KText(px, w, h, big, lines[0], 96, w * 0.5f, h * 0.62f, fg, 0.5f);
        KText(px, w, h, small, lines[1], 32, w * 0.5f, h * 0.2f, Color.Lerp(fg, bg, 0.2f), 0.5f);
        KWear(px, w, h, r, 0.4f);
        // 찢김: 오른쪽 아래가 너덜너덜, 가운데 구멍 하나, 가장자리 들쭉날쭉
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float e = GrimeFbm(i, j, 1024, 12, 2, 2600 + index);
                bool gone = false;
                if (i > w * 0.78f && j < h * (0.25f + 0.5f * e)) gone = true;
                if (i < 10 + 14 * e || i > w - 10 - 14 * e || j < 8 + 12 * e || j > h - 8 - 12 * e) gone = true;
                float hx = (i - w * 0.42f) / 70f, hy = (j - h * 0.3f) / 40f;
                if (hx * hx + hy * hy < 0.5f + e * 0.6f) gone = true;
                if (gone) px[j * w + i].a = 0f;
            }
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) res[k] = KC(px[k]);
        return res;
    }

    // 셔터 256px = 0.5m: 가로 골판 (높이 그림으로 노멀), 벗겨진 칠·녹
    static void ShutterPixels(out Color32[] albedo, out Color32[] normal)
    {
        const int N = 256;
        var px = new Color32[N * N];
        var hgt = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float t = (y % 32) / 32f;
                float ridge = Mathf.Sin(t * Mathf.PI); // 골 하나 = 6.25cm
                float rust = Smooth01(0.6f, 0.9f, GrimeFbm(x, y, N, 4, 3, 2700));
                var c = new Color(0.6f, 0.6f, 0.58f) * (0.75f + 0.3f * ridge) * (0.85f + 0.3f * GrimeFbm(x, y, N, 16, 3, 2800));
                c = Color.Lerp(c, new Color(0.42f, 0.22f, 0.1f), rust * 0.7f);
                px[y * N + x] = KC(c);
                hgt[y * N + x] = ridge;
            }
        albedo = px;
        normal = KNormalFromHeight(hgt, N, 1.4f);
    }

    static void KFillRect(Color[] px, int w, int h, int x0, int y0, int x1, int y1, Color c)
    {
        for (int j = Mathf.Max(0, y0); j < Mathf.Min(h, y1); j++)
            for (int i = Mathf.Max(0, x0); i < Mathf.Min(w, x1); i++)
                px[j * w + i] = new Color(c.r, c.g, c.b, px[j * w + i].a);
    }

    // 간판 때: 아래쪽 흘러내린 때, 긁힘, 바랜 얼룩
    static void KWear(Color[] px, int w, int h, System.Random r, float amount)
    {
        var dirt = new Color(0.22f, 0.2f, 0.17f);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float n = GrimeFbm(i, j, 1024, 5, 4, 3000 + w);
                float drip = GrimeFbm(i, j, 1024, 20, 2, 3100 + w);
                float bottom = Mathf.Clamp01(1f - j / (float)h * 2.2f) * (0.4f + 0.6f * drip);
                float fade = Smooth01(0.58f, 0.85f, n) * 0.5f;
                var c = px[j * w + i];
                var a = c.a;
                c = Color.Lerp(c, dirt, Mathf.Clamp01(bottom * 0.55f + fade * 0.6f) * amount);
                c.a = a;
                px[j * w + i] = c;
            }
        for (int k = 0, n = 6 + r.Next(10); k < n; k++)
        {
            int x0 = r.Next(w), y0 = r.Next(h), len = 20 + r.Next(120);
            float ang = (float)r.NextDouble() * Mathf.PI;
            for (int s = 0; s < len; s++)
            {
                int x = x0 + Mathf.RoundToInt(Mathf.Cos(ang) * s), y = y0 + Mathf.RoundToInt(Mathf.Sin(ang) * s);
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                var c = px[y * w + x];
                var a = c.a;
                c = Color.Lerp(c, new Color(0.15f, 0.14f, 0.13f), 0.6f * amount);
                c.a = a;
                px[y * w + x] = c;
            }
        }
    }

    // ---------- 1층 통유리 상점 ----------

    // 바깥 면 side(0 남 1 북 2 서 3 동)에서 벽 밖으로 나가는 로컬 z 부호
    static float KOutSign(int side) => side == 1 || side == 2 ? 1f : -1f;

    // 상점 상태: 0 유리 멀쩡(45%), 1 깨짐(35%), 2 셔터 내림(20%)
    static int KShopState(System.Random r)
    {
        double v = r.NextDouble();
        return v < 0.45 ? 0 : v < 0.8 ? 1 : 2;
    }

    // 상점 구멍: 문이 있는 면이면 문 옆의 더 긴 쪽(들)에, 아니면 가운데에 큰 통유리 구멍. (c는 방 모서리 기준, RoomWalls가 벽 두께만큼 옮긴다)
    static Op[] KShopOps(float len, float doorC)
    {
        var list = new List<Op>();
        if (doorC >= 0)
        {
            list.Add(DoorOp(doorC));
            float a0 = 0.35f, a1 = doorC - 0.7f - 0.25f, b0 = doorC + 0.7f + 0.25f, b1 = len - 0.35f;
            if (a1 - a0 >= 2.0f) list.Add(new Op((a0 + a1) / 2f, Mathf.Min(a1 - a0, 7f), 0.45f, 2.75f));
            if (b1 - b0 >= 2.0f) list.Add(new Op((b0 + b1) / 2f, Mathf.Min(b1 - b0, 7f), 0.45f, 2.75f));
        }
        else list.Add(new Op(len / 2f, Mathf.Min(len - 1.4f, 8f), 0.45f, 2.75f));
        return list.ToArray();
    }

    // 상점 구멍 꾸밈: 알루미늄 창틀, 유리(또는 깨진 유리), 바닥 돌 띠, 셔터 상자, (가끔) 내린 셔터·어닝. c는 벽 로컬 중심
    // state: 0 멀쩡한 유리, 1 깨짐, 2 셔터 내림
    static void KStorefront(Transform wall, float c, float w, float bottom, float top, float outSign, float t, int state, System.Random r)
    {
        var g = Group("Storefront", wall, new Vector3(c, 0, 0));
        float face = outSign * (t / 2f);
        float hgt = top - bottom;
        var frame = M["frameDark"];
        // 바닥 돌 띠 (바깥 면)
        Box("Base", g, new Vector3(0, bottom / 2f, face + outSign * 0.012f), new Vector3(w + 0.3f, bottom, 0.024f), M["tileStone"], 1f, false);
        // 창틀: 위·아래 가로대, 양 끝 세로대, 1.5m마다 중간 세로대, 2.1m 높이 가로대
        var bars = new List<CombineInstance>();
        float fz = outSign * Mathf.Max(t / 2f - 0.06f, 0.025f); // 얇은 가짜 벽(상가 빌딩 0.12)에서는 유리 앞면이 외벽 때 얼룩(+6~12mm)과 겹치지 않게 2.5cm 앞으로
        KPart(bars, new Vector3(w, 0.08f, 0.1f), 1f, new Vector3(0, bottom + 0.04f, fz), Quaternion.identity);
        KPart(bars, new Vector3(w, 0.08f, 0.1f), 1f, new Vector3(0, top - 0.04f, fz), Quaternion.identity);
        int cols = Mathf.Max(1, Mathf.RoundToInt(w / 1.5f));
        for (int i = 0; i <= cols; i++)
            KPart(bars, new Vector3(0.07f, hgt, 0.1f), 1f, new Vector3(-w / 2f + i * w / cols, bottom + hgt / 2f, fz), Quaternion.identity);
        if (top - bottom > 1.9f) KPart(bars, new Vector3(w, 0.06f, 0.1f), 1f, new Vector3(0, bottom + 1.75f, fz), Quaternion.identity);
        CombineInto(g, "Frame", bars, frame);
        if (state == 0)
        {
            Box("Glass", g, new Vector3(0, bottom + hgt / 2f, fz), new Vector3(w - 0.1f, hgt - 0.1f, 0.012f), M["shopGlass"], 1f, true).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        else if (state == 1)
        {
            // 깨진 유리: 위 가로대에 매달린 조각, 아래 가로대 위에 선 조각, 바닥의 유리 가루. 막는 판정은 허리 높이까지
            var shards = new List<CombineInstance>();
            for (int i = 0, n = 4 + r.Next(5); i < n; i++)
            {
                float sx = KF(r, -w / 2f + 0.2f, w / 2f - 0.2f);
                bool hang = r.NextDouble() < 0.55;
                float sh = KF(r, 0.15f, 0.55f);
                KPart(shards, new Vector3(KF(r, 0.12f, 0.45f), sh, 0.01f), 1f, new Vector3(sx, hang ? top - 0.08f - sh / 2f : bottom + 0.08f + sh / 2f, fz), Quaternion.Euler(0, 0, KF(r, -20f, 20f)));
            }
            for (int i = 0; i < 12; i++)
                KPart(shards, new Vector3(KF(r, 0.04f, 0.14f), 0.006f, KF(r, 0.04f, 0.14f)), 1f, new Vector3(KF(r, -w / 2f, w / 2f), 0.004f, face + outSign * KF(r, 0.1f, 1.1f)), Quaternion.Euler(0, KF(r, 0, 360), 0));
            CombineInto(g, "Shards", shards, M["shopGlass"]);
            ColliderBox("ShardGuard", g, new Vector3(0, bottom + 0.35f, fz), new Vector3(w, 0.7f, 0.08f), Vector3.zero);
            ColliderBox("ShardGuardTop", g, new Vector3(0, top - 0.3f, fz), new Vector3(w, 0.6f, 0.08f), Vector3.zero);
        }
        else
        {
            // 내린 셔터: 구멍을 덮는 골판 (살짝 찌그러지고 아래 틈)
            var sh = Box("Shutter", g, new Vector3(0, bottom + hgt / 2f + 0.06f, face + outSign * 0.03f), new Vector3(w + 0.1f, hgt - 0.02f, 0.03f), M["shutter"], 0.5f, true, new Vector3(0, 0, KF(r, -1.5f, 1.5f)));
            Box("ShutterRail", g, new Vector3(-w / 2f - 0.07f, bottom + hgt / 2f, face + outSign * 0.03f), new Vector3(0.06f, hgt + 0.1f, 0.07f), frame, 1f, false);
            Box("ShutterRail", g, new Vector3(w / 2f + 0.07f, bottom + hgt / 2f, face + outSign * 0.03f), new Vector3(0.06f, hgt + 0.1f, 0.07f), frame, 1f, false);
        }
        // 셔터 상자 (위 가로대 위, 바깥으로 둥근 통)
        var box = new List<CombineInstance>();
        box.Add(new CombineInstance { mesh = KBuiltin(PrimitiveType.Cylinder), transform = Matrix4x4.TRS(new Vector3(0, top + 0.1f, face + outSign * 0.1f), Quaternion.Euler(0, 0, 90), new Vector3(0.26f, (w + 0.3f) / 2f, 0.26f)) });
        CombineInto(g, "ShutterBox", box, M["steelGrey"]);
        // 어닝 (5곳 중 2곳): 틀 + 기운 천, 한 폭은 찢어져 늘어졌다
        if (state != 2 && r.NextDouble() < 0.4)
        {
            var cloth = r.NextDouble() < 0.5 ? M["awningRed"] : M["awningGreen"];
            var aw = Group("Awning", g, new Vector3(0, top + 0.02f, face + outSign * 0.02f));
            var rods = new List<CombineInstance>();
            foreach (float x in new[] { -w / 2f + 0.1f, w / 2f - 0.1f })
                KRod(rods, new Vector3(x, 0, 0), new Vector3(x, -0.35f, outSign * 1.05f), 0.02f);
            KRod(rods, new Vector3(-w / 2f + 0.1f, -0.35f, outSign * 1.05f), new Vector3(w / 2f - 0.1f, -0.35f, outSign * 1.05f), 0.02f);
            CombineInto(aw, "Rods", rods, M["steelGrey"]);
            int strips = Mathf.Max(2, Mathf.RoundToInt(w / 0.9f));
            float sw = w / strips;
            int torn = r.Next(strips);
            for (int i = 0; i < strips; i++)
            {
                float x = -w / 2f + (i + 0.5f) * sw;
                if (i == torn)
                {
                    // 찢어진 폭: 틀에서 늘어진 천 (아래로)
                    Box("Torn", aw, new Vector3(x, -0.5f, outSign * 0.3f), new Vector3(sw - 0.04f, 0.015f, 0.9f), cloth, 1f, false, new Vector3(outSign * 70f, 0, KF(r, -6f, 6f)));
                    continue;
                }
                Box("Cloth", aw, new Vector3(x, -0.17f, outSign * 0.52f), new Vector3(sw - 0.04f, 0.015f, 1.1f), cloth, 1f, false, new Vector3(outSign * -18f, 0, 0));
            }
        }
    }

    // ---------- 상가 빌딩 (들어갈 수 없는 탑) ----------

    // 꽉 찬 덩어리 건물의 바깥 면에 붙는 1층 상점 칸: 면 밖 0.15m에 가짜 벽 좌표계를 두고 통유리 상점을 짓는다 (유리 뒤는 어두운 벽)
    static void KTowerShopfront(Transform g, int side, float x0, float z0, float w, float d, System.Random r)
    {
        float len = side < 2 ? w : d;
        float yaw = side < 2 ? 0f : -90f;
        var outN = KSideOut(side);
        var start = side == 0 ? new Vector3(x0, 0, z0) : side == 1 ? new Vector3(x0, 0, z0 + d) : side == 2 ? new Vector3(x0, 0, z0) : new Vector3(x0 + w, 0, z0);
        // 가짜 벽 두께 0.12: 창틀은 면에 반쯤 묻히고 유리는 면과 겹치지 않게 6mm씩 앞뒤로 나온다
        var fake = Group("ShopBay", g, start, yaw);
        float bay = Mathf.Min(len - 1.8f, 8f);
        KStorefront(fake, len / 2f, bay, 0.45f, 2.75f, KOutSign(side), 0.12f, KShopState(r), r);
        // 유리문 (칸 옆)
        float dx = len / 2f + bay / 2f + 0.55f;
        if (dx + 0.5f > len - 0.2f) dx = len / 2f - bay / 2f - 0.55f;
        float fz = KOutSign(side) * 0.1f;
        Box("DoorFrame", fake, new Vector3(dx, 1.15f, fz), new Vector3(1.0f, 2.3f, 0.06f), M["frameDark"], 1f, false);
        Box("DoorGlass", fake, new Vector3(dx, 1.15f, fz), new Vector3(0.84f, 2.14f, 0.012f), M["shopGlass"], 1f, false).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Box("DoorBar", fake, new Vector3(dx, 1.0f, fz + KOutSign(side) * 0.05f), new Vector3(0.7f, 0.035f, 0.035f), M["steelGrey"], 1f, false);
    }

    // 상가 빌딩 간판: 1층 큰 간판 + 층마다 창문 아래 가로 간판(면이 넓으면 둘로 나눔) + 돌출 간판 하나
    static void KTowerSigns(Transform g, int xSide, int zSide, float x0, float z0, float w, float d, int floors, System.Random r)
    {
        foreach (int side in new[] { zSide, xSide })
        {
            float len = side < 2 ? w : d;
            KSignBand(g, side, x0, z0, w, d, 2.95f, 3.95f, r.Next(KTraderShop), r.NextDouble() < 0.12 ? 2 : (r.NextDouble() < 0.3 ? 1 : 0), r.NextDouble() < 0.2, r);
            for (int f = 1; f < floors; f++)
            {
                float y0 = 3.5f + f * 3.2f + 0.05f, y1 = y0 + 0.8f;
                if (len > 9f && r.NextDouble() < 0.6)
                {
                    float cut = len * KF(r, 0.4f, 0.6f);
                    int s1 = r.Next(KTraderShop), s2 = (s1 + 1 + r.Next(KTraderShop - 1)) % KTraderShop; // 서로 다른 상호
                    KSignBand(g, side, x0, z0, w, d, y0, y1, s1, r.NextDouble() < 0.25 ? 1 : 0, r.NextDouble() < 0.08, r, 0.15f, cut - 0.1f);
                    KSignBand(g, side, x0, z0, w, d, y0, y1, s2, r.NextDouble() < 0.25 ? 1 : 0, r.NextDouble() < 0.08, r, cut + 0.1f, len - 0.15f);
                }
                else if (r.NextDouble() < 0.8)
                    KSignBand(g, side, x0, z0, w, d, y0, y1, r.Next(KTraderShop), r.NextDouble() < 0.25 ? 1 : 0, r.NextDouble() < 0.08, r, len * 0.1f, len * 0.9f);
            }
        }
        int ss = r.NextDouble() < 0.5 ? xSide : zSide;
        KSideSign(g, ss, (ss < 2 ? w : d) - 0.7f, 3.5f + 3.2f * (1 + r.Next(Mathf.Max(1, floors - 1))) + 0.6f, x0, z0, w, d, r.Next(KSideSigns.Length));
    }

    // ---------- 간판 ----------

    // 가로 간판: 바깥 면 side의 벽 위 (y 2.95~3.95) 전체 폭. kind: 0 걸림, 1 기움, 2 떨어짐(인도에 기대 누움). lit이면 켜진 재질 + 깜빡이는 등
    // a0~a1: 면을 따라 간판이 차지하는 구간 (a1 < 0이면 면 전체)
    // doorAlong: 면을 따라 잰 문 중심(음수면 문 없음). 떨어진 간판(kind 2)이 문 앞을 막지 않게 한다
    static void KSignBand(Transform g, int side, float X0, float Z0, float W, float D, float y0, float y1, int shop, int kind, bool lit, System.Random r, float a0 = 0f, float a1 = -1f, float doorAlong = -1f)
    {
        float faceLen = side < 2 ? W : D;
        if (a1 < 0f) a1 = faceLen;
        bool flat = false;
        if (kind == 2 && doorAlong >= 0f)
        {
            // 떨어진 간판이 문 앞을 막으면 안의 좀비가 못 나오고 플레이어도 못 들어간다: 문에서 2.4m 이상 떨어진 긴 쪽 구간에만 기대 눕히고,
            // 그 구간이 3m도 안 되면 인도 바깥쪽(벽에서 1.6~2.6m)에 납작하게 눕혀 벽을 따라 지나갈 길을 남긴다
            float left = doorAlong - 2.4f - a0, right = a1 - (doorAlong + 2.4f);
            if (Mathf.Max(left, right) >= 3f) { if (left >= right) a1 = doorAlong - 2.4f; else a0 = doorAlong + 2.4f; }
            else flat = true;
        }
        float len = a1 - a0;
        float mid = (a0 + a1) / 2f;
        var outN = KSideOut(side);
        var center = FacePoint(side, mid, (y0 + y1) / 2f, X0, Z0, W, D) + outN * 0.12f;
        float yaw = side == 0 ? 0f : side == 1 ? 180f : side == 2 ? -90f : 90f; // 로컬 -z가 바깥을 보게 (면 쿼드가 -z를 본다)
        var sg = Group("Sign", g, center, yaw);
        float h = y1 - y0, dep = 0.22f;
        var mat = lit ? M["signLit" + shop] : M["sign" + shop];
        if (kind == 2)
        {
            // 떨어진 간판: 인도에 비스듬히 기대 누움 (벽에는 자국만)
            Box("Ghost", g, FacePoint(side, mid, (y0 + y1) / 2f, X0, Z0, W, D) + outN * 0.004f, side < 2 ? new Vector3(len - 0.3f, h - 0.1f, 0.006f) : new Vector3(0.006f, h - 0.1f, len - 0.3f), M["soot"], 1f, false);
            if (flat)
            {
                // 문 앞을 비우려고 인도 바깥쪽에 얼굴을 위로 납작하게 (벽과 간판 사이 1.6m 통로)
                sg.localPosition = FacePoint(side, mid + KF(r, -1f, 1f), 0, X0, Z0, W, D) + outN * 2.1f + Vector3.up * (dep / 2f + 0.02f);
                sg.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90f, KF(r, -8f, 8f), 0);
            }
            else
            {
                sg.localPosition = FacePoint(side, mid + KF(r, -1f, 1f), 0, X0, Z0, W, D) + outN * 0.9f;
                sg.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(-62f, KF(r, -8f, 8f), 0);
                sg.localPosition += Vector3.up * 0.45f;
            }
        }
        else if (kind == 1)
        {
            // 한쪽 걸이가 빠져 기운 간판
            sg.localRotation *= Quaternion.Euler(0, 0, KF(r, 7f, 14f) * (r.NextDouble() < 0.5 ? 1f : -1f));
            sg.localPosition += Vector3.up * -0.1f;
        }
        Box("Body", sg, new Vector3(0, 0, 0), new Vector3(len - 0.2f, h, dep), M["tire"], 1f, kind == 2);
        var face = Prim(PrimitiveType.Quad, "Face", sg, new Vector3(0, 0, -dep / 2f - 0.002f), new Vector3(len - 0.26f, h - 0.06f, 1f), mat);
        face.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        // 틀 (위·아래·양 끝)
        var trim = new List<CombineInstance>();
        KPart(trim, new Vector3(len - 0.16f, 0.04f, dep + 0.02f), 1f, new Vector3(0, h / 2f, 0), Quaternion.identity);
        KPart(trim, new Vector3(len - 0.16f, 0.04f, dep + 0.02f), 1f, new Vector3(0, -h / 2f, 0), Quaternion.identity);
        foreach (float x in new[] { -(len - 0.2f) / 2f, (len - 0.2f) / 2f })
            KPart(trim, new Vector3(0.04f, h + 0.02f, dep + 0.02f), 1f, new Vector3(x, 0, 0), Quaternion.identity);
        // 걸이 (벽 쪽 받침 둘)
        if (kind != 2)
            foreach (float x in new[] { -len * 0.35f, len * 0.35f })
                KPart(trim, new Vector3(0.06f, 0.06f, 0.14f), 1f, new Vector3(x, h / 2f - 0.1f, dep / 2f + 0.06f), Quaternion.identity);
        CombineInto(sg, "Trim", trim, M["steelGrey"]);
        if (lit && kind != 2)
        {
            var l = PointLight("SignLight", sg, new Vector3(0, -h / 2f - 0.2f, -0.7f), new Color(1f, 0.92f, 0.8f), 5f, 8f);
            Flicker(l, 0.45f, 11f, true);
            // 고장 난 전기: 글자 빛이 들어왔다 나가고 가끔 노란 스파크가 파지직 튄다 (런타임 SignFlicker)
            var sf = sg.gameObject.AddComponent<SignFlicker>();
            sf.face = face.GetComponent<MeshRenderer>();
            sf.lamp = l;
            sf.sparkLocal = new Vector3(KF(r, -len * 0.35f, len * 0.35f), -h / 2f + 0.02f, -dep / 2f);
            sf.sparkNormal = Vector3.back;
            sf.sparkChance = (float)KF(r, 0.25f, 0.5f);
        }
    }

    // 돌출 간판: 바깥 면 side의 along 자리, y 높이에 벽에서 0.55m 나와 선다 (브래킷 둘)
    static void KSideSign(Transform g, int side, float along, float y, float X0, float Z0, float W, float D, int index)
    {
        var outN = KSideOut(side);
        var p = FacePoint(side, along, y, X0, Z0, W, D);
        var sg = Group("SideSign", g, p + outN * 0.45f, side < 2 ? 90f : 0f);
        Box("Body", sg, Vector3.zero, new Vector3(0.14f, 1.6f, 0.46f), M["tire"], 1f, false);
        foreach (float s in new[] { -1f, 1f })
        {
            var face = Prim(PrimitiveType.Quad, "Face", sg, new Vector3(s * 0.072f, 0, 0), new Vector3(0.44f, 1.56f, 1f), M["sideSign" + index], new Vector3(0, s > 0 ? -90f : 90f, 0));
            face.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        var rods = new List<CombineInstance>();
        var toWall = sg.InverseTransformDirection(-outN) * 0.42f;
        foreach (float dy in new[] { 0.55f, -0.55f })
            KRod(rods, new Vector3(0, dy, 0), toWall + new Vector3(0, dy, 0), 0.02f);
        CombineInto(sg, "Brackets", rods, M["steelGrey"]);
    }

    // ---------- 창문·실외기 ----------

    // 벽 구멍 창문 꾸밈 (2층): 흰 새시 틀 + 유리(막음). 깨진 창은 유리 대신 조각. 아래 실외기(절반)
    static void KWindowDress(Transform wall, float c, Op op, float outSign, float t, System.Random r)
    {
        var g = Group("WindowDress", wall, new Vector3(c, 0, 0));
        float fz = outSign * (t / 2f - 0.05f);
        float h = op.top - op.bottom, cy = (op.bottom + op.top) / 2f;
        var sash = new List<CombineInstance>();
        KPart(sash, new Vector3(op.w, 0.06f, 0.08f), 1f, new Vector3(0, op.bottom + 0.03f, fz), Quaternion.identity);
        KPart(sash, new Vector3(op.w, 0.06f, 0.08f), 1f, new Vector3(0, op.top - 0.03f, fz), Quaternion.identity);
        foreach (float x in new[] { -op.w / 2f + 0.03f, 0f, op.w / 2f - 0.03f })
            KPart(sash, new Vector3(0.06f, h, 0.08f), 1f, new Vector3(x, cy, fz), Quaternion.identity);
        KPart(sash, new Vector3(op.w + 0.1f, 0.05f, 0.16f), 1f, new Vector3(0, op.bottom - 0.025f, fz + outSign * 0.04f), Quaternion.identity); // 창턱
        CombineInto(g, "Sash", sash, M["whitePaint"]);
        bool broken = r.NextDouble() < 0.3;
        if (!broken) Box("Glass", g, new Vector3(0, cy, fz), new Vector3(op.w - 0.08f, h - 0.08f, 0.01f), M["glass"], 1f, true).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        else
        {
            var shards = new List<CombineInstance>();
            for (int i = 0, n = 3 + r.Next(4); i < n; i++)
                KPart(shards, new Vector3(KF(r, 0.08f, 0.3f), KF(r, 0.1f, 0.35f), 0.01f), 1f, new Vector3(KF(r, -op.w / 2f + 0.12f, op.w / 2f - 0.12f), r.NextDouble() < 0.5 ? op.top - 0.2f : op.bottom + 0.2f, fz), Quaternion.Euler(0, 0, KF(r, -25f, 25f)));
            CombineInto(g, "Shards", shards, M["glass"]);
            ColliderBox("WindowGuard", g, new Vector3(0, cy, fz), new Vector3(op.w, h, 0.06f), Vector3.zero);
        }
        if (r.NextDouble() < 0.5) KAcUnit(g, new Vector3(KF(r, -op.w * 0.25f, op.w * 0.25f), op.bottom - 0.45f, outSign * (t / 2f + 0.18f)), outSign > 0 ? Quaternion.identity : Quaternion.Euler(0, 180f, 0), r);
    }

    // 실외기: 상자 + 앞면 팬 격자(로컬 +z, rot이 바깥을 보게 돌린다) + 받침 브래킷 + 배관
    static void KAcUnit(Transform parent, Vector3 pos, Quaternion rot, System.Random r)
    {
        var g = Group("AcUnit", parent, pos);
        g.localRotation = rot * Quaternion.Euler(0, 0, KF(r, -2f, 2f));
        const float outSign = 1f;
        Box("Body", g, Vector3.zero, new Vector3(0.78f, 0.54f, 0.3f), M["acUnit"], 1f, true);
        var fan = new GameObject("Fan");
        fan.transform.SetParent(g, false);
        fan.transform.localPosition = new Vector3(-0.12f, 0.02f, 0.155f);
        fan.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        fan.AddComponent<MeshFilter>().sharedMesh = KDisc("ac_fan", 0.2f, 20, 0.004f);
        fan.AddComponent<MeshRenderer>().sharedMaterial = M["grate"];
        GameObjectUtility.SetStaticEditorFlags(fan, StaticEditorFlags.BatchingStatic);
        var bits = new List<CombineInstance>();
        foreach (float x in new[] { -0.3f, 0.3f })
        {
            KBar(bits, new Vector3(0.04f, 0.04f, 0.34f), new Vector3(x, -0.29f, -outSign * 0.02f), Quaternion.identity); // 브래킷 팔은 뚜껑 없는 막대
            KRod(bits, new Vector3(x, -0.29f, outSign * 0.14f), new Vector3(x, -0.6f, -outSign * 0.16f), 0.015f);
        }
        KRod(bits, new Vector3(0.3f, 0.1f, -outSign * 0.16f), new Vector3(0.3f, 0.9f, -outSign * 0.17f), 0.02f);
        CombineInto(g, "Bracket", bits, M["steelGrey"]);
    }

    // 위층 겉모습 창문 (들어갈 수 없는 층): 흰 틀 + 어두운 유리, 실외기 더러, 깨진 창 조금. FacadeWindows와 같은 자리·같은 수
    static void KUpperWindows(Transform g, float x0, float z0, float w, float d, float baseY, int floors, System.Random r)
    {
        var glass = new List<CombineInstance>();
        var sash = new List<CombineInstance>();
        var shards = new List<CombineInstance>();
        for (int f = 0; f < floors; f++)
        {
            float y = baseY + f * 3.2f + 1.7f;
            for (int side = 0; side < 4; side++)
            {
                float len = side < 2 ? w : d;
                int cols = Mathf.Max(1, Mathf.FloorToInt(len / 3f));
                for (int i = 0; i < cols; i++)
                {
                    if (r.NextDouble() < 0.06) continue;
                    float u = (i + 0.5f) * len / cols;
                    var outN = KSideOut(side);
                    var p = FacePoint(side, u, y, x0, z0, w, d) + outN * 0.03f;
                    var q = side < 2 ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
                    bool broken = r.NextDouble() < 0.12;
                    if (!broken) glass.Add(new CombineInstance { mesh = BoxMesh(new Vector3(1.04f, 1.44f, 0.04f), 1f), transform = Matrix4x4.TRS(p, q, Vector3.one) });
                    else
                        for (int s = 0; s < 3; s++)
                            shards.Add(new CombineInstance { mesh = BoxMesh(new Vector3(KF(r, 0.1f, 0.3f), KF(r, 0.12f, 0.35f), 0.03f), 1f), transform = Matrix4x4.TRS(p + q * new Vector3(KF(r, -0.4f, 0.4f), s == 0 ? 0.55f : -0.55f, 0), q * Quaternion.Euler(0, 0, KF(r, -25f, 25f)), Vector3.one) });
                    // 틀: 위·아래·좌·우·가운데
                    KBar(sash, new Vector3(1.14f, 0.05f, 0.08f), p + q * new Vector3(0, 0.75f, 0), q); // 틀은 뚜껑 없는 막대 (메시 용량)
                    KBar(sash, new Vector3(1.14f, 0.07f, 0.14f), p + q * new Vector3(0, -0.76f, 0), q);
                    foreach (float x in new[] { -0.55f, 0f, 0.55f })
                        KBar(sash, new Vector3(0.05f, 1.5f, 0.08f), p + q * new Vector3(x, 0, 0), q);
                    if (r.NextDouble() < 0.35) KAcUnit(g, p + outN * 0.18f + Vector3.up * -1.2f + q * new Vector3(KF(r, -0.2f, 0.2f), 0, 0), Quaternion.LookRotation(outN, Vector3.up), r);
                }
            }
        }
        CombineInto(g, "UpperGlass", glass, M["glass"]);
        CombineInto(g, "UpperSash", sash, M["whitePaint"]);
        CombineInto(g, "UpperShards", shards, M["glass"]);
    }

    // ---------- 옥상·홈통·계량기 ----------

    // 옥상: 난간(파라펫)과 갓돌, 물탱크(선 것·쓰러진 것), 옥탑방(가끔), 안테나, 배관
    static void KRooftop(Transform g, float X0, float Z0, float W, float D, float roofY, System.Random r)
    {
        var top = Group("Rooftop", g, new Vector3(X0 + W / 2f, roofY, Z0 + D / 2f));
        const float ph = 0.9f, pt = 0.2f;
        var par = new List<CombineInstance>();
        KPart(par, new Vector3(W, ph, pt), 2f, new Vector3(0, ph / 2f, -D / 2f + pt / 2f), Quaternion.identity);
        KPart(par, new Vector3(W, ph, pt), 2f, new Vector3(0, ph / 2f, D / 2f - pt / 2f), Quaternion.identity);
        KPart(par, new Vector3(pt, ph, D - pt * 2f), 2f, new Vector3(-W / 2f + pt / 2f, ph / 2f, 0), Quaternion.identity);
        KPart(par, new Vector3(pt, ph, D - pt * 2f), 2f, new Vector3(W / 2f - pt / 2f, ph / 2f, 0), Quaternion.identity);
        CombineInto(top, "Parapet", par, M["dirty"]);
        var cap = new List<CombineInstance>();
        KPart(cap, new Vector3(W + 0.06f, 0.06f, pt + 0.06f), 1f, new Vector3(0, ph + 0.03f, -D / 2f + pt / 2f), Quaternion.identity);
        KPart(cap, new Vector3(W + 0.06f, 0.06f, pt + 0.06f), 1f, new Vector3(0, ph + 0.03f, D / 2f - pt / 2f), Quaternion.identity);
        KPart(cap, new Vector3(pt + 0.06f, 0.06f, D - pt * 2f), 1f, new Vector3(-W / 2f + pt / 2f, ph + 0.03f, 0), Quaternion.identity);
        KPart(cap, new Vector3(pt + 0.06f, 0.06f, D - pt * 2f), 1f, new Vector3(W / 2f - pt / 2f, ph + 0.03f, 0), Quaternion.identity);
        CombineInto(top, "Coping", cap, M["floor"]);
        // 물탱크: 쇠 받침 위 노란(또는 파란) 통. 셋 중 하나는 쓰러져 굴렀다
        var tankPos = new Vector3(KF(r, -W / 2f + 1.5f, W / 2f - 1.5f), 0, KF(r, -D / 2f + 1.5f, D / 2f - 1.5f));
        var tankMat = r.NextDouble() < 0.5 ? M["blastStripe"] : M["paintBlue"];
        var legs = new List<CombineInstance>();
        bool toppled = r.NextDouble() < 0.33;
        foreach (float sx in new[] { -0.6f, 0.6f })
            foreach (float sz in new[] { -0.6f, 0.6f })
                KPart(legs, new Vector3(0.08f, 0.9f, 0.08f), 1f, tankPos + new Vector3(sx, 0.45f, sz), Quaternion.identity);
        KPart(legs, new Vector3(1.5f, 0.06f, 0.06f), 1f, tankPos + new Vector3(0, 0.9f, -0.6f), Quaternion.identity);
        KPart(legs, new Vector3(1.5f, 0.06f, 0.06f), 1f, tankPos + new Vector3(0, 0.9f, 0.6f), Quaternion.identity);
        CombineInto(top, "TankStand", legs, M["steelGrey"]);
        var tank = Group("Tank", top, toppled ? tankPos + new Vector3(KF(r, 1.2f, 1.8f), 0.85f, KF(r, -0.6f, 0.6f)) : tankPos + Vector3.up * 0.95f);
        if (toppled) tank.localRotation = Quaternion.Euler(KF(r, 80f, 95f), KF(r, 0, 360), 0);
        var tk = new List<CombineInstance>();
        KCylAt(tk, new Vector3(0, 0.8f, 0), 0.85f, 1.6f, Quaternion.identity);
        KCylAt(tk, new Vector3(0, 1.62f, 0), 0.6f, 0.08f, Quaternion.identity);
        KCylAt(tk, new Vector3(0, 1.7f, 0), 0.2f, 0.1f, Quaternion.identity);
        CombineInto(tank, "Shell", tk, tankMat);
        // 옥탑방 (셋 중 하나): 작은 방 + 문 + 창
        if (r.NextDouble() < 0.33 && W > 8f && D > 8f)
        {
            var room = Group("RoofRoom", top, new Vector3(-W / 2f + 2.2f, 0, D / 2f - 2.0f));
            Box("Walls", room, new Vector3(0, 1.2f, 0), new Vector3(3.2f, 2.4f, 2.6f), M["wall"], 2f);
            Box("Roof", room, new Vector3(0, 2.46f, 0), new Vector3(3.5f, 0.12f, 2.9f), M["metal"], 1.5f, false);
            Box("Door", room, new Vector3(0.6f, 1.0f, 1.31f), new Vector3(0.85f, 2.0f, 0.04f), M["metal"], 1f, false);
            Box("Win", room, new Vector3(-0.8f, 1.4f, 1.31f), new Vector3(0.9f, 0.8f, 0.04f), M["glass"], 1f, false);
        }
        // 안테나·배관·환기구
        var misc = new List<CombineInstance>();
        var ant = new Vector3(W / 2f - 1.0f, 0, -D / 2f + 1.0f);
        KRod(misc, ant, ant + new Vector3(0, 3.2f, 0), 0.02f);
        foreach (float ay in new[] { 2.4f, 2.8f, 3.1f })
            KPart(misc, new Vector3(0.02f, 0.02f, 1.1f - (ay - 2.4f) * 0.8f), 1f, ant + new Vector3(0, ay, 0), Quaternion.identity);
        var vent = new Vector3(KF(r, -W / 2f + 1f, W / 2f - 1f), 0, KF(r, -D / 2f + 1f, 0));
        KCylAt(misc, vent + new Vector3(0, 0.5f, 0), 0.18f, 1.0f, Quaternion.identity);
        KSphereAt(misc, vent + new Vector3(0, 1.05f, 0), 0.26f);
        KRod(misc, new Vector3(-W / 2f + 0.4f, 0.15f, -D / 2f + 0.5f), new Vector3(W / 2f - 0.4f, 0.15f, -D / 2f + 0.5f), 0.05f);
        CombineInto(top, "Misc", misc, M["steelGrey"]);
    }

    // 빗물 홈통: 바깥 면 side의 along 자리에서 roofY부터 바닥까지 (브래킷 셋, 아래 굽은 끝)
    static void KDownpipe(Transform g, int side, float along, float X0, float Z0, float W, float D, float roofY)
    {
        var outN = KSideOut(side);
        var top = FacePoint(side, along, roofY, X0, Z0, W, D) + outN * 0.09f;
        var bottom = FacePoint(side, along, 0.3f, X0, Z0, W, D) + outN * 0.09f;
        var parts = new List<CombineInstance>();
        KRod(parts, top, bottom, 0.05f);
        KRod(parts, bottom, bottom + outN * 0.35f + Vector3.down * 0.25f, 0.05f);
        for (int i = 0; i < 3; i++)
            KPart(parts, new Vector3(0.14f, 0.06f, 0.1f), 1f, Vector3.Lerp(top, bottom, (i + 0.5f) / 3f) - outN * 0.04f, Quaternion.Euler(0, side < 2 ? 0 : 90, 0));
        CombineInto(g, "Downpipe", parts, M["steelGrey"]);
    }

    // 계량기함: 문 옆 벽에 붙은 작은 회색 상자 (창 하나)
    static void KMeterBox(Transform wall, float c, float outSign, float t)
    {
        var g = Group("MeterBox", wall, new Vector3(c, 1.5f, outSign * (t / 2f + 0.07f)));
        Box("Box", g, Vector3.zero, new Vector3(0.36f, 0.5f, 0.14f), M["acUnit"], 1f, false);
        Box("Window", g, new Vector3(0, 0.06f, outSign * 0.072f), new Vector3(0.16f, 0.12f, 0.004f), M["glass"], 1f, false);
    }

    // 현수막: 두 점 사이에 걸린 천 (줄로 묶임), 바람에 살짝 기움. a·b = 천의 양 끝 (부모 로컬, 높이 포함), outN = 글자가 보이는 쪽
    static void KBanner(Transform parent, Vector3 a, Vector3 b, Vector3 outN, int index, System.Random r)
    {
        var mid = (a + b) / 2f;
        float len = (b - a).magnitude;
        var dir = Vector3.Cross(outN, Vector3.up).normalized; // 로컬 +x 방향: 쿼드의 -z가 outN을 본다
        var g = Group("Banner", parent, mid, Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg);
        g.localRotation *= Quaternion.Euler(0, 0, KF(r, -3f, 3f));
        float h = len * 0.1875f;
        var cloth = Prim(PrimitiveType.Quad, "Cloth", g, Vector3.zero, new Vector3(len, h, 1f), M["banner" + index]);
        var mr = cloth.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        var back = Prim(PrimitiveType.Quad, "ClothBack", g, new Vector3(0, 0, 0.004f), new Vector3(len, h, 1f), M["banner" + index], new Vector3(0, 180f, 0));
        back.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var ropes = new List<CombineInstance>();
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sy in new[] { -1f, 1f })
                KRod(ropes, new Vector3(sx * len / 2f, sy * h / 2f, 0), new Vector3(sx * (len / 2f + 0.35f), sy * h / 2f + 0.1f, 0), 0.008f);
        CombineInto(g, "Ropes", ropes, M["cloth"]);
    }
}
