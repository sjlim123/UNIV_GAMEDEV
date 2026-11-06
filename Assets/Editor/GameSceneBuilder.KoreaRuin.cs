using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// GameSceneBuilder의 핵전쟁 뒤 분위기 (맵 개편 세션): 충격 구덩이(흙 둔덕·그을음·철근), 연석에 쌓인 재, 전봇대의 대피 안내문,
// 북쪽 터널 앞 검문소(모래주머니·철조망·통제 표지). 기존 폐허 요소(폭격 건물·시체·탄 차·황금 문·반딧불·해치)는 그대로 두고 그 사이를 메운다
public static partial class GameSceneBuilder
{
    static void KoreaRuinMaterials()
    {
        // 안내문·검문소에 쓰는 글자와 크기만 요청한다 (상호 전체×큰 크기를 요청하면 글꼴 텍스처가 넘쳐 에디터가 죽는다)
        const string ruinChars = "민방위대피안내소→삼일동주민센터검문통행금지군창에서떨어십시오물식량손전등참제구역허가차외진입m";
        var ruinSizes = new[] { 24, 26, 32, 34, 40, 112 };
        var pretendard = KFontFor("Fonts/Pretendard-Bold", ruinChars, ruinSizes);
        var dohyeon = KFontFor("Fonts/DoHyeon-Regular", ruinChars, ruinSizes);
        var notice = NoticePixels(dohyeon, pretendard);
        var check = CheckpointSignPixels(dohyeon, pretendard);
        KReleaseFonts();
        M["notice"] = KPaintMat("M_Notice", KSaveTex("ruin_notice", notice, 256, 384, KTexKind.ColorAlpha, false), Color.white);
        M["checkSign"] = KLitMat("M_CheckpointSign", KSaveTex("ruin_checkpoint", check, 1024, 384, KTexKind.Color, false), null, Color.white, 0.25f, 0f);
        M["earth"] = TexMat("M_Earth", "dirty_concrete", new Color(0.3f, 0.26f, 0.2f), 0.04f);
        M["sandbag"] = TexMat("M_Sandbag", "dirty_concrete", new Color(0.5f, 0.46f, 0.36f), 0.05f);
    }

    // 대피 안내문 256×384: 누런 종이, 붉은 머리띠 '민방위 대피 안내', 본문 몇 줄, 찢긴 가장자리(알파)
    static Color32[] NoticePixels(KFont big, KFont small)
    {
        const int w = 256, h = 384;
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var c = new Color(0.86f, 0.8f, 0.64f) * (0.82f + 0.25f * GrimeFbm(i, j, 512, 6, 3, 5100));
                if (j > h * 0.8f) c = new Color(0.6f, 0.1f, 0.08f) * (0.85f + 0.2f * GrimeFbm(i, j, 512, 6, 3, 5101));
                c.a = 1f;
                px[j * w + i] = c;
            }
        KText(px, w, h, big, "민방위 대피 안내", 34, w * 0.5f, h * 0.9f, Color.white, 0.5f);
        KText(px, w, h, small, "대피소 →  300m", 32, w * 0.5f, h * 0.66f, new Color(0.12f, 0.1f, 0.08f), 0.5f);
        KText(px, w, h, small, "창문에서 떨어지십시오", 24, w * 0.5f, h * 0.5f, new Color(0.2f, 0.18f, 0.15f), 0.5f);
        KText(px, w, h, small, "물 · 식량 · 손전등 지참", 24, w * 0.5f, h * 0.38f, new Color(0.2f, 0.18f, 0.15f), 0.5f);
        KText(px, w, h, small, "삼일동 주민센터", 26, w * 0.5f, h * 0.14f, new Color(0.3f, 0.12f, 0.1f), 0.5f);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float e = GrimeFbm(i, j, 512, 14, 2, 5102);
                bool gone = i < 4 + 8 * e || i > w - 4 - 8 * e || j < 4 + 10 * e || j > h - 4 - 10 * e;
                if (j < h * 0.22f && i > w * (0.55f + 0.3f * e)) gone = true; // 오른쪽 아래 찢김
                if (gone) px[j * w + i].a = 0f;
            }
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) res[k] = KC(px[k]);
        return res;
    }

    // 검문소 표지 1024×384: 흰 판에 붉은 띠 '검문소 · 통행 금지', 아래 작은 글씨
    static Color32[] CheckpointSignPixels(KFont big, KFont small)
    {
        const int w = 1024, h = 384;
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var c = new Color(0.88f, 0.88f, 0.84f) * (0.85f + 0.2f * GrimeFbm(i, j, 1024, 6, 3, 5200));
                if (j > h * 0.52f) c = new Color(0.62f, 0.1f, 0.08f) * (0.85f + 0.2f * GrimeFbm(i, j, 1024, 6, 3, 5201));
                if (i < 10 || i >= w - 10 || j < 10 || j >= h - 10) c = new Color(0.15f, 0.15f, 0.15f);
                px[j * w + i] = c;
            }
        KText(px, w, h, big, "검문소 · 통행 금지", 112, w * 0.5f, h * 0.76f, Color.white, 0.5f);
        KText(px, w, h, small, "민방위 통제 구역 · 허가 차량 외 진입 금지 · 삼일동", 40, w * 0.5f, h * 0.26f, new Color(0.15f, 0.15f, 0.15f), 0.5f);
        var r = new System.Random(5203);
        KWear(px, w, h, r, 0.5f);
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // ---------- 충격 구덩이 ----------

    // 흙이 밀려 올라온 둔덕 고리(회전체, 메시 콜라이더) + 가운데 그을음·재·부서진 아스팔트 조각·철근. 길 위에 둔다
    static void KCrater(Transform parent, Vector3 pos, float radius, System.Random r)
    {
        var g = Group("Crater", parent, pos, KF(r, 0, 360));
        var prof = new List<Vector2>();
        int steps = 10;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;               // 0 = 안쪽 가장자리, 1 = 바깥
            float rr = Mathf.Lerp(radius * 0.35f, radius, t);
            float hgt = Mathf.Sin(t * Mathf.PI) * 0.55f * (1f - 0.35f * t) + 0.02f;
            prof.Add(new Vector2(rr, hgt));
        }
        prof.Add(new Vector2(radius + 0.4f, 0.004f));
        var mesh = KBowlMesh("crater_" + Mathf.RoundToInt(radius * 10f), prof.ToArray(), 28, 1.5f);
        var mound = new GameObject("Mound");
        mound.transform.SetParent(g, false);
        mound.AddComponent<MeshFilter>().sharedMesh = mesh;
        mound.AddComponent<MeshRenderer>().sharedMaterial = M["earth"];
        var mc = mound.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        GameObjectUtility.SetStaticEditorFlags(mound, StaticEditorFlags.BatchingStatic); // 내비메시는 NavMeshSurface가 콜라이더로 모은다 (NavigationStatic은 폐기됨)
        Stain("Scorch", g, new Vector3(0, 0.012f, 0), radius * 0.6f, radius * 0.55f, M["scorch"], 5300 + r.Next(40));
        Stain("Ash", g, new Vector3(KF(r, -0.4f, 0.4f), 0.02f, KF(r, -0.4f, 0.4f)), radius * 0.3f, radius * 0.28f, M["ashPile"], 5350 + r.Next(40));
        // 둔덕 위 부서진 아스팔트 조각·철근
        for (int i = 0, n = 8 + r.Next(6); i < n; i++)
        {
            float a = KF(r, 0, Mathf.PI * 2f), d = KF(r, radius * 0.5f, radius * 1.1f);
            var p = new Vector3(Mathf.Cos(a) * d, 0.18f, Mathf.Sin(a) * d);
            Box("Chunk", g, p, new Vector3(KF(r, 0.3f, 0.8f), KF(r, 0.1f, 0.25f), KF(r, 0.25f, 0.6f)), r.NextDouble() < 0.5 ? M["asphalt"] : M["cracked"], 1f, false, new Vector3(KF(r, -30f, 30f), KF(r, 0, 360), KF(r, -20f, 20f)));
        }
        for (int i = 0; i < 3; i++)
        {
            float a = KF(r, 0, Mathf.PI * 2f), d = KF(r, radius * 0.5f, radius * 0.9f);
            Rebar(g, new Vector3(Mathf.Cos(a) * d, 0.25f, Mathf.Sin(a) * d), new Vector3(KF(r, -0.6f, 0.6f), 1f, KF(r, -0.6f, 0.6f)), KF(r, 0.4f, 0.9f), r);
        }
        Smoke(g, new Vector3(KF(r, -0.5f, 0.5f), 0.1f, KF(r, -0.5f, 0.5f)));
    }

    // 회전체 둔덕 (위를 보는 면): profile = (반지름, 높이), 안쪽에서 바깥으로. UV는 m 단위
    static Mesh KBowlMesh(string key, Vector2[] profile, int segments, float uvTile)
    {
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        int rows = profile.Length;
        for (int rIdx = 0; rIdx < rows; rIdx++)
        {
            var p0 = profile[Mathf.Max(0, rIdx - 1)];
            var p1 = profile[Mathf.Min(rows - 1, rIdx + 1)];
            var tangent = new Vector2(p1.x - p0.x, p1.y - p0.y).normalized;
            var n2 = new Vector2(-tangent.y, tangent.x); // 위쪽을 보는 법선
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                var v = new Vector3(cos * profile[rIdx].x, profile[rIdx].y, sin * profile[rIdx].x);
                verts.Add(v);
                norms.Add(new Vector3(cos * n2.x, n2.y, sin * n2.x).normalized);
                uvs.Add(new Vector2(v.x / uvTile, v.z / uvTile));
            }
        }
        for (int rIdx = 0; rIdx < rows - 1; rIdx++)
            for (int s = 0; s < segments; s++)
            {
                int a = rIdx * (segments + 1) + s, b = a + segments + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
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

    // ---------- 연석의 재, 안내문 ----------

    // 블록 연석 바깥쪽에 바람에 쌓인 재 더미 (납작한 타원)
    static void KAshDrifts(Transform b, System.Random r)
    {
        for (int i = 0, n = 3 + r.Next(3); i < n; i++)
        {
            int side = r.Next(4);
            var p = KSidePoint(side, KF(r, -13f, 13f), KBlockHalf + 0.45f);
            Prim(PrimitiveType.Sphere, "AshDrift", b, p + new Vector3(0, 0.03f, 0), side < 2 ? new Vector3(KF(r, 0.9f, 1.8f), KF(r, 0.1f, 0.2f), KF(r, 0.35f, 0.6f)) : new Vector3(KF(r, 0.35f, 0.6f), KF(r, 0.1f, 0.2f), KF(r, 0.9f, 1.8f)), M["ashPile"]);
        }
    }

    // 안내문 종이: 기둥·벽에 붙인다 (pos = 종이 가운데, normal = 종이가 보는 방향). 바람에 들린 귀퉁이
    static void KNotice(Transform parent, Vector3 pos, Vector3 normal, System.Random r)
    {
        var g = Group("Notice", parent, pos);
        g.localRotation = Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(0, 0, KF(r, -8f, 8f));
        var q = Prim(PrimitiveType.Quad, "Paper", g, Vector3.zero, new Vector3(0.3f, 0.45f, 1f), M["notice"]);
        q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var back = Prim(PrimitiveType.Quad, "PaperBack", g, new Vector3(0, 0, 0.002f), new Vector3(0.3f, 0.45f, 1f), M["notice"], new Vector3(0, 180f, 0));
        back.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // 전봇대 눈높이에 안내문 (기둥 둘레의 길 쪽)
    static void KPoleNotices(Transform street, System.Random r)
    {
        foreach (var t in street.GetComponentsInChildren<Transform>())
        {
            if (t.name != "UtilityPole" || t.up.y < 0.9f || r.NextDouble() < 0.45) continue; // 쓰러진 기둥은 뺀다
            float a = KF(r, 0, Mathf.PI * 2f);
            var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); // 기둥 로컬 (축이 로컬 y라 살짝 기운 기둥에서도 표면 바로 앞에 붙는다; 경고 띠 반지름 0.185)
            KNotice(t, new Vector3(n.x * 0.215f, KF(r, 1.45f, 1.75f), n.z * 0.215f), n, r);
        }
    }

    // ---------- 검문소 (북쪽 터널 입구 앞) ----------

    static void KCheckpoint(Transform geo, Vector3 pos, float yaw, System.Random r)
    {
        var g = Group("Checkpoint", geo, pos, yaw);
        // 모래주머니 벽 둘 (두 단, 어긋나게)
        foreach (float x in new[] { -4.2f, 4.2f })
        {
            var wall = Group("SandbagWall", g, new Vector3(x, 0, 0));
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 5; i++)
                {
                    float bx = -1.8f + i * 0.9f + (row % 2) * 0.45f;
                    if (row == 2 && r.NextDouble() < 0.4) continue;
                    KSandbag(wall, new Vector3(bx, 0.17f + row * 0.3f, KF(r, -0.03f, 0.03f)), KF(r, -5f, 5f)); // 둥근 모래주머니 (GameSceneBuilder.KoreaBunker.cs)
                }
        }
        // 철조망: 두 모래주머니 벽 사이에 나선 (둥근 고리들)
        var wire = new List<CombineInstance>();
        for (float x = -2.2f; x <= 2.2f; x += 0.28f)
        {
            float a0 = x * 7f;
            for (int k = 0; k < 10; k++)
            {
                float a = a0 + k * Mathf.PI * 2f / 10f, b = a + Mathf.PI * 2f / 10f;
                var p = new Vector3(x + 0.028f * k, 0.35f + Mathf.Sin(a) * 0.33f, Mathf.Cos(a) * 0.33f);
                var q = new Vector3(x + 0.028f * (k + 1), 0.35f + Mathf.Sin(b) * 0.33f, Mathf.Cos(b) * 0.33f);
                KRod(wire, p, q, 0.006f);
            }
        }
        CombineInto(g, "BarbedWire", wire, M["steelGrey"]);
        ColliderBox("WireCollider", g, new Vector3(0, 0.4f, 0), new Vector3(4.6f, 0.8f, 0.7f), Vector3.zero);
        // 표지판 (기둥 둘 + 판), 쓰러진 안전 고깔, 드럼통
        var sign = Group("CheckSign", g, new Vector3(-5.6f, 0, -0.6f));
        foreach (float sx in new[] { -1.2f, 1.2f }) Box("Post", sign, new Vector3(sx, 1.1f, 0), new Vector3(0.08f, 2.2f, 0.08f), M["steelGrey"], 1f, true);
        Box("Board", sign, new Vector3(0, 1.9f, 0), new Vector3(2.6f, 0.95f, 0.06f), M["tire"], 1f, false);
        var face = Prim(PrimitiveType.Quad, "Face", sign, new Vector3(0, 1.9f, -0.032f), new Vector3(2.5f, 0.9f, 1f), M["checkSign"]);
        face.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Prim(PrimitiveType.Cylinder, "Drum", g, new Vector3(5.9f, 0.45f, -1.2f), new Vector3(0.6f, 0.45f, 0.6f), M["metal"], null, true);
        Prim(PrimitiveType.Cylinder, "DrumTipped", g, new Vector3(6.6f, 0.3f, -0.4f), new Vector3(0.6f, 0.45f, 0.6f), M["paintRed"], new Vector3(90f, KF(r, 0, 90), 0), true);
        var l = PointLight("CheckLight", g, new Vector3(-5.6f, 2.6f, -0.9f), new Color(1f, 0.6f, 0.3f), 2.5f, 7f);
        Flicker(l, 0.4f, 9f, true);
    }

    // ---------- 블록·도시에 적용 ----------

    static void KoreaRuinBlock(Transform b, System.Random r)
    {
        KAshDrifts(b, r);
    }

    // 도시 전체: 구덩이 둘(길 위, 교차로·해치·출발 지점에서 떨어진 곳), 전봇대 안내문, 검문소. 쓰러진 전봇대는 전선보다 먼저 정해야 해서 BuildUtilityPoles(KoreaStreet.cs)가 맡는다
    static void KoreaRuinCity(Transform geo, Transform street)
    {
        var r = KRandom(new Vector3(7f, 0, 7f), 55);
        foreach (var p in new[] { new Vector3(23f, 0, -46f + 2.5f), new Vector3(-46f + 1.5f, 0, 23f) })
            KCrater(geo, p, KF(r, 2.6f, 3.4f), r);
        KPoleNotices(street, r);
        KCheckpoint(geo, new Vector3(20f, 0, 70.5f), 0f, r);
        KScatterPuddles(geo, r); // 길·도랑·인도의 웅덩이
        KBigSmokes(geo, r);      // 폭격 건물 큰 연기
    }

    // ---------- 물웅덩이, 큰 연기 ----------

    // 웅덩이 하나: 검은 젖은 자국 위에 물 얼룩 (PuddleFx가 M_Puddle 재질로 찾는다). onAsphalt면 아래에 젖은 아스팔트 판
    static void KPuddle(Transform parent, Vector3 pos, float rx, float rz, int seed, bool onAsphalt)
    {
        if (onAsphalt) Box("WetPatch", parent, pos + new Vector3(0, 0.003f, 0), new Vector3(rx * 2.5f, 0.005f, rz * 2.3f), M["wetAsphalt"], 2f, false, new Vector3(0, seed * 53 % 360, 0));
        Stain("Puddle", parent, pos + new Vector3(0, 0.012f, 0), rx, rz, M["puddle"], seed); // 차선 페인트(윗면 0.0085)보다 3.5mm 위
    }

    // 도시 전체 웅덩이: 길 가운데(크고 둥근 것·작은 것), 연석 도랑(길쭉한 것), 인도 낮은 자리, 아파트 마당. 교차로·횡단보도(가운데에서 13m) 근처는 피한다
    static void KScatterPuddles(Transform geo, System.Random r)
    {
        var g = Group("Puddles", geo, Vector3.zero);
        int seed = 9000;
        foreach (var rc in RoadCenters)
            foreach (bool alongZ in new[] { true, false })
            {
                float sMax = alongZ ? 74f : KCityEastEdge - 6f;
                for (int i = 0, n = 5 + r.Next(3); i < n; i++)
                {
                    float s = KF(r, -74f, sMax);
                    bool near = false;
                    foreach (var o in RoadCenters) if (Mathf.Abs(s - o) < 13f) near = true;
                    if (near) continue;
                    float d = KF(r, -4.8f, 4.8f);
                    var p = alongZ ? new Vector3(rc + d, 0, s) : new Vector3(s, 0, rc + d);
                    float big = r.NextDouble() < 0.35 ? 1.8f : 1f;
                    KPuddle(g, p, KF(r, 0.5f, 1.4f) * big, KF(r, 0.4f, 1.1f) * big, seed++, true);
                }
                // 연석 도랑을 따라 길쭉한 웅덩이
                for (int i = 0, n = 1 + r.Next(2); i < n; i++)
                {
                    float s = KF(r, -70f, sMax - 4f);
                    bool near = false;
                    foreach (var o in RoadCenters) if (Mathf.Abs(s - o) < 14f) near = true;
                    if (near) continue;
                    float d = r.NextDouble() < 0.5 ? -5.45f : 5.45f;
                    var p = alongZ ? new Vector3(rc + d, 0, s) : new Vector3(s, 0, rc + d);
                    var go = Stain("Puddle", g, p + new Vector3(0, 0.012f, 0), alongZ ? 0.4f : KF(r, 2f, 4.5f), alongZ ? KF(r, 2f, 4.5f) : 0.4f, M["puddle"], seed++, new Vector3(0, 0, 0));
                    Box("WetPatch", g, p + new Vector3(0, 0.003f, 0), alongZ ? new Vector3(1.0f, 0.005f, KF(r, 5f, 10f)) : new Vector3(KF(r, 5f, 10f), 0.005f, 1.0f), M["wetAsphalt"], 2f, false);
                }
            }
        // 인도 낮은 자리 (보도블록 위, 젖은 판 없이)
        foreach (var bx in new[] { -46f, 0f, 46f, KEastBlockX })
            foreach (var bz in new[] { -46f, 0f, 46f })
                for (int i = 0, n = 1 + r.Next(2); i < n; i++)
                {
                    int side = r.Next(4);
                    var p = new Vector3(bx, 0, bz) + KSidePoint(side, KF(r, -12f, 12f), KLotHalf + KF(r, 0.8f, 2.6f));
                    Stain("Puddle", g, p + new Vector3(0, KSideY + 0.004f, 0), KF(r, 0.35f, 0.9f), KF(r, 0.3f, 0.7f), M["puddle"], seed++);
                }
    }

    // 큰 연기 기둥 (폭격 건물·파손 아파트 위): 느리게 솟아 바람에 밀리며 커진다
    static void BigSmoke(Transform parent, Vector3 pos, float scale)
    {
        var psGo = new GameObject("BigSmoke");
        psGo.transform.SetParent(parent, false);
        psGo.transform.localPosition = pos;
        psGo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f * scale, 1.3f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f * scale, 3.2f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.22f, 0.21f, 0.2f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 180;
        main.prewarm = true;
        main.loop = true;
        var emission = ps.emission;
        emission.rateOverTime = 11f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8;
        shape.radius = 0.9f * scale;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        vel.y = new ParticleSystem.MinMaxCurve(0.5f * scale, 0.9f * scale);
        vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.15f);
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(new Color(0.3f, 0.26f, 0.22f), 0), new GradientColorKey(new Color(0.2f, 0.2f, 0.21f), 0.4f), new GradientColorKey(new Color(0.16f, 0.16f, 0.17f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0.35f, 0.6f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 3.2f));
        psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["ash"];
        // 밑불: 주황 깜빡임
        var l = PointLight("EmberLight", psGo.transform, Vector3.zero, new Color(1f, 0.45f, 0.15f), 3f * scale, 9f * scale);
        Flicker(l, 0.5f, 7f, true);
    }

    // 폭격 건물 중 둘 걸러 하나에 큰 연기 (지붕 위 무너진 모퉁이 근처)
    static void KBigSmokes(Transform geo, System.Random r)
    {
        int k = 0;
        foreach (var t in geo.GetComponentsInChildren<Transform>())
        {
            if (t.name != "Bombed") continue;
            if (k++ % 2 == 1) continue;
            Bounds b = new Bounds(t.position, Vector3.zero);
            bool first = true;
            foreach (var mr in t.GetComponentsInChildren<MeshRenderer>()) { if (first) { b = mr.bounds; first = false; } else b.Encapsulate(mr.bounds); }
            if (first) continue;
            var top = new Vector3(b.center.x + KF(r, -2f, 2f), b.max.y - 0.5f, b.center.z + KF(r, -2f, 2f));
            BigSmoke(geo, geo.InverseTransformPoint(top), KF(r, 0.9f, 1.3f));
        }
    }
}
