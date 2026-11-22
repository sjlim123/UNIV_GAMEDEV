using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 가로등 품질 4단계 (2026-11-23, 사용자 "트럭 1만·가로등 직접"). Tripo 그림 → 3D가 8 m짜리 가는 기둥을 두 번 망가뜨려(11-22 팔 없음, 11-23 기둥 끊김)
// 모양은 직접 짠다. 치수는 게임 코드 가로등(GameSceneBuilder.City.cs LampPost)과 같다: 받침 플랜지 r 0.22, 아래 기둥 r 0.09 (0~5.2 m),
// 위 기둥 r 0.07 (~7.95 m), 굽은 팔 (0,7.95,0)→(0,8.25,0.5)→(0,8.4,1.1)→(0,8.42,1.6), 등 머리 0.34×0.1×0.62 (가운데 (0,8.38,1.65)), 제어함 0.22×0.5×0.16.
// 앞(+Z) = 팔이 뻗는 쪽, 위 = +Y, 원점 = 기둥 밑 가운데.
// 칠: Tripo 그림 편집(작업 bbaa2387, 우리 코드 가로등을 옆에서 찍은 사진을 '녹슨 강철 가로등'으로 바꾼 그림)을 부위별로 비춰 굽는다.
// 그림은 찍은 카메라와 같은 구도라, 3D 점을 그 카메라로 비추면 그림 속 같은 자리가 나온다. 뒤쪽(-x)은 앞쪽으로 접어 비추고,
// 테두리에서 배경색이 묻지 않게 부위 축 쪽으로 55 %만 당겨서 비춘다.
// 결과: lamp_post_01/Q0_낮음..Q3_매우높음/lamp_post_01_q{n}.asset·_diff.png·.prefab (실외기·나무와 같은 규칙, 텍스처 256/512/512/1024 HighRes)
public static partial class GameSceneBuilder
{
    const string LampId = "lamp_post_01";
    static string LampPaintPath => $"{TripoModelDir}/{LampId}/source/{LampId}_paint_src.png";

    // 비추기용: 부위마다 꼭짓점 범위와 읽는 방법
    //  kind 0 = 축(선분 a→b, path가 있으면 그 꺾은선) 쪽으로 당겨 읽기 — 기둥·팔·플랜지·볼트
    //  kind 1 = 상자(제어함): 옆(±x)·위아래 면은 그림에 보이는 +x 면의 같은 자리, 길 쪽(+z) 면은 그 자리 그대로 비춰 읽는다
    //  kind 2 = 등 머리: 그림 속 머리는 옆면이 10 cm뿐이라 분홍빛 녹 한 줄이 늘어나 돌처럼 보였다 → 위 기둥 칠(청록·녹)을 빌려 쓰고 아랫면은 어둡게
    struct LampPart { public int v0, v1, kind; public Vector3 a, b; public Vector3 lo, hi; public List<Vector3> path; }

    static void LampMark(List<LampPart> parts, LpMesh m, int v0, Vector3 a, Vector3 b) => parts.Add(new LampPart { v0 = v0, v1 = m.pos.Count, a = a, b = b });
    static void LampMarkPath(List<LampPart> parts, LpMesh m, int v0, List<Vector3> path) => parts.Add(new LampPart { v0 = v0, v1 = m.pos.Count, a = path[0], b = path[path.Count - 1], path = new List<Vector3>(path) });
    static void LampMarkBox(List<LampPart> parts, LpMesh m, int v0, Vector3 lo, Vector3 hi, int kind = 1) => parts.Add(new LampPart { v0 = v0, v1 = m.pos.Count, kind = kind, lo = lo, hi = hi });

    // HqTorus의 조각 좌표는 (고리 번호, 둘레 번호)라 미터가 아니다: 텍스처 한 장을 나눌 때 다른 부위를 아주 작게 만들어 흐려졌다. 미터로 고친다
    static void LampTorus(LpMesh m, Vector3 c, Vector3 n, float R, float rt, int segs, int sides, int chart)
    {
        int v0 = m.pos.Count;
        HqTorus(m, c, n, R, rt, segs, sides, chart);
        for (int i = v0; i < m.pos.Count; i++) m.flat[i] = new Vector2(m.flat[i].x * 2 * Mathf.PI * R / segs, m.flat[i].y * 2 * Mathf.PI * rt / sides);
    }

    static List<Vector3> LampLine(Vector3 a, Vector3 b, float step)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / step));
        var l = new List<Vector3>(); for (int i = 0; i <= n; i++) l.Add(Vector3.Lerp(a, b, i / (float)n));
        return l;
    }

    static List<float> LampRad(int n, float r0, float r1) { var l = new List<float>(); for (int i = 0; i < n; i++) l.Add(Mathf.Lerp(r0, r1, n > 1 ? i / (float)(n - 1) : 0)); return l; }

    static LpMesh LampBuild(int q, List<LampPart> parts)
    {
        var m = new LpMesh();
        int sPole = new[] { 6, 10, 16, 24 }[q], sArm = new[] { 4, 6, 10, 14 }[q], sFl = new[] { 8, 12, 20, 32 }[q];
        var Y = Vector3.up;
        int v;

        // 받침 플랜지 (+ 높음부터 용접 띠, 중간부터 볼트 4개)
        v = m.pos.Count;
        LpCyl(m, Vector3.zero, new Vector3(0, 0.08f, 0), 0.22f, sFl, false, q >= 1);
        if (q >= 2) LpTube(m, new List<Vector3> { new Vector3(0, 0.08f, 0), new Vector3(0, 0.13f, 0) }, new List<float> { 0.12f, 0.095f }, sPole, false, false);
        LampMark(parts, m, v, Vector3.zero, new Vector3(0, 0.13f, 0));
        if (q >= 1)
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + 90f * i) * Mathf.Deg2Rad; var c = new Vector3(Mathf.Cos(a) * 0.165f, 0.08f, Mathf.Sin(a) * 0.165f);
                v = m.pos.Count;
                LpCyl(m, c, c + Y * 0.035f, 0.018f, 6, false, false);                     // 너트
                if (q >= 2) LpCyl(m, c + Y * 0.035f, c + Y * 0.06f, 0.009f, 6, false, false); // 볼트 끝
                LampMark(parts, m, v, c, c + Y * 0.06f);
            }

        // 아래 기둥 (0.08 ~ 5.2 m, r 0.09)과 위 기둥 (~7.95 m, r 0.07). 낮음은 한 관
        float split = q == 0 ? 2.0f : 1.4f;
        if (q == 0)
        {
            v = m.pos.Count;
            var pts = LampLine(new Vector3(0, 0.08f, 0), new Vector3(0, 7.95f, 0), split);
            var rad = new List<float>(); foreach (var p in pts) rad.Add(p.y < 5.2f ? 0.085f : 0.07f);
            LpTube(m, pts, rad, sPole, false, true, split);
            LampMark(parts, m, v, new Vector3(0, 0.08f, 0), new Vector3(0, 7.95f, 0));
        }
        else
        {
            v = m.pos.Count;
            LpTube(m, LampLine(new Vector3(0, 0.08f, 0), new Vector3(0, 5.2f, 0), split), LampRad(LampLine(new Vector3(0, 0.08f, 0), new Vector3(0, 5.2f, 0), split).Count, 0.09f, 0.09f), sPole, false, false, split);
            // 이음 칼라: 짧은 원뿔 (r 0.09 → 0.07)
            LpTube(m, new List<Vector3> { new Vector3(0, 5.2f, 0), new Vector3(0, 5.26f, 0), new Vector3(0, 5.36f, 0) }, new List<float> { 0.1f, 0.1f, 0.07f }, sPole, false, false);
            var up = LampLine(new Vector3(0, 5.36f, 0), new Vector3(0, 7.95f, 0), split);
            LpTube(m, up, LampRad(up.Count, 0.07f, 0.07f), sPole, false, true, split);
            LampMark(parts, m, v, new Vector3(0, 0.08f, 0), new Vector3(0, 7.95f, 0));
            if (q >= 2)
            {
                // 칼라 아래 턱(링)과 팔을 무는 꼭대기 죔쇠
                v = m.pos.Count;
                LampTorus(m, new Vector3(0, 5.2f, 0), Y, 0.098f, 0.012f, sPole, q == 3 ? 8 : 6, m.NewChart());
                LpTube(m, new List<Vector3> { new Vector3(0, 7.72f, 0), new Vector3(0, 7.74f, 0), new Vector3(0, 7.9f, 0), new Vector3(0, 7.92f, 0) }, new List<float> { 0.07f, 0.085f, 0.085f, 0.07f }, sPole, false, false);
                LampMark(parts, m, v, new Vector3(0, 5.2f, 0), new Vector3(0, 7.92f, 0));
            }
        }

        // 제어함 (기둥 앞쪽, 가운데 (0, 1.1, 0.12)), 높음부터 둥근 모서리 + 기둥을 감는 띠 둘
        v = m.pos.Count;
        var blo = new Vector3(-0.11f, 0.85f, 0.04f); var bhi = new Vector3(0.11f, 1.35f, 0.2f);
        if (q >= 2) HqRoundBox(m, blo, bhi, 0.012f, new[] { 4 }, null);
        else m.Box(blo, bhi, 4);
        LampMarkBox(parts, m, v, blo, bhi);
        if (q >= 2)
        {
            v = m.pos.Count;
            foreach (float yy in new[] { 0.93f, 1.27f }) LampTorus(m, new Vector3(0, yy, 0), Y, 0.094f, 0.006f, sPole, 4, m.NewChart());
            if (q == 3) LpCyl(m, new Vector3(0, 0.85f, 0.15f), new Vector3(0, 0.79f, 0.15f), 0.012f, 8, false, false); // 아래로 나온 전선 구멍
            LampMark(parts, m, v, new Vector3(0, 0.79f, 0), new Vector3(0, 1.35f, 0));
        }

        // 굽은 팔 (낮음은 꺾인 세 마디, 그 위는 부드러운 곡선)
        v = m.pos.Count;
        var arm = new List<Vector3> { new Vector3(0, 7.95f, 0), new Vector3(0, 8.25f, 0.5f), new Vector3(0, 8.4f, 1.1f), new Vector3(0, 8.42f, 1.5f) };
        var armR = new List<float> { 0.05f, 0.045f, 0.04f, 0.035f };
        if (q >= 1)
        {
            arm.Insert(0, new Vector3(0, 7.7f, 0)); armR.Insert(0, 0.055f);
            LpSmoothPath(arm, armR, new[] { 1, 2, 4, 6 }[q], out var ap, out var ar);
            arm = ap; armR = ar;
        }
        LpTube(m, arm, armR, sArm, false, false, 0.8f);
        LampMarkPath(parts, m, v, arm); // 굽은 팔의 실제 꺾은선 (곧은 선으로 읽으면 굽은 곳 아랫면이 그림 배경을 읽어 흰 줄이 났다)

        // 등 머리 (0.34 × 0.1 × 0.62), 높음부터 둥근 모서리 + 매우 높음은 윗면 방열 날개 5장
        v = m.pos.Count;
        var hlo = new Vector3(-0.17f, 8.33f, 1.34f); var hhi = new Vector3(0.17f, 8.43f, 1.96f);
        if (q >= 2) HqRoundBox(m, hlo, hhi, q == 3 ? 0.025f : 0.018f, null, null);
        else m.Box(hlo, hhi);
        if (q == 3)
            for (int i = 0; i < 5; i++)
            {
                float x = -0.1f + 0.05f * i;
                m.Box(new Vector3(x - 0.004f, 8.43f, 1.45f), new Vector3(x + 0.004f, 8.455f, 1.86f), 2);
            }
        LampMarkBox(parts, m, v, hlo, new Vector3(hhi.x, q == 3 ? 8.455f : hhi.y, hhi.z), 2);
        return m;
    }

    // 그림을 찍은 카메라 (참고 그림 찍기 도구 ref.cs.txt와 같은 계산: 코드 가로등 상자 가운데 (0, 4.225, 0.87), 방향 (1, 0.12, 0.3), 화각 25°, 정사각형)
    static Vector2 LampProject(Vector3 p)
    {
        var c = new Vector3(0, 4.225f, 0.87f); float R = new Vector3(0.22f, 4.225f, 1.09f).magnitude;
        var d = new Vector3(1f, 0.12f, 0.3f).normalized;
        var cam = c + d * (R / Mathf.Sin(12.5f * Mathf.Deg2Rad));
        var fwd = (c - cam).normalized; var right = Vector3.Cross(Vector3.up, fwd).normalized; var up = Vector3.Cross(fwd, right);
        float f = 1f / Mathf.Tan(12.5f * Mathf.Deg2Rad);
        var v = p - cam; float x = Vector3.Dot(v, right), y = Vector3.Dot(v, up), z = Vector3.Dot(v, fwd);
        return new Vector2(x / z * f * 0.5f + 0.5f, y / z * f * 0.5f + 0.5f);
    }

    // 꼭짓점마다 그림에서 읽을 3D 점과 밝기 배율(shade). 뒤쪽(-x)은 앞으로 접고(축도 같이), 부위 축 쪽으로 55 %만 당긴다
    static Vector3[] LampSamplePoints(LpMesh m, List<LampPart> parts, out float[] shade)
    {
        var s = new Vector3[m.pos.Count]; shade = new float[m.pos.Count];
        for (int i = 0; i < s.Length; i++) { s[i] = m.pos[i]; shade[i] = 1f; }
        foreach (var pt in parts)
            for (int i = pt.v0; i < pt.v1; i++)
            {
                var q0 = m.pos[i]; var n = m.nrm[i]; var sz = pt.hi - pt.lo;
                if (pt.kind == 1)
                {
                    float fx = sz.x > 1e-5f ? (q0.x - pt.lo.x) / sz.x : 0.5f;
                    var fc = new Vector3(pt.hi.x, (pt.lo.y + pt.hi.y) * 0.5f, (pt.lo.z + pt.hi.z) * 0.5f);
                    Vector3 f;
                    if (Mathf.Abs(n.z) > Mathf.Abs(n.x) && Mathf.Abs(n.z) >= Mathf.Abs(n.y))
                    {
                        // 길 쪽(+z) 면: 그림에서 비스듬히(약 17°) 보이는 그 면을 그대로 (예전에는 +x 면을 거울처럼 접어 데칼코마니 무늬가 났다)
                        var c = new Vector3((pt.lo.x + pt.hi.x) * 0.5f, (pt.lo.y + pt.hi.y) * 0.5f, pt.hi.z);
                        var pz = new Vector3(Mathf.Abs(q0.x), q0.y, pt.hi.z);
                        s[i] = c + (pz - c) * 0.85f;
                        continue;
                    }
                    f = new Vector3(pt.hi.x, q0.y, q0.z);
                    if (Mathf.Abs(n.y) > Mathf.Abs(n.x)) f.y = pt.lo.y + fx * sz.y;
                    s[i] = fc + (f - fc) * 0.85f;
                    continue;
                }
                if (pt.kind == 2)
                {
                    // 등 머리: 면 좌표(길이 방향 u, 가로 v)를 위 기둥(6.0~7.3 m, 앞쪽 폭 안) 칠에 옮긴다. 아랫면은 어둡게(유리 판 자리, 게임에서는 빛 판이 덮는다)
                    float u = sz.z > 1e-5f ? (q0.z - pt.lo.z) / sz.z : 0.5f;
                    float w = Mathf.Abs(n.x) > 0.7f ? (sz.y > 1e-5f ? (q0.y - pt.lo.y) / sz.y : 0.5f) : (sz.x > 1e-5f ? (q0.x - pt.lo.x) / sz.x : 0.5f);
                    s[i] = new Vector3(0.012f + 0.03f * w, 6.0f + 1.3f * u, 0.01f);
                    if (n.y < -0.6f) shade[i] = 0.45f;
                    continue;
                }
                var p = q0; p.x = Mathf.Abs(p.x);
                Vector3 cc;
                if (pt.path != null && pt.path.Count > 1)
                {
                    // 꺾은선 위의 가장 가까운 점
                    cc = pt.path[0]; float best = float.MaxValue;
                    for (int k = 0; k + 1 < pt.path.Count; k++)
                    {
                        Vector3 a0 = pt.path[k], b0 = pt.path[k + 1]; var e = b0 - a0;
                        float tt = e.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a0, e) / e.sqrMagnitude) : 0f;
                        var cp = a0 + e * tt; float dd = (cp - p).sqrMagnitude;
                        if (dd < best) { best = dd; cc = cp; }
                    }
                }
                else
                {
                    var a = pt.a; var b = pt.b; a.x = Mathf.Abs(a.x); b.x = Mathf.Abs(b.x); // 축도 접는다 (뒤쪽 볼트가 기둥 옆 플랜지 색을 읽었다)
                    var ab = b - a; float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                    cc = a + ab * t;
                }
                s[i] = cc + (p - cc) * 0.55f;
            }
        return s;
    }

    // 굽기: 텍셀마다 꼭짓점 읽기 점을 보간해 그림에 비춘 색. 빈 곳은 이웃으로 번지고 남은 곳은 평균색 (LpBake와 같은 마무리)
    // detail: 잔무늬 텍스처 (Poly Haven rusty_metal_02, CC0). 그림 속 기둥은 폭이 36픽셀쯤이라 가까이서 흐려서,
    // 조각 좌표(미터)로 0.6 m마다 되풀이하는 잔무늬의 밝기를 곱한다 (평균 밝기로 나눠 전체 색은 그대로, 세기 55 %)
    static Color32[] LampBake(LpMesh m, Vector2[] uv, Vector3[] samp, float[] shade, Texture2D paint, Texture2D detail, float detailMean, int size)
    {
        var px = new Color32[size * size]; var filled = new bool[size * size];
        for (int t = 0; t < m.tri.Count; t += 3)
        {
            int i0 = m.tri[t], i1 = m.tri[t + 1], i2 = m.tri[t + 2];
            Vector2 a = uv[i0] * size, b = uv[i1] * size, c = uv[i2] * size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(den) < 1e-9f) continue;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var qq = new Vector2(x + 0.5f, y + 0.5f);
                    float wa = ((b.y - c.y) * (qq.x - c.x) + (c.x - b.x) * (qq.y - c.y)) / den;
                    float wb = ((c.y - a.y) * (qq.x - c.x) + (a.x - c.x) * (qq.y - c.y)) / den;
                    float wc = 1 - wa - wb;
                    if (wa < -0.02f || wb < -0.02f || wc < -0.02f) continue;
                    var P = samp[i0] * wa + samp[i1] * wb + samp[i2] * wc;
                    var u = LampProject(P);
                    Color col = paint.GetPixelBilinear(u.x, u.y);
                    float sh = shade[i0] * wa + shade[i1] * wb + shade[i2] * wc;
                    col = new Color(col.r * sh, col.g * sh, col.b * sh, 1f);
                    if (detail != null)
                    {
                        var fl = (m.flat[i0] * wa + m.flat[i1] * wb + m.flat[i2] * wc) / 0.6f + new Vector2(m.chart[i0] * 0.371f, m.chart[i0] * 0.613f);
                        float lum = detail.GetPixelBilinear(fl.x, fl.y).grayscale;
                        float k = Mathf.Clamp(Mathf.Lerp(1f, lum / detailMean, 0.55f), 0.55f, 1.45f);
                        col = new Color(col.r * k, col.g * k, col.b * k, 1f);
                    }
                    px[y * size + x] = col; filled[y * size + x] = true;
                }
        }
        for (int it = 0; it < 6; it++)
        {
            var add = new List<int>(); var col = new List<Color32>();
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x; if (filled[i]) continue;
                    int r = 0, g = 0, bl = 0, n = 0;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                        int j = yy * size + xx; if (!filled[j]) continue;
                        r += px[j].r; g += px[j].g; bl += px[j].b; n++;
                    }
                    if (n > 0) { add.Add(i); col.Add(new Color32((byte)(r / n), (byte)(g / n), (byte)(bl / n), 255)); }
                }
            for (int k = 0; k < add.Count; k++) { px[add[k]] = col[k]; filled[add[k]] = true; }
        }
        long sr = 0, sg = 0, sb = 0, sn = 0;
        for (int i = 0; i < px.Length; i++) if (filled[i]) { sr += px[i].r; sg += px[i].g; sb += px[i].b; sn++; }
        var avg = sn > 0 ? new Color32((byte)(sr / sn), (byte)(sg / sn), (byte)(sb / sn), 255) : new Color32(110, 120, 115, 255);
        for (int i = 0; i < px.Length; i++) { if (!filled[i]) px[i] = avg; px[i].a = 255; }
        return px;
    }

    [MenuItem("Tools/핵폭발 생존/가로등 품질별 판 만들기 (직접 짠 모양)")]
    static void LampBuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        var paint = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        paint.LoadImage(File.ReadAllBytes(LampPaintPath));
        paint.wrapMode = TextureWrapMode.Clamp;
        var detail = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        detail.LoadImage(File.ReadAllBytes("Assets/ThirdParty/PolyHaven/rusty_metal_02_diff_1k.jpg"));
        detail.wrapMode = TextureWrapMode.Repeat;
        float dm = 0f; var dpx = detail.GetPixels(); foreach (var c in dpx) dm += c.grayscale; dm /= dpx.Length;
        var log = new List<string>();
        for (int q = 0; q < 4; q++)
        {
            var parts = new List<LampPart>();
            var m = LampBuild(q, parts);
            int size = q == 0 ? 256 : q == 3 ? 1024 : 512;
            var uv = LpPackUv(m, size, q == 0 ? 2 : 3);
            var samp = LampSamplePoints(m, parts, out var shade);
            var px = LampBake(m, uv, samp, shade, paint, detail, dm, size);
            log.Add(LpSave(LampId, m, uv, px, size, "_q" + q, q == 3 ? "HighRes" : "", LpTierDir[q]));
        }
        Object.DestroyImmediate(paint); Object.DestroyImmediate(detail);
        Debug.Log("[GameSceneBuilder] 가로등 품질별 판: " + string.Join(" / ", log));
    }
}
