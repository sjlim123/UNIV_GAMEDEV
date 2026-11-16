using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// GameSceneBuilder의 아파트 창틀 카드 — 그래픽 품질 '낮음' 전용 (맵 세션 '31', 2026-11-17. 11-16 미리보기 뒤 사용자 요청: 저사양만 카드, 고사양은 원본 그대로):
//  도시 아파트 4층부터의 창틀(새시 네 변과 앞 창의 가운데 새시)·유리·발코니 난간을 두 벌 만든다.
//  "WindowsFull" = 예전 막대 메시 그대로 (중간·높음), "WindowsCards" = 알파 클립 사각형 한 장(카드)에 그린 그림 (낮음, 꺼 둔 채 저장).
//  동 뿌리의 FacadeDetail(Assets/Scripts/City/FacadeDetail.cs)이 GameSettings.Quality에 맞춰 하나만 켠다.
//  카드 창 하나 = 틀 카드(벽 +0.07 = 예전 막대 앞면, M_WinFrame) + 유리 사각형(벽 +0.05, M_DarkWindow).
//  깨진 창은 같은 틀 카드 + 검은 구멍 사각형(벽 +0.035 = 예전 구멍 상자 앞면, M_Soot). 유리 조각 상자 둘은 두 벌이 같이 쓴다 (늘 켜진 Shards).
//  난간은 카드 한 장(벽 +0.15 = 예전 가로대 앞면). 실외기·층 띠·몸통은 그대로다.
//  난수: 막대 벌을 예전과 같은 함수(KAptWindow)로 만들고, 카드는 그 결과(깨졌는지)로 난수 없이 더한다 → 뒤에 이어 쓰는 실외기·그을음·파손 자리가 그대로다.
//  BuildApartment의 cardFrom 기본값은 '카드 없음'이라 102동 보스 씬(Apt102)·컷씬의 아파트는 막대 메시 한 벌 그대로다. 도시만 KAptCardFrom을 넘긴다.
//  그림 칸은 도시 아파트의 창 크기(앞 2.25×1.6, 뒤 1.1×1.2, 계단실 0.9×1.0 m)에 맞춰 그렸다. 다른 크기의 창에 쓰면 틀 굵기가 늘거나 준다.
public static partial class GameSceneBuilder
{
    // 도시 아파트에서 낮은 품질일 때 카드로 바꾸는 첫 층 (0 = 1층): 3 = 4층부터. 1~3층은 마당에서 가까이·비스듬히 보이므로 늘 막대 메시
    const int KAptCardFrom = 3;

    // ---------- 그림 칸 ----------

    const int KWinTex = 1024;
    const int KWinFront = 0, KWinBack = 1, KWinStair = 2, KWinRail = 3;

    // 칸 (픽셀, 왼쪽 아래 기준). 창 칸은 약 240px/m (5cm 틀 = 12px), 난간 칸은 400px/m (2cm 살 = 8px).
    // 칸 사이 32px·가장자리 16px 여백: 밉맵에서 이웃 칸이 섞이지 않게
    static readonly RectInt[] KWinCells =
    {
        new RectInt(16, 612, 560, 396),  // 앞 발코니 창 + 가운데 새시: 카드 2.33 × 1.65 m
        new RectInt(608, 708, 284, 300), // 뒤 창: 1.18 × 1.25 m
        new RectInt(608, 424, 236, 252), // 계단실 창: 0.98 × 1.05 m
        new RectInt(16, 16, 900, 128),   // 발코니 난간: 2.25 × 0.32 m
    };

    // 칸마다 그린 창 크기 (m, ww × wh = 예전 KAptWindow의 유리 크기). 창 카드는 바깥 틀까지 (ww + 0.08) × (wh + 0.05), 난간 카드는 ww × 0.32
    static readonly Vector2[] KWinOpening = { new Vector2(2.25f, 1.6f), new Vector2(1.1f, 1.2f), new Vector2(0.9f, 1.0f), new Vector2(2.25f, 0.32f) };

    // 틀 색 (재질 색 0.8·0.8·0.78을 곱하기 전. M_WhitePaint와 같은 재질 색이라 결과 밝기가 같다): dirty_concrete 그림 평균 (선형 0.2547·0.2333·0.1934 → sRGB)
    static readonly Color KWinFrameCol = new Color(0.542f, 0.520f, 0.477f);
    // 난간 색: M_AptRail(녹슨 쇠 그림 평균 × 0.35·0.36·0.38, 금속성 0.5)의 확산광 밝기를 이 재질(금속성 0, 색 0.8)로 맞춘 값
    static readonly Color KWinRailCol = new Color(0.204f, 0.174f, 0.125f);

    // 칸의 UV (반 픽셀 안쪽: 카드 가장자리가 칸 바깥 여백을 섞지 않게)
    static Rect KWinUV(int fam)
    {
        var c = KWinCells[fam];
        return Rect.MinMaxRect((c.x + 0.5f) / KWinTex, (c.y + 0.5f) / KWinTex, (c.x + c.width - 0.5f) / KWinTex, (c.y + c.height - 0.5f) / KWinTex);
    }

    // ---------- 그림·재질 (도시 생성기만 부른다) ----------

    // 도시 씬만 생성에서는 있는 그림·재질을 바꾸지 않는다 (그림은 같은 바이트면 다시 쓰지 않고, 재질은 KKeep으로 그대로 둔다).
    // 재질을 처음 만들 때는 저장 안 한 에셋이 없을 때 이 함수만 따로 부른다: URP가 새 재질을 들여온 다음 에디터 틱에 프로젝트 전체를 저장한다
    static void KWindowMaterials()
    {
        KWinAtlasPixels(out var albedo, out var normal);
        var atlas = KSaveTex("win_frame", albedo, KWinTex, KWinTex, KTexKind.ColorAlpha, false, true);
        var atlasN = KSaveTex("win_frame_normal", normal, KWinTex, KWinTex, KTexKind.Normal, false);
        M["winFrame"] = KCardMat("M_WinFrame", atlas, atlasN);
    }

    // 알파 클립 카드 재질: M_WhitePaint와 같은 색·매끈함 + KPaintMat과 같은 클립 설정 (큐 2450 = URP가 알파 클립에 스스로 고르는 값). 발광 키워드 없음
    static Material KCardMat(string name, Texture2D albedo, Texture2D normal)
    {
        var m = KLitMat(name, albedo, normal, new Color(0.8f, 0.8f, 0.78f), 0.3f, 0f, 0.8f);
        if (KKeep(m)) return m;
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_AlphaToMask", 1f); // URP가 알파 클립 재질을 들여올 때 스스로 1로 바꾼다 (MSAA가 꺼져 있어 화면은 같다). 미리 맞춰 두면 불러온 뒤 '저장 안 됨'이 되지 않는다
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    // 그림 전체: 칸마다 틀·난간을 그리고(알파 = 4×4 표본 중 막대 안 비율), 잘라 낸 픽셀의 색을 가장 가까운 칠한 색으로 채운 뒤
    // (밉맵·필터가 막대 가장자리를 검은색과 섞어 어둡게 만들지 않게), 높이 그림(둥근 모서리)으로 노멀을 만든다
    static void KWinAtlasPixels(out Color32[] albedo, out Color32[] normal)
    {
        var px = new Color[KWinTex * KWinTex];
        var hgt = new float[KWinTex * KWinTex];
        for (int fam = 0; fam < KWinCells.Length; fam++) KWinPaintCell(px, hgt, fam);
        KWinDilate(px, KWinTex, 32, KWinFrameCol);
        albedo = new Color32[px.Length];
        for (int k = 0; k < px.Length; k++) albedo[k] = KC(px[k]);
        normal = KNormalFromHeight(hgt, KWinTex, 2f);
    }

    // 칸 하나. 창: 위·아래 가로대(바깥 폭 ww+0.08)와 양옆 세로대(창 가장자리 ±ww/2), 앞 창은 가운데 새시. 막대는 모두 5cm (예전 KAptWindow·KBar 치수 그대로).
    // 난간: 위 가로대 4cm와 살 넷 2cm (예전 막대 치수 그대로)
    static void KWinPaintCell(Color[] px, float[] hgt, int fam)
    {
        var c = KWinCells[fam];
        var open = KWinOpening[fam];
        bool rail = fam == KWinRail;
        float W = rail ? open.x : open.x + 0.08f, H = rail ? 0.32f : open.y + 0.05f;
        float sx = c.width / W, sy = c.height / H;
        var bars = new List<Rect>();
        if (rail)
        {
            bars.Add(new Rect(0f, H - 0.04f, W, 0.04f));
            for (int q = 0; q < 4; q++) bars.Add(new Rect((q + 0.5f) * W / 4f - 0.01f, 0f, 0.02f, 0.30f));
        }
        else
        {
            bars.Add(new Rect(0f, H - 0.05f, W, 0.05f));
            bars.Add(new Rect(0f, 0f, W, 0.05f));
            bars.Add(new Rect(0.015f, 0.025f, 0.05f, open.y));
            bars.Add(new Rect(W - 0.065f, 0.025f, 0.05f, open.y));
            if (fam == KWinFront) bars.Add(new Rect(W * 0.5f - 0.025f, 0.025f, 0.05f, open.y));
        }
        var baseCol = rail ? KWinRailCol : KWinFrameCol;
        float bevelW = rail ? 0.005f : 0.01f;
        int seed = 5300 + fam * 31;
        Vector3 sum = Vector3.zero;
        int full = 0;
        for (int j = 0; j < c.height; j++)
            for (int i = 0; i < c.width; i++)
            {
                int gx = c.x + i, gy = c.y + j, k = gy * KWinTex + gx;
                int inside = 0;
                for (int sj = 0; sj < 4; sj++)
                    for (int si = 0; si < 4; si++)
                        if (KWinInBars(bars, (i + (si + 0.5f) * 0.25f) / sx, (j + (sj + 0.5f) * 0.25f) / sy) > 0f) inside++;
                if (inside == 0) { px[k] = new Color(0f, 0f, 0f, 0f); hgt[k] = 0f; continue; }
                float mx = (i + 0.5f) / sx, my = (j + 0.5f) / sy;
                float bevel = Smooth01(0f, bevelW, KWinInBars(bars, mx, my));
                var col = baseCol * (0.9f + 0.2f * GrimeFbm(gx, gy, KWinTex, 64, 3, seed));
                float dirt = Smooth01(0.55f, 0.85f, GrimeFbm(gx, gy, KWinTex, 16, 3, seed + 5)) * 0.35f * (1f - 0.5f * my / H); // 아래쪽일수록 때
                col = Color.Lerp(col, baseCol * 0.45f, dirt);
                col *= 0.9f + 0.1f * bevel; // 모서리는 조금 어둡게 (막대 옆면 그늘)
                col.a = inside / 16f;
                px[k] = col;
                hgt[k] = 0.5f * bevel;
                if (inside == 16) { sum += new Vector3(col.r, col.g, col.b); full++; }
            }
        // 칸 평균을 기준 색에 맞춘다 (얼룩·모서리로 어두워진 만큼): 카드 층과 막대 층의 틀 밝기가 같게
        if (full == 0) return;
        var mean = sum / full;
        float gr = baseCol.r / mean.x, gg = baseCol.g / mean.y, gb = baseCol.b / mean.z;
        for (int j = 0; j < c.height; j++)
            for (int i = 0; i < c.width; i++)
            {
                int k = (c.y + j) * KWinTex + c.x + i;
                if (px[k].a > 0f) px[k] = new Color(px[k].r * gr, px[k].g * gg, px[k].b * gb, px[k].a);
            }
    }

    // 막대 모음 안에서 가장 가까운 막대 가장자리까지 거리 (m). 막대가 겹친 곳은 큰 쪽 (이음매에 모서리가 생기지 않게). 모든 막대 밖이면 0 이하
    static float KWinInBars(List<Rect> bars, float x, float y)
    {
        float best = -1f;
        foreach (var b in bars)
        {
            float d = Mathf.Min(Mathf.Min(x - b.xMin, b.xMax - x), Mathf.Min(y - b.yMin, b.yMax - y));
            if (d > best) best = d;
        }
        return best;
    }

    // 잘라 낸 픽셀(알파 0)의 색을 가장 가까운 칠한 픽셀의 색으로 채운다 (알파는 0 그대로, reach 픽셀까지, 그 밖은 fill)
    static void KWinDilate(Color[] px, int n, int reach, Color fill)
    {
        var dist = new int[px.Length];
        var queue = new Queue<int>();
        for (int k = 0; k < px.Length; k++)
        {
            if (px[k].a > 0f) { dist[k] = 0; queue.Enqueue(k); }
            else dist[k] = -1;
        }
        while (queue.Count > 0)
        {
            int k = queue.Dequeue();
            if (dist[k] >= reach) continue;
            int x = k % n, y = k / n;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                int nk = ny * n + nx;
                if (dist[nk] >= 0) continue;
                dist[nk] = dist[k] + 1;
                px[nk] = new Color(px[k].r, px[k].g, px[k].b, 0f);
                queue.Enqueue(nk);
            }
        }
        for (int k = 0; k < px.Length; k++)
            if (dist[k] < 0) px[k] = new Color(fill.r, fill.g, fill.b, 0f);
    }

    // ---------- 카드 메시 ----------

    // 사각형 모음: 한 동의 틀 카드·유리·구멍을 재질마다 한 메시로 합친다 (원본 메시를 묶음에 남기지 않는다)
    sealed class KQuads
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        // c = 가운데, outN = 보이는 쪽(바깥), w·h = 크기(m), uvRect = 그림 자리, mirror = 좌우 뒤집기
        public void Add(Vector3 c, Vector3 outN, float w, float h, Rect uvRect, bool mirror = false)
        {
            var right = Vector3.Cross(outN, Vector3.up).normalized * (w * 0.5f); // 바깥에서 볼 때 오른쪽
            var up = Vector3.up * (h * 0.5f);
            int b = v.Count;
            v.Add(c - right - up); v.Add(c - right + up); v.Add(c + right + up); v.Add(c + right - up);
            for (int k = 0; k < 4; k++) n.Add(outN);
            float u0 = mirror ? uvRect.xMax : uvRect.xMin, u1 = mirror ? uvRect.xMin : uvRect.xMax;
            uv.Add(new Vector2(u0, uvRect.yMin)); uv.Add(new Vector2(u0, uvRect.yMax)); uv.Add(new Vector2(u1, uvRect.yMax)); uv.Add(new Vector2(u1, uvRect.yMin));
            t.Add(b); t.Add(b + 1); t.Add(b + 2); // 바깥에서 볼 때 시계 방향 = 앞면
            t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }
    }

    // 카드·유리·구멍 사각형은 그림자를 드리우지 않는다 (벽에 붙은 판이라 보이는 그림자가 거의 없고, 그림자 패스 정점만 는다)
    static void KQuadsInto(Transform parent, string name, KQuads q, Material mat)
    {
        if (q.v.Count == 0) return;
        var mesh = new Mesh { name = name + "_" + meshCache.Count };
        mesh.SetVertices(q.v);
        mesh.SetNormals(q.n);
        mesh.SetUVs(0, q.uv);
        mesh.SetTriangles(q.t, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(mesh, meshContainer);
        meshCache[mesh.name] = mesh;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // 카드 창 한 짝 (낮은 품질 벌). 난수를 쓰지 않는다: 같은 창의 막대 벌(KAptWindow)이 정한 broken을 받는다.
    // s = +1 앞면, -1 뒷면. at = 예전 창 가운데 (벽 +0.03)
    static void KAptCard(KQuads cards, KQuads glass, KQuads holes, Transform g, Vector3 at, float s, int fam, float ww, float wh, bool broken)
    {
        var outN = Vector3.forward * s;
        if (broken) holes.Add(at + outN * 0.005f, outN, ww, wh, new Rect(0f, 0f, ww, wh)); // 예전 구멍 상자(두께 1cm) 앞면 자리. 유리 조각은 막대 벌과 같이 쓴다
        else glass.Add(at + outN * 0.02f, outN, ww, wh, new Rect(0f, 0f, ww, wh)); // 예전 유리 상자 앞면(벽 +0.045)보다 5mm 앞
        cards.Add(at + outN * 0.04f, outN, ww + 0.08f, wh + 0.05f, KWinUV(fam), KWinHash(g.TransformPoint(at), 12) < 0.5f); // 예전 막대 앞면 (벽 +0.07)
    }

    // 카드 층의 두 벌을 동 아래에 묶는다: "WindowsFull" = 예전 막대·유리 상자 (중간·높음, 켠 채 저장), "WindowsCards" = 카드·사각형 (낮음, 꺼 둔 채 저장).
    // 동 뿌리의 FacadeDetail이 그래픽 품질에 맞춰 하나만 켠다. 막대 벌은 예전처럼 CombineInto (그림자 켬), 카드 벌은 그림자 끔
    static void KAptWindowSets(Transform g, List<CombineInstance> glassHi, List<CombineInstance> framesHi, List<CombineInstance> railsHi, List<CombineInstance> holesHi,
        KQuads cards, KQuads glass, KQuads holes)
    {
        var full = Group("WindowsFull", g, Vector3.zero);
        CombineInto(full, "Glass", glassHi, M["glass"]);
        CombineInto(full, "Frames", framesHi, M["whitePaint"]);
        CombineInto(full, "Rails", railsHi, M["aptRail"]);
        CombineInto(full, "Holes", holesHi, M["soot"]);
        var low = Group("WindowsCards", g, Vector3.zero);
        KQuadsInto(low, "WindowCards", cards, M["winFrame"]);
        KQuadsInto(low, "GlassPanes", glass, M["glass"]);
        KQuadsInto(low, "HolePanes", holes, M["soot"]);
        low.gameObject.SetActive(false);
        var detail = g.gameObject.AddComponent<FacadeDetail>();
        detail.full = full.gameObject;
        detail.cards = low.gameObject;
    }

    // 창마다 다른 값 (월드 자리 해시, 난수 흐름을 쓰지 않는다). KRandom은 y를 쓰지 않아 같은 세로줄의 창이 같은 값을 받으므로 y까지 넣는다
    static float KWinHash(Vector3 world, int salt) =>
        GrimeHash(Mathf.RoundToInt(world.x * 20f), Mathf.RoundToInt(world.y * 20f) * 7919 + Mathf.RoundToInt(world.z * 20f), salt);
}
