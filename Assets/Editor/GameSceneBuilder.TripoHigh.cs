using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 실외기 고품질 판 (11-22, 맵 모델링 세션). 사용자가 저폴리 계획을 접고 "폴리수 제한 없이, 퀄리티 높이는 쪽으로" 정했고,
// 이어서 "낮음부터 매우 높음까지 품질을 확연하게 분리"를 요청했다. HqBuildAc(false) = 품질 '높음'(_q2), HqBuildAc(true) = '매우 높음'(_q3).
// 중간 판(GameSceneBuilder.TripoLow.cs의 LpBuildAc)과 같은 치수·같은 굽기 방식에, 둥근 모서리·진짜 둥근 철사 보호망·두께 있는 휜 날개·
// 둥근 축·옆 그릴 날개살·다리 끝막음·꺾인 배관과 너트를 더한다. 매우 높음은 더 촘촘한 철사·나사·날개 뒤 열교환기 핀·텍스처 1024
public static partial class GameSceneBuilder
{
    // 둥근 모서리 상자 껍데기: 면마다 6×6 격자(모서리 쪽 두 칸이 22.5°씩 둥글게)를 만든다.
    // skip: 통째로 뺄 면, hole: 가운데 칸(둥근 모서리 안쪽 평평한 곳)만 뺄 면 — 부르는 쪽이 그 자리를 따로 짠다. 면 번호 0 -x, 1 +x, 2 -y, 3 +y, 4 -z, 5 +z
    static void HqRoundBox(LpMesh m, Vector3 lo, Vector3 hi, float r, int[] skip, int[] hole, Color? solid = null)
    {
        var sk = new HashSet<int>(skip ?? new int[0]); var ho = new HashSet<int>(hole ?? new int[0]);
        float k = 1f - Mathf.Tan(22.5f * Mathf.Deg2Rad);
        for (int f = 0; f < 6; f++)
        {
            if (sk.Contains(f)) continue;
            int ax = f / 2; float s = (f & 1) == 0 ? -1 : 1;
            int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
            float[] c1 = { lo[a1], lo[a1] + r * k, lo[a1] + r, hi[a1] - r, hi[a1] - r * k, hi[a1] };
            float[] c2 = { lo[a2], lo[a2] + r * k, lo[a2] + r, hi[a2] - r, hi[a2] - r * k, hi[a2] };
            int ch = m.NewChart(); if (solid.HasValue) m.solid[ch] = solid.Value;
            var id = new int[6, 6];
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                {
                    var q = Vector3.zero; q[ax] = s < 0 ? lo[ax] : hi[ax]; q[a1] = c1[i]; q[a2] = c2[j];
                    var c = new Vector3(Mathf.Clamp(q.x, lo.x + r, hi.x - r), Mathf.Clamp(q.y, lo.y + r, hi.y - r), Mathf.Clamp(q.z, lo.z + r, hi.z - r));
                    var n = (q - c).normalized;
                    id[i, j] = m.V(c + n * r, n, new Vector2(q[a1], q[a2]), ch);
                }
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    if (i == 2 && j == 2 && ho.Contains(f)) continue;
                    m.T(id[i, j], id[i + 1, j], id[i + 1, j + 1]); m.T(id[i, j], id[i + 1, j + 1], id[i, j + 1]);
                }
        }
    }

    // 원환(도넛): 가운데 c, 축 n, 큰 반지름 R, 철사 반지름 rt
    static void HqTorus(LpMesh m, Vector3 c, Vector3 n, float R, float rt, int segs, int sides, int chart)
    {
        n = n.normalized;
        var e1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var e2 = Vector3.Cross(n, e1);
        var id = new int[segs + 1, sides + 1];
        for (int i = 0; i <= segs; i++)
        {
            float a = 2 * Mathf.PI * i / segs; var radial = e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a);
            for (int j = 0; j <= sides; j++)
            {
                float b = 2 * Mathf.PI * j / sides; var nn = radial * Mathf.Cos(b) + n * Mathf.Sin(b);
                id[i, j] = m.V(c + radial * R + nn * rt, nn, new Vector2(i, j), chart);
            }
        }
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < sides; j++) { m.T(id[i, j], id[i + 1, j], id[i + 1, j + 1]); m.T(id[i, j], id[i + 1, j + 1], id[i, j + 1]); }
    }

    // 육각 나사 머리 (면 위 p, 바깥 법선 n)
    static void HqScrew(LpMesh m, Vector3 p, Vector3 n, float r)
    {
        int before = m.charts;
        LpCyl(m, p - n * 0.002f, p + n * 0.004f, r, 6, false, false);
        for (int c = before; c < m.charts; c++) m.solid[c] = new Color(0.45f, 0.43f, 0.40f);
    }

    static LpMesh HqBuildAc(bool u)
    {
        var m = new LpMesh();
        float x0 = -0.385f, x1 = 0.485f, y0 = -0.27f, y1 = 0.335f, z0 = -0.235f, z1 = 0.232f, rr = 0.016f;
        Vector3 fz = Vector3.forward, ex = Vector3.right, ey = Vector3.up;
        // 몸통: 둥근 모서리. 윗면은 덮개 안이라 빼고, 앞(+z)·오른쪽(+x)은 가운데 평평한 곳을 따로 짠다
        HqRoundBox(m, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), rr, new[] { 3 }, new[] { 1, 5 });
        float ix0 = x0 + rr, ix1 = x1 - rr, iy0 = y0 + rr, iy1 = y1 - rr, iz0 = z0 + rr, iz1 = z1 - rr;

        // 덮개: 옆모양(둥근 윗모서리 반지름 4.5 cm)을 X로 밀어낸다
        {
            float la = -0.394f, lb = 0.494f, hw = 0.258f, yb = 0.30f, yt = 0.368f, fr = 0.045f;
            var prof = new List<Vector2> { new Vector2(yb, -hw) };
            int fs = u ? 5 : 3;
            for (int i = 0; i <= fs; i++) { float a = Mathf.PI - i * Mathf.PI / 2 / fs; prof.Add(new Vector2(yt - fr + Mathf.Sin(a) * fr, -hw + fr + Mathf.Cos(a) * fr)); }
            for (int i = 0; i <= fs; i++) { float a = Mathf.PI / 2 - i * Mathf.PI / 2 / fs; prof.Add(new Vector2(yt - fr + Mathf.Sin(a) * fr, hw - fr + Mathf.Cos(a) * fr)); }
            prof.Add(new Vector2(yb, hw));
            var mid = new Vector2(0.334f, 0);
            for (int i = 0; i < prof.Count; i++)
            {
                var p = prof[i]; var q = prof[(i + 1) % prof.Count];
                if ((q - p).sqrMagnitude < 1e-10f) continue;
                var e = q - p; var n = new Vector3(0, -e.y, e.x).normalized;
                if (Vector3.Dot(n, new Vector3(0, (p.x + q.x) * 0.5f - mid.x, (p.y + q.y) * 0.5f)) < 0) n = -n;
                LpQuad(m, new Vector3(la, p.x, p.y), new Vector3(lb, p.x, p.y), new Vector3(lb, q.x, q.y), new Vector3(la, q.x, q.y), n);
            }
            foreach (var xx in new[] { la, lb })
            {
                var cap = new Vector3[prof.Count];
                for (int i = 0; i < prof.Count; i++) cap[i] = new Vector3(xx, prof[i].x, prof[i].y);
                m.Poly(cap, xx < 0 ? Vector3.left : Vector3.right, fz, ey, m.NewChart());
            }
        }

        // 앞면 가운데: 팬 둘레 네모 바깥 띠 + 네모와 테두리 사이 고리 + 둥근 테두리 입술 + 깊은 통(6 cm) + 바닥
        float fx = 0.17f, fy = 0.03f, r = 0.245f, h = 0.262f, depth = 0.06f, lip = 0.01f;
        int N = u ? 48 : 32; float st = 360f / N; // 8의 배수: 45°·135°·225°·315°가 네모 모서리에 닿는다
        var cir = new Vector3[N]; var sq = new Vector3[N];
        for (int k = 0; k < N; k++)
        {
            float a = st * k * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            cir[k] = new Vector3(fx + (r + lip) * c, fy + (r + lip) * s, z1);
            float t = h / Mathf.Max(Mathf.Abs(c), Mathf.Abs(s));
            sq[k] = new Vector3(fx + t * c, fy + t * s, z1);
        }
        float qy0 = fy - h, qy1 = fy + h;
        int front = m.NewChart();
        {
            var top = new List<Vector3> { new Vector3(ix0, iy1, z1), new Vector3(ix1, iy1, z1), new Vector3(ix1, qy1, z1) };
            for (int k = N / 8; k <= 3 * N / 8; k++) top.Add(sq[k]);
            top.Add(new Vector3(ix0, qy1, z1)); m.Poly(top, fz, ex, ey, front);
            var bot = new List<Vector3> { new Vector3(ix1, iy0, z1), new Vector3(ix0, iy0, z1), new Vector3(ix0, qy0, z1) };
            for (int k = 5 * N / 8; k <= 7 * N / 8; k++) bot.Add(sq[k]);
            bot.Add(new Vector3(ix1, qy0, z1)); m.Poly(bot, fz, ex, ey, front);
            var left = new List<Vector3> { new Vector3(ix0, qy1, z1) };
            for (int k = 3 * N / 8; k <= 5 * N / 8; k++) left.Add(sq[k]);
            left.Add(new Vector3(ix0, qy0, z1)); m.Poly(left, fz, ex, ey, front);
            var right = new List<Vector3> { new Vector3(ix1, qy0, z1), new Vector3(ix1, qy1, z1) };
            for (int k = N / 8; k >= -N / 8; k--) right.Add(sq[(k + N) % N]);
            m.Poly(right, fz, ex, ey, front);
            for (int k = 0; k < N; k++) { int k2 = (k + 1) % N; LpQuad(m, cir[k], cir[k2], sq[k2], sq[k], fz, front); }
        }
        Vector3 F(float rad, float deg, float z) => new Vector3(fx + rad * Mathf.Cos(deg * Mathf.Deg2Rad), fy + rad * Mathf.Sin(deg * Mathf.Deg2Rad), z);
        // 테두리 입술(4분의 1 원) + 통 벽: 고리 단면 (반지름, z)
        {
            var prof = new List<Vector2>();
            for (int i = 0; i <= 4; i++) { float a = i * Mathf.PI / 2 / 4; prof.Add(new Vector2(r + lip - Mathf.Sin(a) * lip, z1 - lip + Mathf.Cos(a) * lip)); }
            prof.Add(new Vector2(r, z1 - depth));
            int wc = m.NewChart(); float acc = 0;
            var ids = new int[prof.Count, N + 1];
            for (int i = 0; i < prof.Count; i++)
            {
                if (i > 0) acc += (prof[i] - prof[i - 1]).magnitude;
                for (int k = 0; k <= N; k++)
                {
                    float deg = st * k; var radial = F(1, deg, 0) - new Vector3(fx, fy, 0);
                    Vector3 n = i < 4 ? (-radial * Mathf.Sin((i * 0.25f + 0.125f) * Mathf.PI / 2) + fz * Mathf.Cos((i * 0.25f + 0.125f) * Mathf.PI / 2)) : -radial;
                    if (i == 0) n = fz; if (i == 4) n = -radial;
                    ids[i, k] = m.V(F(prof[i].x, deg, prof[i].y), n, new Vector2(2 * Mathf.PI * r * k / N, acc), wc);
                }
            }
            for (int i = 0; i + 1 < prof.Count; i++)
                for (int k = 0; k < N; k++) { m.T(ids[i, k], ids[i, k + 1], ids[i + 1, k + 1]); m.T(ids[i, k], ids[i + 1, k + 1], ids[i + 1, k]); }
            var floor = new Vector3[N];
            for (int k = 0; k < N; k++) floor[k] = F(r, st * k, z1 - depth);
            m.Poly(floor, fz, ex, ey, m.NewChart());
        }
        // 매우 높음: 날개 뒤 열교환기 핀 (바닥 앞 8 mm까지 솟은 세로 판 40장, 철망과 날개 사이로 보인다)
        if (u)
        {
            int cc = m.NewChart(); m.solid[cc] = new Color(0.2f, 0.2f, 0.19f);
            float zf1 = z1 - depth + 0.008f, fw = 0.0015f;
            for (int i = 0; i < 40; i++)
            {
                float dx = -r + (i + 0.5f) * 2 * r / 40f, hh = Mathf.Sqrt(Mathf.Max(0, r * r - dx * dx)) - 0.004f;
                if (hh < 0.01f) continue;
                Vector3 a = new Vector3(fx + dx, fy + hh, zf1), b = new Vector3(fx + dx, fy - hh, zf1);
                LpQuad(m, a - ex * fw, a + ex * fw, b + ex * fw, b - ex * fw, fz, cc);
                LpQuad(m, a + ex * fw, b + ex * fw, b + ex * fw - fz * 0.008f, a + ex * fw - fz * 0.008f, ex, cc);
            }
        }
        // 축: 16각 통 + 둥근 앞(두 고리) + 가운데 육각 볼트
        {
            int hc = m.NewChart(); m.solid[hc] = new Color(0.27f, 0.27f, 0.26f);
            int H = u ? 16 : 12; float hr = 0.056f, zb = z1 - depth, zs = z1 - 0.034f;
            var rings = new[] { new Vector2(hr, zb), new Vector2(hr, zs), new Vector2(hr * 0.87f, zs + 0.009f), new Vector2(hr * 0.5f, zs + 0.016f) };
            var ids = new int[rings.Length, H + 1];
            for (int i = 0; i < rings.Length; i++)
                for (int k = 0; k <= H; k++)
                {
                    float deg = 360f * k / H; var radial = F(1, deg, 0) - new Vector3(fx, fy, 0);
                    var n = i < 2 ? radial : (radial + fz * (i == 2 ? 0.8f : 2.5f)).normalized;
                    ids[i, k] = m.V(F(rings[i].x, deg, rings[i].y), n, Vector2.zero, hc);
                }
            for (int i = 0; i + 1 < rings.Length; i++)
                for (int k = 0; k < H; k++) { m.T(ids[i, k], ids[i, k + 1], ids[i + 1, k + 1]); m.T(ids[i, k], ids[i + 1, k + 1], ids[i + 1, k]); }
            int ctr = m.V(new Vector3(fx, fy, zs + 0.019f), fz, Vector2.zero, hc);
            for (int k = 0; k < H; k++) m.T(ctr, ids[rings.Length - 1, k], ids[rings.Length - 1, k + 1]);
            if (u) HqScrew(m, new Vector3(fx, fy, zs + 0.019f), fz, 0.012f);
        }
        // 날개 4장: 두께 4 mm, 가운데가 불룩한 단면, 끝으로 갈수록 넓어졌다 둥글게 좁아지며 28° 휜다. 앞뒤 모서리 높이가 달라 비틀려 있다
        {
            int bc = m.NewChart(); m.solid[bc] = new Color(0.44f, 0.44f, 0.42f);
            float[] tt = u ? new[] { 0f, 0.15f, 0.32f, 0.5f, 0.68f, 0.84f, 0.95f, 1f } : new[] { 0f, 0.3f, 0.62f, 0.88f, 1f };
            float[] hw = u ? new[] { 12f, 20f, 26f, 30f, 31f, 28f, 20f, 8f } : new[] { 12f, 25f, 31f, 24f, 8f };
            float r0 = 0.05f, r1 = r - 0.018f, zc = z1 - depth * 0.55f, pitch = 0.012f, th = 0.004f, camber = 0.004f;
            for (int bl = 0; bl < 4; bl++)
            {
                float baseA = 90f * bl + 15f;
                int S = tt.Length;
                var fr = new int[S, 3]; var bk = new int[S, 3];
                var pos = new Vector3[S, 3];
                for (int i = 0; i < S; i++)
                {
                    float rad = Mathf.Lerp(r0, r1, tt[i]), ac = baseA + 28f * tt[i] * tt[i];
                    float p = pitch * (0.5f + 0.5f * tt[i]);
                    pos[i, 0] = F(rad, ac + hw[i], zc + p);
                    pos[i, 1] = F(rad, ac, zc + camber);
                    pos[i, 2] = F(rad, ac - hw[i], zc - p);
                    for (int j = 0; j < 3; j++)
                    {
                        fr[i, j] = m.V(pos[i, j], fz, Vector2.zero, bc);
                        bk[i, j] = m.V(pos[i, j] - fz * th, -fz, Vector2.zero, bc);
                    }
                }
                for (int i = 0; i + 1 < S; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        m.T(fr[i, j], fr[i + 1, j], fr[i + 1, j + 1]); m.T(fr[i, j], fr[i + 1, j + 1], fr[i, j + 1]);
                        m.T(bk[i, j], bk[i + 1, j], bk[i + 1, j + 1]); m.T(bk[i, j], bk[i + 1, j + 1], bk[i, j + 1]);
                    }
                // 앞 모서리·뒤 모서리·끝: 앞면과 뒷면을 잇는 띠 (법선은 날개 바깥쪽)
                for (int e = 0; e < 2; e++)
                {
                    int j = e == 0 ? 0 : 2;
                    for (int i = 0; i + 1 < S; i++)
                    {
                        var outw = (pos[i, j] - pos[i, 1]) + (pos[i + 1, j] - pos[i + 1, 1]);
                        int a = m.V(pos[i, j], outw, Vector2.zero, bc), b = m.V(pos[i + 1, j], outw, Vector2.zero, bc);
                        int c = m.V(pos[i + 1, j] - fz * th, outw, Vector2.zero, bc), d = m.V(pos[i, j] - fz * th, outw, Vector2.zero, bc);
                        m.T(a, b, c); m.T(a, c, d);
                    }
                }
                for (int j = 0; j < 2; j++)
                {
                    var outw = pos[S - 1, 1] - pos[S - 2, 1];
                    int a = m.V(pos[S - 1, j], outw, Vector2.zero, bc), b = m.V(pos[S - 1, j + 1], outw, Vector2.zero, bc);
                    int c = m.V(pos[S - 1, j + 1] - fz * th, outw, Vector2.zero, bc), d = m.V(pos[S - 1, j] - fz * th, outw, Vector2.zero, bc);
                    m.T(a, b, c); m.T(a, c, d);
                }
            }
        }
        // 보호망: 둥근 철사 고리 6줄 + 굵은 바깥 테 + 살 12개(고리 뒤) + 가운데 원판 + 패널에 고정하는 탭 4개(나사)
        {
            int wc = m.NewChart(); m.solid[wc] = new Color(0.70f, 0.69f, 0.65f);
            float zw = z1 + 0.006f, wt = 0.0032f;
            float[] rings = u ? new[] { 0.062f, 0.097f, 0.132f, 0.167f, 0.202f, 0.236f } : new[] { 0.07f, 0.112f, 0.154f, 0.196f, 0.236f };
            int[] segs = u ? new[] { 24, 32, 36, 40, 48, 48 } : new[] { 16, 20, 24, 28, 32 };
            for (int i = 0; i < rings.Length; i++) HqTorus(m, new Vector3(fx, fy, zw), fz, rings[i], wt, segs[i], u ? 6 : 4, wc);
            HqTorus(m, new Vector3(fx, fy, zw - 0.002f), fz, 0.252f, 0.0055f, u ? 48 : 32, u ? 8 : 5, wc);
            int spokes = u ? 12 : 8;
            for (int s = 0; s < spokes; s++)
            {
                float a = 360f / spokes * (s + 0.5f);
                int before = m.charts;
                LpCyl(m, F(0.045f, a, zw - wt * 1.6f), F(0.252f, a, zw - wt * 1.6f), wt, u ? 6 : 4, false, false);
                for (int c = before; c < m.charts; c++) m.solid[c] = m.solid[wc];
            }
            {
                int before = m.charts;
                LpCyl(m, new Vector3(fx, fy, zw - 0.006f), new Vector3(fx, fy, zw + 0.004f), 0.05f, u ? 24 : 16, false, false);
                for (int c = before; c < m.charts; c++) m.solid[c] = new Color(0.62f, 0.61f, 0.57f);
            }
            foreach (var a in new[] { 45f, 135f, 225f, 315f })
            {
                var p = F(0.262f, a, z1 + 0.002f);
                var dir = (F(1, a, 0) - new Vector3(fx, fy, 0)); var side = new Vector3(-dir.y, dir.x, 0);
                var lo = p - dir * 0.018f - side * 0.012f; var hi = p + dir * 0.014f + side * 0.012f;
                int before = m.charts;
                LpQuad(m, lo + new Vector3(0, 0, 0.002f), p - dir * 0.018f + side * 0.012f + new Vector3(0, 0, 0.002f), hi + new Vector3(0, 0, 0.002f), p + dir * 0.014f - side * 0.012f + new Vector3(0, 0, 0.002f), fz);
                for (int c = before; c < m.charts; c++) m.solid[c] = m.solid[wc];
                if (u) HqScrew(m, p + dir * 0.002f + new Vector3(0, 0, 0.004f), fz, 0.006f);
            }
        }

        // 환기구: 둥근 모서리 판(뒤 빼고) + 두께 있는 날개 7개(아래로 비스듬, 앞에 둥근 입술)
        {
            float vx0 = -0.29f, vx1 = -0.135f, vy0 = 0.01f, vy1 = 0.29f, vz = z1 + 0.007f;
            HqRoundBox(m, new Vector3(vx0, vy0, z1 - 0.003f), new Vector3(vx1, vy1, vz), 0.004f, new[] { 4 }, null);
            for (int i = 0; i < 7; i++)
            {
                float y = 0.262f - 0.033f * i, lx0 = vx0 + 0.014f, lx1 = vx1 - 0.014f;
                // 단면 (y, z): 위 뒤 → 앞 끝 위 → 앞 끝 아래 → 아래 뒤
                var sec = new[] { new Vector2(y + 0.016f, vz), new Vector2(y + 0.002f, vz + 0.013f), new Vector2(y - 0.003f, vz + 0.012f), new Vector2(y - 0.002f, vz) };
                for (int s = 0; s < 3; s++)
                {
                    var p = sec[s]; var q = sec[s + 1]; var e = q - p; var n = new Vector3(0, -e.y, e.x).normalized;
                    if (Vector3.Dot(n, new Vector3(0, (p.x + q.x) * 0.5f - (y + 0.004f), (p.y + q.y) * 0.5f - vz)) < 0) n = -n;
                    LpQuad(m, new Vector3(lx0, p.x, p.y), new Vector3(lx1, p.x, p.y), new Vector3(lx1, q.x, q.y), new Vector3(lx0, q.x, q.y), n);
                }
                foreach (var xx in new[] { lx0, lx1 })
                {
                    var cap = new Vector3[4]; for (int s = 0; s < 4; s++) cap[s] = new Vector3(xx, sec[s].x, sec[s].y);
                    m.Poly(cap, xx < -0.2f ? Vector3.left : Vector3.right, fz, ey, m.NewChart());
                }
            }
            if (u)
                foreach (var sp in new[] { new Vector3(vx0 + 0.012f, vy1 - 0.012f, vz), new Vector3(vx1 - 0.012f, vy1 - 0.012f, vz), new Vector3(vx0 + 0.012f, vy0 + 0.012f, vz), new Vector3(vx1 - 0.012f, vy0 + 0.012f, vz) })
                    HqScrew(m, sp, fz, 0.0055f);
        }
        // 앞판 나사 (팬 왼쪽 위 등 네 귀퉁이)
        if (u)
            foreach (var sp in new[] { new Vector3(0.455f, 0.30f, z1), new Vector3(0.455f, -0.24f, z1), new Vector3(-0.355f, 0.30f, z1), new Vector3(-0.355f, -0.24f, z1) })
                HqScrew(m, sp, fz, 0.007f);

        // 오른쪽(+x) 가운데: 테두리 + 움푹한 그릴(깊이 2 cm) + 비스듬한 날개살 22개 + 뒤판
        {
            float gz0 = -0.145f, gz1 = 0.135f, gy0 = -0.235f, gy1 = 0.27f, gd = 0.02f;
            System.Func<float, float, float, Vector3> P = (yy, zz, xx) => new Vector3(x1 - xx, yy, zz);
            int sc = m.NewChart(); var rx = Vector3.right;
            LpQuad(m, P(iy1, iz0, 0), P(iy1, iz1, 0), P(gy1, gz1, 0), P(gy1, gz0, 0), rx, sc);
            LpQuad(m, P(gy0, gz0, 0), P(gy0, gz1, 0), P(iy0, iz1, 0), P(iy0, iz0, 0), rx, sc);
            LpQuad(m, P(iy1, iz0, 0), P(gy1, gz0, 0), P(gy0, gz0, 0), P(iy0, iz0, 0), rx, sc);
            LpQuad(m, P(gy1, gz1, 0), P(iy1, iz1, 0), P(iy0, iz1, 0), P(gy0, gz1, 0), rx, sc);
            LpQuad(m, P(gy1, gz0, 0), P(gy1, gz1, 0), P(gy1, gz1, gd), P(gy1, gz0, gd), Vector3.down);
            LpQuad(m, P(gy0, gz0, 0), P(gy0, gz1, 0), P(gy0, gz1, gd), P(gy0, gz0, gd), Vector3.up);
            LpQuad(m, P(gy1, gz0, 0), P(gy0, gz0, 0), P(gy0, gz0, gd), P(gy1, gz0, gd), Vector3.forward);
            LpQuad(m, P(gy1, gz1, 0), P(gy0, gz1, 0), P(gy0, gz1, gd), P(gy1, gz1, gd), Vector3.back);
            LpQuad(m, P(gy1, gz0, gd), P(gy1, gz1, gd), P(gy0, gz1, gd), P(gy0, gz0, gd), rx);
            int fc = m.NewChart(); m.solid[fc] = new Color(0.16f, 0.16f, 0.16f);
            int fins = u ? 22 : 14;
            for (int i = 0; i < fins; i++)
            {
                float y = gy1 - 0.012f - i * (gy1 - gy0 - 0.024f) / (fins - 1);
                var a = P(y + 0.009f, gz0, gd - 0.002f); var b = P(y + 0.009f, gz1, gd - 0.002f); var c = P(y - 0.003f, gz1, 0.004f); var d = P(y - 0.003f, gz0, 0.004f);
                LpQuad(m, a, b, c, d, new Vector3(0.75f, 0.66f, 0), fc);
                LpQuad(m, d, c, c - new Vector3(0, 0.003f, 0), d - new Vector3(0, 0.003f, 0), rx, fc);
            }
            if (u) { HqScrew(m, P(iy1 - 0.02f, 0.0f, 0), rx, 0.007f); HqScrew(m, P(iy0 + 0.015f, 0.0f, 0), rx, 0.007f); }
        }

        // 다리: 16각 속 빈 관 둘(앞 끝 구멍, 뒤 막음) + 둥근 모서리 받침 넷과 볼트
        foreach (var fxp in new[] { -0.247f, 0.359f })
        {
            LpCyl(m, new Vector3(fxp, -0.34f, -0.265f), new Vector3(fxp, -0.34f, 0.265f), 0.037f, u ? 16 : 12, true, true);
            foreach (var bz in new[] { -0.15f, 0.15f })
            {
                HqRoundBox(m, new Vector3(fxp - 0.022f, -0.312f, bz - 0.022f), new Vector3(fxp + 0.022f, y0 + 0.004f, bz + 0.022f), 0.004f, new[] { 2, 3 }, null);
                if (u) HqScrew(m, new Vector3(fxp, -0.291f, bz + 0.022f), fz, 0.006f);
            }
        }
        // 배관: 옆면 너트(육각) → 밖으로 나와 앞으로 꺾이는 12각 관 → 끝은 속이 보이게
        {
            int before = m.charts;
            LpCyl(m, new Vector3(x0 + 0.004f, -0.175f, 0.0f), new Vector3(x0 - 0.024f, -0.175f, 0.0f), 0.058f, 6, false, false);
            for (int c = before; c < m.charts; c++) m.solid[c] = new Color(0.5f, 0.4f, 0.24f);
            var path = new List<Vector3>(); var rad = new List<float>();
            int bs = u ? 6 : 3;
            for (int i = 0; i <= bs; i++)
            {
                float a = i / (float)bs * Mathf.PI / 2 * 0.55f;
                path.Add(new Vector3(x0 - 0.02f - 0.07f * Mathf.Sin(a), -0.175f, 0.0f + 0.07f * (1 - Mathf.Cos(a))));
                rad.Add(0.045f);
            }
            path.Insert(0, new Vector3(x0 - 0.005f, -0.175f, 0.0f)); rad.Add(0.045f);
            LpTube(m, path, rad, u ? 12 : 8, false, false);
            var last = path[path.Count - 1]; var dir = (last - path[path.Count - 2]).normalized;
            LpCyl(m, last - dir * 0.002f, last + dir * 0.03f, 0.045f, u ? 12 : 8, true, false);
        }
        return m;
    }
}
