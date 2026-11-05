using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// GameSceneBuilder의 방공호 벽걸이: 3시 31분에 멈춘 낡은 벽시계(오크 틀·놋쇠 테·바랜 종이 글자판 '31시계점'·검은 바늘·붉은 초침·유리)와
// '삼일은행' 2026년 3월 달력. 그림은 Korea.cs의 KText(DoHyeon·Pretendard)로 그려 Assets/Generated/Shelter/에 저장한다.
// 시계는 WallClock(상호작용·초침)이 붙는다. 자리: 방독면이 걸려 있던 서쪽 벽 (BuildShelterScene이 부른다)
public static partial class GameSceneBuilder
{
    const string ShelterGenDir = "Assets/Generated/Shelter";
    const float ClockDialR = 0.19f;   // 글자판 반지름 (m). 틀 바깥 반지름 0.265
    const float ClockHour = 105.5f, ClockMinute = 186f; // 3시 31분 (12시 = 0, 시계 방향 +)

    // 벽시계: pos = 글자판 가운데 (벽 면), yaw = 로컬 +z가 벽에서 방 쪽으로 나오게 (서쪽 벽이면 90)
    static void BuildWallClock(Transform parent, Vector3 pos, float yaw)
    {
        var g = Group("WallClock", parent, pos, yaw);
        var wood = TexMat("M_ClockWood", "wood_planks_dirt", new Color(0.46f, 0.3f, 0.17f), 0.4f);
        var brass = TexMat("M_ClockBrass", null, new Color(0.62f, 0.47f, 0.22f), 0.72f, 0.85f);
        var hand = TexMat("M_ClockHand", null, new Color(0.05f, 0.045f, 0.04f), 0.5f, 0.55f);
        var second = TexMat("M_ClockSecond", null, new Color(0.72f, 0.07f, 0.05f), 0.45f, 0.25f);
        var glass = TransparentMat("M_ClockGlass", new Color(0.82f, 0.86f, 0.9f, 0.035f), 0.45f); // 광택을 낮춰 전구 반사로 글자판이 하얗게 날아가지 않게
        var dialTex = ShelterTexture("clock_dial", ClockDialPixels(1024), 1024, 1024, false);
        var dial = KLitMat("M_ClockDial", dialTex, null, Color.white, 0.12f, 0f);

        // 오크 틀 (둥근 몰딩, 벽에서 8.8cm): 단면은 안쪽 턱 아래에서 시작해 위→앞면→바깥→뒤로 돈다 (겉면이 밖을 보는 순서). 축을 로컬 +z로 돌려 세운다
        var frame = new GameObject("Frame");
        frame.transform.SetParent(g, false);
        frame.transform.localRotation = Quaternion.Euler(90, 0, 0);
        frame.AddComponent<MeshFilter>().sharedMesh = ClockLathe("clock_frame", new[]
        {
            new Vector2(0.194f, 0.04f), new Vector2(0.196f, 0.062f), new Vector2(0.208f, 0.082f), new Vector2(0.232f, 0.088f),
            new Vector2(0.255f, 0.072f), new Vector2(0.265f, 0.045f), new Vector2(0.262f, 0.018f), new Vector2(0.248f, 0.0f),
        }, 48);
        frame.AddComponent<MeshRenderer>().sharedMaterial = wood;
        // 놋쇠 테 (글자판 가장자리를 누르는 얇은 고리): 안쪽 아래 → 안쪽 위 → 바깥 위 → 바깥 아래 → 안쪽 아래
        var bezel = new GameObject("Bezel");
        bezel.transform.SetParent(g, false);
        bezel.transform.localRotation = Quaternion.Euler(90, 0, 0);
        bezel.AddComponent<MeshFilter>().sharedMesh = ClockLathe("clock_bezel", new[]
        {
            new Vector2(0.184f, 0.044f), new Vector2(0.186f, 0.053f), new Vector2(0.207f, 0.053f), new Vector2(0.205f, 0.044f), new Vector2(0.184f, 0.044f),
        }, 48);
        bezel.AddComponent<MeshRenderer>().sharedMaterial = brass;
        // 글자판: 앞(+z)을 보는 원판 (네모 쿼드는 모서리가 틀 밖으로 비쳤다)
        var dialGo = new GameObject("Dial");
        dialGo.transform.SetParent(g, false);
        dialGo.transform.localPosition = new Vector3(0, 0, 0.046f);
        dialGo.AddComponent<MeshFilter>().sharedMesh = FrontDisc("clock_dial_disc", 0.181f, 64);
        var dialMr = dialGo.AddComponent<MeshRenderer>();
        dialMr.sharedMaterial = dial;
        dialMr.shadowCastingMode = ShadowCastingMode.Off;
        // 바늘: 피벗을 시계 방향 각도만큼 돌린다 (로컬 z 둘레 +). 시침·분침은 멈춘 채, 초침만 WallClock이 돌린다
        var hour = Group("HourHand", g, new Vector3(0, 0, 0.056f));
        hour.localRotation = Quaternion.Euler(0, 0, ClockHour);
        Box("Blade", hour, new Vector3(0, 0.048f, 0), new Vector3(0.013f, 0.096f, 0.004f), hand, 1f, false, null, false);
        Box("Spade", hour, new Vector3(0, 0.095f, 0), new Vector3(0.03f, 0.03f, 0.004f), hand, 1f, false, new Vector3(0, 0, 45f), false);
        Box("Tip", hour, new Vector3(0, 0.122f, 0), new Vector3(0.009f, 0.028f, 0.004f), hand, 1f, false, null, false);
        var minute = Group("MinuteHand", g, new Vector3(0, 0, 0.06f));
        minute.localRotation = Quaternion.Euler(0, 0, ClockMinute);
        Box("Blade", minute, new Vector3(0, 0.07f, 0), new Vector3(0.011f, 0.14f, 0.004f), hand, 1f, false, null, false);
        Box("Spade", minute, new Vector3(0, 0.138f, 0), new Vector3(0.024f, 0.024f, 0.004f), hand, 1f, false, new Vector3(0, 0, 45f), false);
        Box("Tip", minute, new Vector3(0, 0.162f, 0), new Vector3(0.007f, 0.03f, 0.004f), hand, 1f, false, null, false);
        var sec = Group("SecondHand", g, new Vector3(0, 0, 0.064f));
        Box("Needle", sec, new Vector3(0, 0.06f, 0), new Vector3(0.0035f, 0.19f, 0.003f), second, 1f, false, null, false);
        Prim(PrimitiveType.Cylinder, "Weight", sec, new Vector3(0, -0.03f, 0), new Vector3(0.018f, 0.0015f, 0.018f), second, new Vector3(90, 0, 0));
        Prim(PrimitiveType.Cylinder, "Cap", g, new Vector3(0, 0, 0.066f), new Vector3(0.02f, 0.002f, 0.02f), brass, new Vector3(90, 0, 0));
        // 유리 (얇은 원판, 그림자 없음)
        var gl = Prim(PrimitiveType.Cylinder, "Glass", g, new Vector3(0, 0, 0.076f), new Vector3(0.41f, 0.0008f, 0.41f), glass, new Vector3(90, 0, 0));
        gl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        foreach (var t in new[] { hour, minute, sec })
            foreach (var mr in t.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.Off;

        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0, 0.045f);
        col.size = new Vector3(0.54f, 0.54f, 0.09f);
        var clock = g.gameObject.AddComponent<WallClock>();
        clock.secondHand = sec;
    }

    // 달력: pos = 종이 가운데 (벽 면), 0.32×0.48m. 위에 쇠 클립과 못
    static void BuildCalendar(Transform parent, Vector3 pos, float yaw)
    {
        var g = Group("Calendar", parent, pos, yaw);
        var tex = ShelterTexture("calendar", CalendarPixels(640, 960), 640, 960, false);
        var paper = KLitMat("M_Calendar", tex, null, Color.white, 0.12f, 0f);
        var sheet = Prim(PrimitiveType.Quad, "Sheet", g, new Vector3(0, 0, 0.007f), new Vector3(0.32f, 0.48f, 1f), paper, new Vector3(0, 180f, 0));
        sheet.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        // 종이 뒤의 얇은 받침 (종이가 벽에서 조금 떠 보인다), 위 클립과 못
        Box("Backing", g, new Vector3(0, 0, 0.003f), new Vector3(0.322f, 0.482f, 0.005f), M["cloth"], 1f, false, null, true);
        Box("Clip", g, new Vector3(0, 0.233f, 0.01f), new Vector3(0.335f, 0.022f, 0.012f), M["steel"], 1f, false, null, true);
        Prim(PrimitiveType.Cylinder, "Nail", g, new Vector3(0, 0.233f, 0.018f), new Vector3(0.012f, 0.004f, 0.012f), M["steel"], new Vector3(90, 0, 0));
        var col = g.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0, 0.008f);
        col.size = new Vector3(0.34f, 0.5f, 0.02f);
    }

    // ---------- 메시 ----------

    // 회전체 (축 = 로컬 +y, profile = (반지름, 높이) 차례). 유니티는 앞면을 시계 방향으로 감으므로, 단면을 '안쪽 아래 → 위 → 바깥 → 아래'로 돌면 겉면이 밖을 본다.
    // 법선은 접선을 왼쪽으로 90° 돌린 것 (보이는 쪽). Korea.cs의 KLathe는 반대 방향으로 감겨 안쪽만 보인다
    static Mesh ClockLathe(string key, Vector2[] profile, int segments)
    {
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var verts = new System.Collections.Generic.List<Vector3>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        int rows = profile.Length;
        for (int r = 0; r < rows; r++)
        {
            var p0 = profile[Mathf.Max(0, r - 1)];
            var p1 = profile[Mathf.Min(rows - 1, r + 1)];
            var t = (p1 - p0).normalized;
            var n2 = new Vector2(-t.y, t.x);
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                verts.Add(new Vector3(cos * profile[r].x, profile[r].y, sin * profile[r].x));
                norms.Add(new Vector3(cos * n2.x, n2.y, sin * n2.x).normalized);
                uvs.Add(new Vector2(s / (float)segments * 4f, r / (float)Mathf.Max(1, rows - 1)));
            }
        }
        for (int r = 0; r < rows - 1; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s, b = a + segments + 1;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
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

    // 앞(+z)을 보는 원판 (글자판). 보는 사람의 오른쪽이 로컬 -x이므로 u는 -x로 커진다 (글자가 거울상이 되지 않게)
    static Mesh FrontDisc(string key, float radius, int segments)
    {
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var verts = new System.Collections.Generic.List<Vector3> { Vector3.zero };
        var uvs = new System.Collections.Generic.List<Vector2> { new Vector2(0.5f, 0.5f) };
        var tris = new System.Collections.Generic.List<int>();
        for (int s = 0; s <= segments; s++)
        {
            float a = s / (float)segments * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0));
            uvs.Add(new Vector2(0.5f - Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int s = 0; s < segments; s++) { tris.Add(0); tris.Add(s + 1); tris.Add(s + 2); }
        // 앞면이 +z를 보게 (왼손 좌표계: 시계 방향 삼각형의 Cross(b-a, c-a)가 앞면 법선)
        if (Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]).z < 0)
            for (int i = 0; i < tris.Count; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
        var norms = new Vector3[verts.Count];
        for (int i = 0; i < norms.Length; i++) norms[i] = Vector3.forward;
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

    // ---------- 그림 ----------

    // 글자판 1024×1024: 바랜 크림색 종이(가장자리로 갈수록 누렇고 얼룩), 가는 바깥 원, 시 눈금 네모·분 눈금 금, 1~12 숫자,
    // 가운데 위 작은 상표 원, 가운데 아래 '31시계점', 태엽 구멍 둘, 가운데 축 구멍. 원 밖은 어둡게 (틀에 가려진다)
    static Color32[] ClockDialPixels(int n)
    {
        var px = new Color[n * n];
        float c = n / 2f;
        var ink = new Color(0.12f, 0.09f, 0.07f, 1f);
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float dx = (i + 0.5f - c) / c, dy = (j + 0.5f - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                var paper = Color.Lerp(new Color(0.9f, 0.82f, 0.64f), new Color(0.7f, 0.57f, 0.37f), Mathf.SmoothStep(0.25f, 1.05f, d));
                float fox = Mathf.SmoothStep(0.57f, 0.76f, Mathf.PerlinNoise(i * 0.011f + 3.1f, j * 0.011f + 7.7f)) * 0.5f;
                paper = Color.Lerp(paper, new Color(0.52f, 0.37f, 0.22f), fox);
                float grain = (Mathf.PerlinNoise(i * 0.23f, j * 0.23f) - 0.5f) * 0.06f;
                paper = new Color(paper.r + grain, paper.g + grain, paper.b + grain, 1f);
                px[j * n + i] = d <= 1f ? paper : new Color(0.07f, 0.055f, 0.045f, 1f);
            }
        DialRing(px, n, c, c, 0.935f * c, 0.943f * c, ink);
        for (int m = 0; m < 60; m++)
        {
            float a = m / 60f * Mathf.PI * 2f; // 12시 = 0, 시계 방향
            float sx = Mathf.Sin(a), sy = Mathf.Cos(a);
            if (m % 5 == 0) DialLine(px, n, c + sx * 0.87f * c, c + sy * 0.87f * c, c + sx * 0.915f * c, c + sy * 0.915f * c, 0.024f * c, ink);
            else DialLine(px, n, c + sx * 0.895f * c, c + sy * 0.895f * c, c + sx * 0.925f * c, c + sy * 0.925f * c, 0.006f * c, ink);
        }
        var num = KFontFor("Fonts/Pretendard-Bold", "0123456789");
        var brand = KFontFor("Fonts/DoHyeon-Regular", "31시계점");
        for (int h = 1; h <= 12; h++)
        {
            float a = h / 12f * Mathf.PI * 2f;
            float x = c + Mathf.Sin(a) * 0.745f * c, y = c + Mathf.Cos(a) * 0.745f * c;
            KText(px, n, n, num, h.ToString(), 118, x, y, ink, 0.5f);
        }
        // 상표 원 (가운데 위)과 '31', 상호 (가운데 아래), 태엽 구멍 둘, 축 구멍
        DialRing(px, n, c, c + 0.31f * c, 0.062f * c, 0.07f * c, ink);
        KText(px, n, n, num, "31", 40, c, c + 0.31f * c, ink, 0.5f);
        KText(px, n, n, brand, "31시계점", 46, c, c - 0.3f * c, new Color(0.16f, 0.12f, 0.09f, 1f), 0.5f);
        foreach (float sx in new[] { -1f, 1f })
        {
            DialDot(px, n, c + sx * 0.27f * c, c - 0.1f * c, 0.03f * c, new Color(0.08f, 0.06f, 0.05f, 1f));
            DialRing(px, n, c + sx * 0.27f * c, c - 0.1f * c, 0.03f * c, 0.042f * c, new Color(0.6f, 0.48f, 0.26f, 1f));
        }
        DialDot(px, n, c, c, 0.022f * c, new Color(0.1f, 0.08f, 0.06f, 1f));
        KReleaseFonts();
        var res = new Color32[px.Length];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // 달력 640×960 (세로): 위 양쪽 작은 지난달·다음달, 가운데 큰 초록 '3'과 '2026 MARCH', 요일 띠(일 빨강·토 파랑), 날짜 다섯 줄(음력 작은 글씨, 삼일절),
    // 아래 메모 칸, 맨 아래 '삼일은행' (파란 네모 '31' 마크)
    static Color32[] CalendarPixels(int w, int h)
    {
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float grain = (Mathf.PerlinNoise(i * 0.19f, j * 0.19f) - 0.5f) * 0.025f;
                float edge = Mathf.Clamp01(Mathf.Min(i, w - 1 - i, j, h - 1 - j) / 14f);
                var p = new Color(0.955f + grain, 0.945f + grain, 0.905f + grain, 1f);
                px[j * w + i] = Color.Lerp(new Color(0.86f, 0.85f, 0.8f, 1f), p, edge);
            }
        var red = new Color(0.82f, 0.16f, 0.14f, 1f);
        var blue = new Color(0.16f, 0.4f, 0.78f, 1f);
        var green = new Color(0.14f, 0.56f, 0.3f, 1f);
        var black = new Color(0.12f, 0.12f, 0.13f, 1f);
        var gray = new Color(0.45f, 0.45f, 0.47f, 1f);
        var line = new Color(0.72f, 0.72f, 0.7f, 1f);
        var bank = new Color(0.1f, 0.3f, 0.64f, 1f);
        var num = KFontFor("Fonts/Pretendard-Bold", "0123456789");
        var small = KFontFor("Fonts/Pretendard-SemiBold", "0123456789.");
        var hangul = KFontFor("Fonts/DoHyeon-Regular", "일월화수목금토삼은행절정대보름");
        var latin = KFontFor("Fonts/Pretendard-Regular", "SUNMOTEWDHFRIA 20263");
        // 위 양쪽: 2월(1일 일요일, 28일)·4월(1일 수요일, 30일) 작은 달력
        MiniMonth(px, w, h, 44f, 946f, "2", 1, 0, 28, num, small, hangul, red, blue, black, gray, green);
        MiniMonth(px, w, h, 428f, 946f, "4", 1, 3, 30, num, small, hangul, red, blue, black, gray, green);
        // 가운데 큰 달 숫자와 영문
        KText(px, w, h, num, "3", 250, 320f, 858f, green, 0.5f);
        KText(px, w, h, latin, "2026  MARCH", 24, 320f, 748f, gray, 0.5f);
        // 요일 띠 (7칸, 폭 84): 일요일 빨강, 토요일 파랑, 평일 진회색 바탕에 흰 글자
        string[] days = { "일", "월", "화", "수", "목", "금", "토" };
        string[] eng = { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };
        for (int d = 0; d < 7; d++)
        {
            int x0 = 26 + d * 84;
            var bg = d == 0 ? red : d == 6 ? blue : new Color(0.33f, 0.33f, 0.36f, 1f);
            CalFill(px, w, h, x0 + 1, 692, x0 + 83, 736, bg);
            KText(px, w, h, hangul, days[d], 30, x0 + 42f, 720f, Color.white, 0.5f);
            KText(px, w, h, latin, eng[d], 12, x0 + 42f, 701f, new Color(1f, 1f, 1f, 0.85f), 0.5f);
        }
        // 날짜 다섯 줄 (3월 1일 일요일, 31일까지), 칸 선, 음력(1.13~1.30, 2.1~2.13), 1일 '삼일절', 3일 '정월대보름'
        for (int row = 0; row <= 5; row++) CalFill(px, w, h, 26, 690 - row * 98, 614, 691 - row * 98, line);
        for (int d = 0; d <= 7; d++) CalFill(px, w, h, 26 + d * 84, 200, 27 + d * 84, 691, line);
        int lunarM = 1, lunarD = 13;
        for (int day = 1; day <= 31; day++)
        {
            int idx = day - 1, row = idx / 7, col = idx % 7;
            float cx = 26 + col * 84 + 42f, cy = 690 - row * 98 - 40f;
            var colr = col == 0 ? red : col == 6 ? blue : black;
            KText(px, w, h, num, day.ToString(), 54, cx, cy, colr, 0.5f);
            KText(px, w, h, small, lunarM + "." + lunarD, 15, cx, cy - 36f, gray, 0.5f);
            if (day == 1) KText(px, w, h, hangul, "삼일절", 15, cx, cy + 34f, red, 0.5f);
            if (day == 3) KText(px, w, h, hangul, "정월대보름", 14, cx, cy + 34f, gray, 0.5f);
            if (lunarM == 1 && lunarD == 30) { lunarM = 2; lunarD = 1; } else lunarD++;
        }
        // 메모 칸 (아래): 가는 줄 넷
        for (int k = 0; k <= 4; k++) CalFill(px, w, h, 26, 108 + k * 22, 614, 109 + k * 22, line);
        // 은행: 파란 네모 마크 '31' + 상호
        CalFill(px, w, h, 206, 36, 254, 84, bank);
        KText(px, w, h, num, "31", 30, 230f, 60f, Color.white, 0.5f);
        KText(px, w, h, hangul, "삼일은행", 50, 268f, 60f, bank, 0f);
        // 위 가운데 걸이 구멍
        DialDot(px, w, 320f, 940f, 7f, new Color(0.2f, 0.18f, 0.16f, 1f));
        KReleaseFonts();
        var res = new Color32[px.Length];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    // 작은 달력 한 달: x0·yTop 왼쪽 위, 7칸(폭 24)·줄 간격 17. firstCol: 1일의 요일 칸(0 일요일)
    static void MiniMonth(Color[] px, int w, int h, float x0, float yTop, string month, int firstDay, int firstCol, int days, KFont num, KFont small, KFont hangul, Color red, Color blue, Color black, Color gray, Color green)
    {
        KText(px, w, h, num, month, 28, x0, yTop - 10f, green, 0f);
        string[] dn = { "일", "월", "화", "수", "목", "금", "토" };
        for (int d = 0; d < 7; d++) KText(px, w, h, hangul, dn[d], 12, x0 + d * 24f + 12f, yTop - 34f, d == 0 ? red : d == 6 ? blue : gray, 0.5f);
        for (int day = firstDay; day <= days; day++)
        {
            int idx = firstCol + day - 1, row = idx / 7, col = idx % 7;
            KText(px, w, h, small, day.ToString(), 14, x0 + col * 24f + 12f, yTop - 52f - row * 17f, col == 0 ? red : col == 6 ? blue : black, 0.5f);
        }
    }

    static void CalFill(Color[] px, int w, int h, int x0, int y0, int x1, int y1, Color c)
    {
        for (int j = Mathf.Max(0, y0); j < Mathf.Min(h, y1); j++)
            for (int i = Mathf.Max(0, x0); i < Mathf.Min(w, x1); i++)
                px[j * w + i] = c;
    }

    // 채운 원 (가장자리 부드럽게)
    static void DialDot(Color[] px, int w, float cx, float cy, float r, Color c)
    {
        int h = px.Length / w;
        for (int j = Mathf.Max(0, (int)(cy - r - 1)); j <= Mathf.Min(h - 1, (int)(cy + r + 1)); j++)
            for (int i = Mathf.Max(0, (int)(cx - r - 1)); i <= Mathf.Min(w - 1, (int)(cx + r + 1)); i++)
            {
                float d = Mathf.Sqrt((i + 0.5f - cx) * (i + 0.5f - cx) + (j + 0.5f - cy) * (j + 0.5f - cy));
                float a = Mathf.Clamp01(r + 0.5f - d);
                if (a > 0) px[j * w + i] = Color.Lerp(px[j * w + i], c, a * c.a);
            }
    }

    // 고리 (r0~r1)
    static void DialRing(Color[] px, int n, float cx, float cy, float r0, float r1, Color c)
    {
        int h = px.Length / n;
        for (int j = Mathf.Max(0, (int)(cy - r1 - 1)); j <= Mathf.Min(h - 1, (int)(cy + r1 + 1)); j++)
            for (int i = Mathf.Max(0, (int)(cx - r1 - 1)); i <= Mathf.Min(n - 1, (int)(cx + r1 + 1)); i++)
            {
                float d = Mathf.Sqrt((i + 0.5f - cx) * (i + 0.5f - cx) + (j + 0.5f - cy) * (j + 0.5f - cy));
                float a = Mathf.Clamp01(r1 + 0.5f - d) * Mathf.Clamp01(d - r0 + 0.5f);
                if (a > 0) px[j * n + i] = Color.Lerp(px[j * n + i], c, a * c.a);
            }
    }

    // 굵은 선 (양 끝 둥글게)
    static void DialLine(Color[] px, int n, float x0, float y0, float x1, float y1, float width, Color c)
    {
        int h = px.Length / n;
        float r = width / 2f;
        var a = new Vector2(x0, y0);
        var b = new Vector2(x1, y1);
        var ab = b - a;
        float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
        int i0 = Mathf.Max(0, (int)(Mathf.Min(x0, x1) - r - 1)), i1 = Mathf.Min(n - 1, (int)(Mathf.Max(x0, x1) + r + 1));
        int j0 = Mathf.Max(0, (int)(Mathf.Min(y0, y1) - r - 1)), j1 = Mathf.Min(h - 1, (int)(Mathf.Max(y0, y1) + r + 1));
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                var p = new Vector2(i + 0.5f, j + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = Vector2.Distance(p, a + ab * t);
                float al = Mathf.Clamp01(r + 0.5f - d);
                if (al > 0) px[j * n + i] = Color.Lerp(px[j * n + i], c, al * c.a);
            }
    }

    // PNG로 저장해 들여온다 (KSaveTex와 같되 Assets/Generated/Shelter/에, 반복 없음)
    static Texture2D ShelterTexture(string name, Color32[] px, int w, int h, bool repeat)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(ShelterGenDir)) AssetDatabase.CreateFolder("Assets/Generated", "Shelter");
        string path = $"{ShelterGenDir}/{name}.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.mipmapEnabled = true;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 8;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
