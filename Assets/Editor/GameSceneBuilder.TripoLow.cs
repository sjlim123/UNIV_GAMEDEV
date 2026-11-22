using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tripo 소품의 저폴리 판 (11-22, 맵 모델링 세션 4단계). 도시에 아주 많이 들어가는 실외기(467개)·죽은 나무(121개)는 원본 그대로면 너무 무겁다.
// Tripo의 면 줄이기(리토폴로지)는 모양을 망가뜨려서(실외기 막대, 나무 사라짐) 쓰지 않고, 면을 직접 새로 짠 뒤 원본 Tripo 텍스처를 새 면에 구워 넣는다.
// - 실외기: 상자 몸통 + 움푹한 팬 자리 + 다리 둘 + 배관. 치수는 원본 모델(앞 = +Z, 긴 변 약 1)에서 잰 값
// - 나무: 원본 표면을 뿌리에서부터 표면 거리로 잘라 가지 뼈대를 찾고, 그 뼈대를 따라 굵기가 줄어드는 각기둥 관을 짠다
// - 굽기: 새 면의 텍셀마다 원본 표면에서 가장 가까운 점(같은 쪽을 보는 면)을 찾아 원본 텍스처 색을 옮긴다
// 결과 (원본 폴더 안 품질별 폴더 Q0_낮음·Q1_중간·Q2_높음·Q3_매우높음): {id}_q번호.asset(메시), _diff.png(구운 텍스처), .prefab. 나무는 아직 {id}_lp 한 판. 좌표는 '앞 = +Z, 위 = +Y'라 TripoSpecs의 yaw는 0
public static partial class GameSceneBuilder
{
    // 짜는 메시: 꼭짓점마다 조각(chart) 번호와 조각 안 2D 좌표(펼친 자리, 원본 단위)를 갖는다. 다 짠 뒤 조각들을 한 장에 늘어놓아 UV를 만든다
    class LpMesh
    {
        public readonly List<Vector3> pos = new List<Vector3>();
        public readonly List<Vector3> nrm = new List<Vector3>();
        public readonly List<Vector2> flat = new List<Vector2>();
        public readonly List<int> chart = new List<int>();
        public readonly List<int> tri = new List<int>();
        public int charts;
        // 원본 텍스처를 굽지 않고 정한 색(약한 얼룩)으로 칠할 조각. 원본에 없는 새 부품(실외기 날개·철망)용. 펼친 자리도 작게 잡는다
        public readonly Dictionary<int, Color> solid = new Dictionary<int, Color>();
        public int NewChart() => charts++;
        public int V(Vector3 p, Vector3 n, Vector2 f, int c) { pos.Add(p); nrm.Add(n.normalized); flat.Add(f); chart.Add(c); return pos.Count - 1; }
        // 감는 방향은 꼭짓점 법선을 보고 맞춘다 (Unity는 앞면이 시계 방향: Cross(b-a, c-a)가 법선 쪽)
        public void T(int a, int b, int c)
        {
            var n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
            if (n.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(n, nrm[a] + nrm[b] + nrm[c]) < 0) { int t = b; b = c; c = t; }
            tri.Add(a); tri.Add(b); tri.Add(c);
        }
        // 평면 다각형(볼록, 같은 선 위 점 허용)을 첫 점에서 부채꼴로. 법선 n, 조각 좌표는 평면 두 축(ax, ay)에 비춘 값
        public void Poly(IList<Vector3> pts, Vector3 n, Vector3 ax, Vector3 ay, int c)
        {
            var id = new int[pts.Count];
            for (int i = 0; i < pts.Count; i++) id[i] = V(pts[i], n, new Vector2(Vector3.Dot(pts[i], ax), Vector3.Dot(pts[i], ay)), c);
            for (int i = 1; i + 1 < pts.Count; i++) T(id[0], id[i], id[i + 1]);
        }
        // 상자: 빼고 싶은 면은 skip에 (0 -x, 1 +x, 2 -y, 3 +y, 4 -z, 5 +z)
        public void Box(Vector3 lo, Vector3 hi, params int[] skip)
        {
            var sk = new HashSet<int>(skip);
            for (int f = 0; f < 6; f++)
            {
                if (sk.Contains(f)) continue;
                int ax = f / 2; float s = (f & 1) == 0 ? -1 : 1;
                var n = Vector3.zero; n[ax] = s;
                int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                var e1 = Vector3.zero; e1[a1] = 1; var e2 = Vector3.zero; e2[a2] = 1;
                var q = new Vector3[4];
                for (int k = 0; k < 4; k++)
                {
                    var p = Vector3.zero;
                    p[ax] = s < 0 ? lo[ax] : hi[ax];
                    p[a1] = (k == 1 || k == 2) ? hi[a1] : lo[a1];
                    p[a2] = (k >= 2) ? hi[a2] : lo[a2];
                    q[k] = p;
                }
                Poly(q, n, e1, e2, NewChart());
            }
        }
    }

    // ---------------- 실외기 ----------------
    // 원본 ac_unit_01 (yaw 90을 돌린 뒤, 앞 = 팬 쪽 = +Z, 긴 변 약 1)에서 잰 치수. 앞·옆 사진에 격자를 그어 읽었다
    // (Docs/맵모델링_미리보기/06_도시넣기_4단계_실외기_나무/설명.md). 11-22 사용자 "실외기는 512로": 삼각형 약 500개
    static LpMesh LpBuildAc()
    {
        var m = new LpMesh();
        float x0 = -0.385f, x1 = 0.485f, y0 = -0.27f, y1 = 0.335f, z0 = -0.235f, z1 = 0.232f;
        Vector3 fz = Vector3.forward, ex = Vector3.right, ey = Vector3.up;
        // 몸통: 뒤·아래·왼쪽은 평면. 윗면은 덮개 안이라 뺀다. 앞(팬)·오른쪽(그릴)은 아래에서 따로
        m.Poly(new[] { new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0) }, Vector3.back, ex, ey, m.NewChart());
        m.Poly(new[] { new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1) }, Vector3.down, ex, fz, m.NewChart());
        m.Poly(new[] { new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1) }, Vector3.left, fz, ey, m.NewChart());

        // 덮개: 몸통보다 앞뒤로 조금 넓고 윗모서리를 비스듬히 깎은 판 (옆에서 본 모양 6점을 X로 밀어낸다)
        {
            float la = -0.392f, lb = 0.492f;
            var prof = new[] { new Vector2(0.30f, -0.258f), new Vector2(0.335f, -0.258f), new Vector2(0.368f, -0.205f), new Vector2(0.368f, 0.205f), new Vector2(0.335f, 0.258f), new Vector2(0.30f, 0.258f) };
            for (int i = 0; i < prof.Length; i++)
            {
                var p = prof[i]; var q = prof[(i + 1) % prof.Length];
                var e = q - p; var n = new Vector3(0, -e.y, e.x).normalized; // (y, z) 평면에서 변에 수직
                if (Vector3.Dot(n, new Vector3(0, (p.x + q.x) * 0.5f - 0.335f, (p.y + q.y) * 0.5f)) < 0) n = -n; // 덮개 가운데에서 바깥쪽
                LpQuad(m, new Vector3(la, p.x, p.y), new Vector3(lb, p.x, p.y), new Vector3(lb, q.x, q.y), new Vector3(la, q.x, q.y), n);
            }
            foreach (var xx in new[] { la, lb })
            {
                var cap = new Vector3[prof.Length];
                for (int i = 0; i < prof.Length; i++) cap[i] = new Vector3(xx, prof[i].x, prof[i].y);
                m.Poly(cap, xx < 0 ? Vector3.left : Vector3.right, fz, ey, m.NewChart());
            }
        }

        // 앞면: 팬 둘레 네모(반 너비 h) 바깥 띠 넷 + 네모와 원 사이 고리 + 움푹한 벽(깊이 5 cm) + 바닥 + 축 + 날개 4장 + 보호 철망
        float fx = 0.17f, fy = 0.03f, r = 0.245f, h = 0.26f, depth = 0.05f, hubR = 0.06f;
        const int N = 24; // 0°부터 15°마다: 45°·135°·225°·315°가 네모 모서리에 닿는다
        var cir = new Vector3[N]; var sq = new Vector3[N];
        for (int k = 0; k < N; k++)
        {
            float a = 15f * k * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            cir[k] = new Vector3(fx + r * c, fy + r * s, z1);
            float t = h / Mathf.Max(Mathf.Abs(c), Mathf.Abs(s));
            sq[k] = new Vector3(fx + t * c, fy + t * s, z1);
        }
        float qy0 = fy - h, qy1 = fy + h;
        int front = m.NewChart();
        // 위 띠 (k 3..9 = 45°..135°), 아래 띠 (15..21), -X 쪽 띠 (9..15), +X 쪽 띠 (21..27)
        var top = new List<Vector3> { new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, qy1, z1) };
        for (int k = 3; k <= 9; k++) top.Add(sq[k]);
        top.Add(new Vector3(x0, qy1, z1));
        m.Poly(top, fz, ex, ey, front);
        var bot = new List<Vector3> { new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, qy0, z1) };
        for (int k = 15; k <= 21; k++) bot.Add(sq[k]);
        bot.Add(new Vector3(x1, qy0, z1));
        m.Poly(bot, fz, ex, ey, front);
        var left = new List<Vector3> { new Vector3(x0, qy1, z1) };
        for (int k = 9; k <= 15; k++) left.Add(sq[k]);
        left.Add(new Vector3(x0, qy0, z1));
        m.Poly(left, fz, ex, ey, front);
        var right = new List<Vector3> { new Vector3(x1, qy0, z1), new Vector3(x1, qy1, z1) };
        for (int k = 3; k >= -3; k--) right.Add(sq[(k + N) % N]);
        m.Poly(right, fz, ex, ey, front);
        for (int k = 0; k < N; k++)
        {
            int k2 = (k + 1) % N;
            LpQuad(m, cir[k], cir[k2], sq[k2], sq[k], fz, front);
        }
        int wall = m.NewChart();
        float circ = 2 * Mathf.PI * r;
        var dz = new Vector3(0, 0, depth);
        for (int k = 0; k < N; k++)
        {
            int k2 = (k + 1) % N;
            var inA = new Vector3(fx - cir[k].x, fy - cir[k].y, 0); var inB = new Vector3(fx - cir[k2].x, fy - cir[k2].y, 0);
            float u0 = circ * k / N, u1 = circ * (k + 1) / N;
            int a = m.V(cir[k], inA, new Vector2(u0, 0), wall), b = m.V(cir[k2], inB, new Vector2(u1, 0), wall);
            int c = m.V(cir[k2] - dz, inB, new Vector2(u1, depth), wall), d = m.V(cir[k] - dz, inA, new Vector2(u0, depth), wall);
            m.T(a, b, c); m.T(a, c, d);
        }
        // 팬 자리 바닥: 원본의 어두운 열교환기 무늬를 그대로 굽는다
        var floor = new Vector3[N];
        for (int k = 0; k < N; k++) floor[k] = cir[k] - dz;
        m.Poly(floor, fz, ex, ey, m.NewChart());
        // 11-22 사용자 "팬 부분을 선풍기 날개처럼, 그 위에 보호 철창망": 날개·축·철망은 원본 텍스처 대신 정한 색으로 칠한다 (LpMesh.solid)
        Vector3 F(float rr, float deg, float z) => new Vector3(fx + rr * Mathf.Cos(deg * Mathf.Deg2Rad), fy + rr * Mathf.Sin(deg * Mathf.Deg2Rad), z);
        // 가운데 축: 짧은 12각 통 + 살짝 둥근 앞
        {
            int hc = m.NewChart(); m.solid[hc] = new Color(0.30f, 0.30f, 0.29f);
            float zb = z1 - depth, zf = z1 - 0.024f; const int H = 12;
            for (int k = 0; k < H; k++)
            {
                float a0 = 360f * k / H, a1 = 360f * (k + 1) / H;
                var n0 = F(1, a0, 0) - new Vector3(fx, fy, 0); var n1 = F(1, a1, 0) - new Vector3(fx, fy, 0);
                int i0 = m.V(F(hubR, a0, zb), n0, new Vector2(k, 0), hc), i1 = m.V(F(hubR, a1, zb), n1, new Vector2(k + 1, 0), hc);
                int i2 = m.V(F(hubR * 0.8f, a1, zf), n1 + fz, new Vector2(k + 1, 1), hc), i3 = m.V(F(hubR * 0.8f, a0, zf), n0 + fz, new Vector2(k, 1), hc);
                m.T(i0, i1, i2); m.T(i0, i2, i3);
            }
            int ctr = m.V(new Vector3(fx, fy, zf + 0.006f), fz, Vector2.zero, hc);
            var hv = new int[H];
            for (int k = 0; k < H; k++) hv[k] = m.V(F(hubR * 0.8f, 360f * k / H, zf), fz, Vector2.zero, hc);
            for (int k = 0; k < H; k++) m.T(ctr, hv[k], hv[(k + 1) % H]);
        }
        // 날개 4장: 뿌리에서 끝으로 갈수록 넓어졌다가 둥글게 좁아지고, 돌아가는 쪽으로 휘며, 앞뒤 모서리 높이가 달라 비틀려 보인다
        {
            int bc = m.NewChart(); m.solid[bc] = new Color(0.46f, 0.46f, 0.44f);
            float[] tt = { 0f, 0.35f, 0.72f, 1f }, half = { 13f, 27f, 30f, 14f };
            float r0 = hubR * 0.85f, r1 = r - 0.022f, zc = z1 - depth * 0.55f, pitch = 0.009f;
            for (int bl = 0; bl < 4; bl++)
            {
                float baseA = 90f * bl + 20f;
                var lead = new int[tt.Length]; var trail = new int[tt.Length];
                for (int i = 0; i < tt.Length; i++)
                {
                    float rr = Mathf.Lerp(r0, r1, tt[i]), ac = baseA + 24f * tt[i];
                    float p = pitch * (0.6f + 0.4f * tt[i]);
                    lead[i] = m.V(F(rr, ac + half[i], zc + p), fz, Vector2.zero, bc);
                    trail[i] = m.V(F(rr, ac - half[i], zc - p), fz, Vector2.zero, bc);
                }
                for (int i = 0; i + 1 < tt.Length; i++) { m.T(lead[i], lead[i + 1], trail[i + 1]); m.T(lead[i], trail[i + 1], trail[i]); }
            }
        }
        // 보호 철망: 팬 앞 3 mm에 고리 4줄 + 살 8개 + 가운데 원판. 철사는 앞을 보는 얇은 띠(폭 0.8 cm)
        {
            int wc = m.NewChart(); m.solid[wc] = new Color(0.72f, 0.71f, 0.67f);
            float zw = z1 + 0.003f, w = 0.004f;
            float[] rings = { 0.075f, 0.125f, 0.18f, 0.236f }; int[] segs = { 12, 16, 20, 24 };
            for (int ri = 0; ri < rings.Length; ri++)
                for (int k = 0; k < segs[ri]; k++)
                {
                    float a0 = 360f * k / segs[ri], a1 = 360f * (k + 1) / segs[ri];
                    LpQuad(m, F(rings[ri] - w, a0, zw), F(rings[ri] - w, a1, zw), F(rings[ri] + w, a1, zw), F(rings[ri] + w, a0, zw), fz, wc);
                }
            for (int s = 0; s < 8; s++)
            {
                float a = 45f * s + 22.5f;
                var dir = F(1, a, 0) - new Vector3(fx, fy, 0); var side = new Vector3(-dir.y, dir.x, 0) * w;
                Vector3 pa = F(0.05f, a, zw + 0.0005f), pb = F(0.24f, a, zw + 0.0005f);
                LpQuad(m, pa - side, pb - side, pb + side, pa + side, fz, wc);
            }
            var plate = new Vector3[12];
            for (int k = 0; k < 12; k++) plate[k] = F(0.052f, 30f * k, zw + 0.001f);
            m.Poly(plate, fz, ex, ey, wc);
        }

        // 환기구: 앞면 -X 쪽에 살짝 솟은 판 + 아래로 비스듬한 날개 7개
        {
            float vx0 = -0.29f, vx1 = -0.135f, vy0 = 0.01f, vy1 = 0.29f, vz = z1 + 0.006f;
            m.Box(new Vector3(vx0, vy0, z1 - 0.002f), new Vector3(vx1, vy1, vz), 4);
            int lc = m.NewChart();
            for (int i = 0; i < 7; i++)
            {
                float y = 0.262f - 0.033f * i, lx0 = vx0 + 0.012f, lx1 = vx1 - 0.012f;
                Vector3 a = new Vector3(lx0, y + 0.014f, vz), b = new Vector3(lx1, y + 0.014f, vz), c = new Vector3(lx1, y, vz + 0.012f), d = new Vector3(lx0, y, vz + 0.012f);
                Vector3 c2 = new Vector3(lx1, y - 0.004f, vz + 0.012f), d2 = new Vector3(lx0, y - 0.004f, vz + 0.012f);
                LpQuad(m, a, b, c, d, new Vector3(0, 0.7f, 0.7f));   // 비스듬한 윗면
                LpQuad(m, d, c, c2, d2, fz, lc);                          // 날개 끝
                m.Poly(new[] { a, d, d2 }, Vector3.left, fz, ey, m.NewChart());
                m.Poly(new[] { b, c, c2 }, Vector3.right, fz, ey, m.NewChart());
            }
        }

        // 오른쪽(+X) 옆면: 테두리 + 움푹한 그릴(깊이 1.2 cm)
        {
            float gz0 = -0.145f, gz1 = 0.135f, gy0 = -0.235f, gy1 = 0.27f, gd = 0.012f;
            System.Func<float, float, float, Vector3> P = (yy, zz, xx) => new Vector3(x1 - xx, yy, zz);
            int sc = m.NewChart();
            var rx = Vector3.right;
            LpQuad(m, P(y1, z0, 0), P(y1, z1, 0), P(gy1, gz1, 0), P(gy1, gz0, 0), rx, sc);
            LpQuad(m, P(gy0, gz0, 0), P(gy0, gz1, 0), P(y0, z1, 0), P(y0, z0, 0), rx, sc);
            LpQuad(m, P(y1, z0, 0), P(gy1, gz0, 0), P(gy0, gz0, 0), P(y0, z0, 0), rx, sc);
            LpQuad(m, P(gy1, gz1, 0), P(y1, z1, 0), P(y0, z1, 0), P(gy0, gz1, 0), rx, sc);
            LpQuad(m, P(gy1, gz0, 0), P(gy1, gz1, 0), P(gy1, gz1, gd), P(gy1, gz0, gd), Vector3.down);
            LpQuad(m, P(gy0, gz0, 0), P(gy0, gz1, 0), P(gy0, gz1, gd), P(gy0, gz0, gd), Vector3.up);
            LpQuad(m, P(gy1, gz0, 0), P(gy0, gz0, 0), P(gy0, gz0, gd), P(gy1, gz0, gd), Vector3.forward);
            LpQuad(m, P(gy1, gz1, 0), P(gy0, gz1, 0), P(gy0, gz1, gd), P(gy1, gz1, gd), Vector3.back);
            LpQuad(m, P(gy1, gz0, gd), P(gy1, gz1, gd), P(gy0, gz1, gd), P(gy0, gz0, gd), rx, m.NewChart());
        }

        // 다리: 앞뒤로 긴 속 빈 관 둘(앞 끝은 구멍이 보이게, 뒤 끝은 벽 쪽이라 막는다) + 몸통과 잇는 받침 넷
        foreach (var fxp in new[] { -0.247f, 0.359f })
        {
            LpCyl(m, new Vector3(fxp, -0.34f, -0.265f), new Vector3(fxp, -0.34f, 0.265f), 0.037f, 8, true, true);
            foreach (var bz in new[] { -0.15f, 0.15f })
                m.Box(new Vector3(fxp - 0.02f, -0.31f, bz - 0.02f), new Vector3(fxp + 0.02f, y0, bz + 0.02f), 2, 3);
        }
        // 배관: 왼쪽(-X) 옆면에서 나와 앞으로 조금 꺾인 관, 끝은 속이 보이게
        LpCyl(m, new Vector3(x0 + 0.01f, -0.175f, 0.0f), new Vector3(-0.445f, -0.175f, 0.02f), 0.05f, 8, false, false);
        LpCyl(m, new Vector3(-0.44f, -0.175f, 0.015f), new Vector3(-0.485f, -0.175f, 0.085f), 0.05f, 8, true, false);
        return m;
    }

    // 품질 '낮음' 판 (삼각형 약 124): 상자 몸통 + 납작한 팬 자리(12각) + 다리 둘 + 육각 배관. 11-22 첫 저폴리 판과 같은 모양
    static LpMesh LpBuildAcSimple()
    {
        var m = new LpMesh();
        float x0 = -0.385f, x1 = 0.485f, y0 = -0.302f, y1 = 0.368f, z0 = -0.235f, z1 = 0.232f;
        m.Box(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), 5);
        float fx = 0.172f, fy = 0.031f, r = 0.238f, h = 0.255f, depth = 0.02f;
        const int N = 12; // 15°부터 30°마다: 45°·135°·225°·315°가 네모 모서리에 닿는다
        var cir = new Vector3[N]; var sq = new Vector3[N];
        for (int k = 0; k < N; k++)
        {
            float a = (15f + 30f * k) * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            cir[k] = new Vector3(fx + r * c, fy + r * s, z1);
            float t = h / Mathf.Max(Mathf.Abs(c), Mathf.Abs(s));
            sq[k] = new Vector3(fx + t * c, fy + t * s, z1);
        }
        float qy0 = fy - h, qy1 = fy + h;
        int front = m.NewChart();
        Vector3 fz = Vector3.forward, ex = Vector3.right, ey = Vector3.up;
        m.Poly(new[] { new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, qy1, z1), sq[1], sq[2], sq[3], sq[4], new Vector3(x0, qy1, z1) }, fz, ex, ey, front);
        m.Poly(new[] { new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, qy0, z1), sq[7], sq[8], sq[9], sq[10], new Vector3(x1, qy0, z1) }, fz, ex, ey, front);
        m.Poly(new[] { new Vector3(x0, qy1, z1), sq[4], sq[5], sq[6], sq[7], new Vector3(x0, qy0, z1) }, fz, ex, ey, front);
        m.Poly(new[] { new Vector3(x1, qy0, z1), new Vector3(x1, qy1, z1), sq[1], sq[0], sq[11], sq[10] }, fz, ex, ey, front);
        for (int k = 0; k < N; k++) { int k2 = (k + 1) % N; LpQuad(m, cir[k], cir[k2], sq[k2], sq[k], fz, front); }
        int wall = m.NewChart();
        float circ = 2 * Mathf.PI * r;
        for (int k = 0; k < N; k++)
        {
            int k2 = (k + 1) % N;
            var inA = new Vector3(fx - cir[k].x, fy - cir[k].y, 0); var inB = new Vector3(fx - cir[k2].x, fy - cir[k2].y, 0);
            float u0 = circ * k / N, u1 = circ * (k + 1) / N;
            int a = m.V(cir[k], inA, new Vector2(u0, 0), wall), b = m.V(cir[k2], inB, new Vector2(u1, 0), wall);
            int c = m.V(cir[k2] - new Vector3(0, 0, depth), inB, new Vector2(u1, depth), wall), d = m.V(cir[k] - new Vector3(0, 0, depth), inA, new Vector2(u0, depth), wall);
            m.T(a, b, c); m.T(a, c, d);
        }
        var disc = new Vector3[N];
        for (int k = 0; k < N; k++) disc[k] = cir[k] - new Vector3(0, 0, depth);
        m.Poly(disc, fz, ex, ey, m.NewChart());
        m.Box(new Vector3(-0.291f, -0.379f, -0.263f), new Vector3(-0.203f, y0, 0.263f), 3);
        m.Box(new Vector3(0.304f, -0.379f, -0.263f), new Vector3(0.414f, y0, 0.263f), 3);
        LpTube(m, new List<Vector3> { new Vector3(x0 + 0.01f, -0.18f, 0.05f), new Vector3(-0.49f, -0.18f, 0.08f) }, new List<float> { 0.062f, 0.062f }, 6, false, true);
        return m;
    }

    // 평면 사각형 하나 (조각 chart, 비면 새 조각). 조각 좌표 축은 법선만으로 정한다: 같은 조각을 쓰는 사각형끼리 펼친 자리가 겹치지 않게
    // (사각형마다 a→b 축을 쓰면 한 조각 안에서 서로 다른 방향으로 펼쳐져 텍스처가 깨진다). 같은 조각에는 법선이 같은 사각형만 넣는다
    static void LpQuad(LpMesh m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, int chart = -1)
    {
        n = n.normalized;
        var ax = Mathf.Abs(n.y) < 0.9f ? Vector3.Cross(Vector3.up, n).normalized : Vector3.right;
        var ay = Vector3.Cross(n, ax).normalized;
        m.Poly(new[] { a, b, c, d }, n, ax, ay, chart < 0 ? m.NewChart() : chart);
    }

    // 곧은 원기둥 a→b. hollowEnd = b 끝을 속 빈 관(안지름 65 %, 안쪽 깊이 4 cm까지)으로, 아니면 막는다. capStart = a 끝을 막는다
    static void LpCyl(LpMesh m, Vector3 a, Vector3 b, float r, int sides, bool hollowEnd, bool capStart)
    {
        var d = (b - a).normalized; float len = (b - a).magnitude;
        var e1 = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var e2 = Vector3.Cross(d, e1);
        float circ = 2 * Mathf.PI * r;
        System.Func<int, Vector3> Rad = k => { float an = 2 * Mathf.PI * k / sides; return e1 * Mathf.Cos(an) + e2 * Mathf.Sin(an); };
        int ch = m.NewChart();
        for (int k = 0; k < sides; k++)
        {
            var n0 = Rad(k); var n1 = Rad(k + 1);
            int i0 = m.V(a + n0 * r, n0, new Vector2(circ * k / sides, 0), ch), i1 = m.V(a + n1 * r, n1, new Vector2(circ * (k + 1) / sides, 0), ch);
            int i2 = m.V(b + n1 * r, n1, new Vector2(circ * (k + 1) / sides, len), ch), i3 = m.V(b + n0 * r, n0, new Vector2(circ * k / sides, len), ch);
            m.T(i0, i1, i2); m.T(i0, i2, i3);
        }
        if (capStart)
        {
            var cap = new Vector3[sides]; for (int k = 0; k < sides; k++) cap[k] = a + Rad(k) * r;
            m.Poly(cap, -d, e1, e2, m.NewChart());
        }
        if (hollowEnd)
        {
            float ri = r * 0.65f, deep = Mathf.Min(0.04f, len * 0.5f);
            int ac = m.NewChart();
            for (int k = 0; k < sides; k++) LpQuad(m, b + Rad(k) * r, b + Rad(k + 1) * r, b + Rad(k + 1) * ri, b + Rad(k) * ri, d, ac);
            int ic = m.NewChart(); float icirc = 2 * Mathf.PI * ri;
            for (int k = 0; k < sides; k++)
            {
                var n0 = -Rad(k); var n1 = -Rad(k + 1);
                int i0 = m.V(b + Rad(k) * ri, n0, new Vector2(icirc * k / sides, 0), ic), i1 = m.V(b + Rad(k + 1) * ri, n1, new Vector2(icirc * (k + 1) / sides, 0), ic);
                int i2 = m.V(b - d * deep + Rad(k + 1) * ri, n1, new Vector2(icirc * (k + 1) / sides, deep), ic), i3 = m.V(b - d * deep + Rad(k) * ri, n0, new Vector2(icirc * k / sides, deep), ic);
                m.T(i0, i1, i2); m.T(i0, i2, i3);
            }
            var bottom = new Vector3[sides]; for (int k = 0; k < sides; k++) bottom[k] = b - d * deep + Rad(k) * ri;
            m.Poly(bottom, d, e1, e2, m.NewChart());
        }
        else
        {
            var cap = new Vector3[sides]; for (int k = 0; k < sides; k++) cap[k] = b + Rad(k) * r;
            m.Poly(cap, d, e1, e2, m.NewChart());
        }
    }

    // 관 하나: 점 사이마다 sides각 고리를 잇는다. tip = 끝을 한 점으로 모은다, capEnd = 끝을 평평하게 막는다. 고리 방향은 평행 이동으로 비틀림 없이
    static void LpTube(LpMesh m, List<Vector3> p, List<float> rad, int sides, bool tip, bool capEnd, float splitLen = 0.3f, System.Func<float, float, float> radMul = null)
    {
        int n = p.Count;
        if (n < 2) return;
        var dirs = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var d = i == 0 ? p[1] - p[0] : i == n - 1 ? p[n - 1] - p[n - 2] : (p[i + 1] - p[i]).normalized + (p[i] - p[i - 1]).normalized;
            dirs[i] = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.up;
        }
        var refv = Mathf.Abs(dirs[0].y) < 0.9f ? Vector3.up : Vector3.right;
        var side = Vector3.Cross(dirs[0], refv).normalized;
        var sides0 = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            if (i > 0) side = Quaternion.FromToRotation(dirs[i - 1], dirs[i]) * side;
            side = (side - Vector3.Dot(side, dirs[i]) * dirs[i]).normalized;
            sides0[i] = side;
        }
        float rMax = 0; foreach (var r in rad) rMax = Mathf.Max(rMax, r);
        float circ = 2 * Mathf.PI * Mathf.Max(rMax, 0.002f);
        int chart = m.NewChart();
        float along = 0, chartStart = 0;
        int[] prev = null;
        int last = tip ? n - 1 : n;
        for (int i = 0; i < last; i++)
        {
            if (i > 0) along += (p[i] - p[i - 1]).magnitude;
            bool split = prev != null && along - chartStart > splitLen && i < last - 1;
            var up2 = Vector3.Cross(dirs[i], sides0[i]);
            var ring = new int[sides + 1];
            for (int k = 0; k <= sides; k++)
            {
                float a = 2 * Mathf.PI * k / sides;
                var nn = sides0[i] * Mathf.Cos(a) + up2 * Mathf.Sin(a);
                // radMul(각도, 길이 방향 거리): 둘레를 따라 반지름을 바꾼다 (나무껍질 골·밑동 뿌리). 법선은 둘레 방향 기울기만큼 눕힌다
                float mul = 1f, slope = 0f;
                if (radMul != null) { mul = radMul(a, along); slope = (radMul(a + 0.01f, along) - radMul(a - 0.01f, along)) / 0.02f; }
                var tang = -sides0[i] * Mathf.Sin(a) + up2 * Mathf.Cos(a);
                var nrm = radMul == null ? nn : (nn * mul - tang * slope).normalized;
                ring[k] = m.V(p[i] + nn * rad[i] * mul, nrm, new Vector2(circ * k / sides, along - chartStart), chart);
            }
            if (prev != null)
                for (int k = 0; k < sides; k++) { m.T(prev[k], prev[k + 1], ring[k + 1]); m.T(prev[k], ring[k + 1], ring[k]); }
            if (split)
            {
                // 조각을 여기서 끊는다: 같은 고리를 새 조각 좌표로 한 번 더 만든다 (긴 줄기가 한 장에 다 안 들어가서)
                chart = m.NewChart(); chartStart = along;
                for (int k = 0; k <= sides; k++)
                {
                    var src = ring[k];
                    ring[k] = m.V(m.pos[src], m.nrm[src], new Vector2(circ * k / sides, 0), chart);
                }
            }
            prev = ring;
        }
        if (tip)
        {
            along += (p[n - 1] - p[n - 2]).magnitude;
            for (int k = 0; k < sides; k++)
            {
                int t = m.V(p[n - 1], dirs[n - 1], new Vector2(circ * (k + 0.5f) / sides, along - chartStart), chart);
                m.T(prev[k], prev[k + 1], t);
            }
        }
        else if (capEnd)
        {
            var cap = new Vector3[sides];
            for (int k = 0; k < sides; k++) cap[k] = m.pos[prev[k]];
            var e1 = sides0[n - 1]; var e2 = Vector3.Cross(dirs[n - 1], e1);
            m.Poly(cap, dirs[n - 1], e1, e2, m.NewChart());
        }
    }

    // ---------------- 원본 읽기 ----------------
    class LpSource { public Vector3[] p; public Vector2[] uv; public int[] tri; public Texture2D tex; public Bounds b; }

    // 원본 FBX를 TripoSwap과 같은 방향(yaw)으로 돌린 좌표로 읽는다. 텍스처는 원본 jpg를 그대로(2048) 읽는다 (들여온 512는 읽기 금지라)
    static LpSource LpLoadSource(string id, float yaw)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{TripoModelDir}/{id}/{id}.fbx");
        var ps = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        var rot = Matrix4x4.Rotate(Quaternion.Euler(0, yaw, 0)) * Matrix4x4.Rotate(model.transform.localRotation) * model.transform.worldToLocalMatrix;
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            var mm = rot * mf.transform.localToWorldMatrix;
            var mesh = mf.sharedMesh; int baseV = ps.Count;
            foreach (var v in mesh.vertices) ps.Add(mm.MultiplyPoint3x4(v));
            uvs.AddRange(mesh.uv);
            foreach (var t in mesh.triangles) tris.Add(t + baseV);
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes($"{TripoModelDir}/{id}/{id}_diff.jpg"));
        var src = new LpSource { p = ps.ToArray(), uv = uvs.ToArray(), tri = tris.ToArray(), tex = tex };
        src.b = new Bounds(src.p[0], Vector3.zero); foreach (var v in src.p) src.b.Encapsulate(v);
        return src;
    }

    // ---------------- 나무: 원본에서 가지 뼈대 찾기 ----------------
    // 1) 같은 자리 꼭짓점 합치기 2) 덩어리마다 가장 낮은 점에서 표면 거리(다익스트라) 3) 거리를 step 간격으로 잘라, 같은 칸에서 이어진 꼭짓점 무리 = 마디
    // 4) 마디끼리 부모(낮은 칸)–자식으로 잇기 5) 가장 무거운 자식을 따라가며 가지(점열)로 나눈다. 돌려주는 것: 가지마다 점·반지름
    static List<KeyValuePair<List<Vector3>, List<float>>> LpTreeSkeleton(LpSource s, float step)
    {
        // 1) 합치기
        var map = new Dictionary<Vector3Int, int>(); var wp = new List<Vector3>(); var remap = new int[s.p.Length];
        for (int i = 0; i < s.p.Length; i++)
        {
            var key = Vector3Int.RoundToInt(s.p[i] * 100000f);
            if (!map.TryGetValue(key, out int w)) { w = wp.Count; wp.Add(s.p[i]); map[key] = w; }
            remap[i] = w;
        }
        int nv = wp.Count;
        var adj = new List<int>[nv]; for (int i = 0; i < nv; i++) adj[i] = new List<int>();
        for (int t = 0; t < s.tri.Length; t += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = remap[s.tri[t + e]], b = remap[s.tri[t + (e + 1) % 3]];
                if (a != b && !adj[a].Contains(b)) { adj[a].Add(b); adj[b].Add(a); }
            }
        // 2) 덩어리와 뿌리, 표면 거리
        var dist = new float[nv]; for (int i = 0; i < nv; i++) dist[i] = float.MaxValue;
        var comp = new int[nv]; for (int i = 0; i < nv; i++) comp[i] = -1;
        int nComp = 0;
        var rootOf = new List<int>();
        for (int i = 0; i < nv; i++)
        {
            if (comp[i] >= 0) continue;
            var q = new Stack<int>(); q.Push(i); comp[i] = nComp; int low = i;
            while (q.Count > 0) { int a = q.Pop(); if (wp[a].y < wp[low].y) low = a; foreach (var b in adj[a]) if (comp[b] < 0) { comp[b] = nComp; q.Push(b); } }
            rootOf.Add(low); nComp++;
        }
        var heap = new SortedSet<(float, int)>();
        // 뿌리: 덩어리마다 가장 낮은 점에서 1.5 cm(원본 단위) 안의 점 전부 (밑동 둘레 전체에서 출발해야 줄기 칸이 고리 모양으로 잘린다)
        for (int i = 0; i < nv; i++)
            if (wp[i].y < wp[rootOf[comp[i]]].y + 0.015f) { dist[i] = 0; heap.Add((0f, i)); }
        while (heap.Count > 0)
        {
            var top = heap.Min; heap.Remove(top);
            int a = top.Item2;
            foreach (var b in adj[a])
            {
                float nd = dist[a] + (wp[a] - wp[b]).magnitude;
                if (nd < dist[b]) { if (dist[b] < float.MaxValue) heap.Remove((dist[b], b)); dist[b] = nd; heap.Add((nd, b)); }
            }
        }
        // 3) 칸과 마디 (같은 칸에서 이어진 무리: 합집합 찾기)
        var bin = new int[nv]; for (int i = 0; i < nv; i++) bin[i] = Mathf.FloorToInt(dist[i] / step);
        var uf = new int[nv]; for (int i = 0; i < nv; i++) uf[i] = i;
        System.Func<int, int> Find = null; Find = x => { while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; } return x; };
        for (int a = 0; a < nv; a++) foreach (var b in adj[a]) if (bin[a] == bin[b]) { int ra = Find(a), rb = Find(b); if (ra != rb) uf[ra] = rb; }
        var nodeOf = new int[nv]; var nodeIdx = new Dictionary<int, int>();
        var nSum = new List<Vector3>(); var nCnt = new List<int>(); var nBin = new List<int>(); var nVerts = new List<List<int>>();
        for (int i = 0; i < nv; i++)
        {
            int r = Find(i);
            if (!nodeIdx.TryGetValue(r, out int k)) { k = nSum.Count; nodeIdx[r] = k; nSum.Add(Vector3.zero); nCnt.Add(0); nBin.Add(bin[i]); nVerts.Add(new List<int>()); }
            nodeOf[i] = k; nSum[k] += wp[i]; nCnt[k]++; nVerts[k].Add(i);
        }
        int nn = nSum.Count;
        var c = new Vector3[nn]; for (int k = 0; k < nn; k++) c[k] = nSum[k] / nCnt[k];
        // 4) 부모: 낮은 칸 마디 중 가장 높은 칸(같으면 꼭짓점이 많은 쪽)
        var parent = new int[nn]; for (int k = 0; k < nn; k++) parent[k] = -1;
        for (int a = 0; a < nv; a++)
            foreach (var b in adj[a])
            {
                int na = nodeOf[a], nb = nodeOf[b];
                if (nBin[na] >= nBin[nb]) continue; // na가 낮은 칸 → nb의 부모 후보
                int cur = parent[nb];
                if (cur < 0 || nBin[na] > nBin[cur] || (nBin[na] == nBin[cur] && nCnt[na] > nCnt[cur])) parent[nb] = na;
            }
        var children = new List<int>[nn]; for (int k = 0; k < nn; k++) children[k] = new List<int>();
        var roots = new List<int>();
        for (int k = 0; k < nn; k++) if (parent[k] >= 0) children[parent[k]].Add(k); else roots.Add(k);
        // 반지름: 마디 꼭짓점이 가지 방향에 수직으로 떨어진 평균 거리
        var rad = new float[nn];
        for (int k = 0; k < nn; k++)
        {
            Vector3 d = parent[k] >= 0 ? c[k] - c[parent[k]] : (children[k].Count > 0 ? c[children[k][0]] - c[k] : Vector3.up);
            d = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.up;
            float sum = 0; foreach (var v in nVerts[k]) { var o = wp[v] - c[k]; sum += (o - Vector3.Dot(o, d) * d).magnitude; }
            rad[k] = sum / nVerts[k].Count * 1.15f; // 각기둥은 원보다 가늘어 보이고 칸 꼭짓점이 고리 안쪽 골에도 있어, 사진으로 맞춘 배율
        }
        // 5) 무게(아래 마디 수)와 가지 나누기
        var weight = new int[nn];
        var order = new List<int>(); var st = new Stack<int>(); foreach (var r in roots) st.Push(r);
        while (st.Count > 0) { int k = st.Pop(); order.Add(k); foreach (var ch in children[k]) st.Push(ch); }
        for (int i = order.Count - 1; i >= 0; i--) { int k = order[i]; weight[k] = 1; foreach (var ch in children[k]) weight[k] += weight[ch]; }
        var result = new List<KeyValuePair<List<Vector3>, List<float>>>();
        var work = new Stack<(int from, int node)>(); foreach (var r in roots) work.Push((-1, r));
        while (work.Count > 0)
        {
            var (from, node) = work.Pop();
            var pts = new List<Vector3>(); var rs = new List<float>();
            if (from >= 0) { pts.Add(c[from]); rs.Add(rad[node]); }
            int cur = node;
            while (true)
            {
                pts.Add(c[cur]); rs.Add(rad[cur]);
                if (children[cur].Count == 0) break;
                int best = children[cur][0]; foreach (var ch in children[cur]) if (weight[ch] > weight[best]) best = ch;
                foreach (var ch in children[cur]) if (ch != best) work.Push((cur, ch));
                cur = best;
            }
            result.Add(new KeyValuePair<List<Vector3>, List<float>>(pts, rs));
        }
        return result;
    }

    // 점열 줄이기 (더글러스–포이커, 3D). 반지름은 남는 점의 값을 그대로
    static void LpSimplify(List<Vector3> p, List<float> r, float tol, out List<Vector3> op, out List<float> or)
    {
        var keep = new bool[p.Count]; keep[0] = keep[p.Count - 1] = true;
        var st = new Stack<(int, int)>(); st.Push((0, p.Count - 1));
        while (st.Count > 0)
        {
            var (a, b) = st.Pop();
            float best = 0; int bi = -1;
            for (int i = a + 1; i < b; i++)
            {
                var ab = p[b] - p[a]; float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p[i] - p[a], ab) / ab.sqrMagnitude) : 0;
                float d = (p[i] - (p[a] + ab * t)).magnitude;
                // 굵기가 크게 바뀌는 점도 남긴다
                float rr = r[a] + (r[b] - r[a]) * t; d = Mathf.Max(d, Mathf.Abs(r[i] - rr));
                if (d > best) { best = d; bi = i; }
            }
            if (bi >= 0 && best > tol) { keep[bi] = true; st.Push((a, bi)); st.Push((bi, b)); }
        }
        op = new List<Vector3>(); or = new List<float>();
        for (int i = 0; i < p.Count; i++) if (keep[i]) { op.Add(p[i]); or.Add(r[i]); }
    }

    static int LpSidesFor(float r) => r > 0.015f ? 7 : r > 0.007f ? 5 : r > 0.003f ? 4 : 3;

    // 나무 메시 짜기(단계별)는 GameSceneBuilder.TripoTree.cs의 LpBuildTreeTier

    // ---------------- UV 펼치기 ----------------
    // 조각마다 2D 상자를 재서 긴 변을 가로로 눕히고, 한 장(size)에 선반식으로 늘어놓는다. 배율은 다 들어가는 가장 큰 값 (이분 탐색)
    static Vector2[] LpPackUv(LpMesh m, int size, int pad)
    {
        int nc = m.charts;
        var mn = new Vector2[nc]; var mx = new Vector2[nc]; var has = new bool[nc];
        for (int i = 0; i < m.pos.Count; i++)
        {
            int c = m.chart[i]; var f = m.flat[i];
            if (!has[c]) { mn[c] = mx[c] = f; has[c] = true; } else { mn[c] = Vector2.Min(mn[c], f); mx[c] = Vector2.Max(mx[c], f); }
        }
        var rot = new bool[nc]; var w = new float[nc]; var hgt = new float[nc];
        for (int c = 0; c < nc; c++)
        {
            var d = m.solid.ContainsKey(c) ? Vector2.zero : mx[c] - mn[c]; // 한 색 조각은 점 하나(둘레 pad 칸)만 차지한다
            rot[c] = d.y > d.x; w[c] = rot[c] ? d.y : d.x; hgt[c] = rot[c] ? d.x : d.y;
        }
        var ord = new List<int>(); for (int c = 0; c < nc; c++) if (has[c]) ord.Add(c);
        ord.Sort((a, b) => hgt[b].CompareTo(hgt[a]));
        var ox = new int[nc]; var oy = new int[nc];
        System.Func<float, bool> Fits = sc =>
        {
            int x = 0, y = 0, rowH = 0;
            foreach (var c in ord)
            {
                int cw = Mathf.CeilToInt(w[c] * sc) + pad * 2, ch = Mathf.CeilToInt(hgt[c] * sc) + pad * 2;
                if (cw > size) return false;
                if (x + cw > size) { x = 0; y += rowH; rowH = 0; }
                ox[c] = x; oy[c] = y; x += cw; rowH = Mathf.Max(rowH, ch);
                if (y + rowH > size) return false;
            }
            return true;
        };
        float lo = 1, hi = 100000;
        for (int it = 0; it < 40; it++) { float mid = (lo + hi) * 0.5f; if (Fits(mid)) lo = mid; else hi = mid; }
        Fits(lo);
        var uv = new Vector2[m.pos.Count];
        for (int i = 0; i < m.pos.Count; i++)
        {
            int c = m.chart[i]; var f = m.solid.ContainsKey(c) ? Vector2.zero : m.flat[i] - mn[c];
            if (rot[c]) f = new Vector2(f.y, f.x);
            uv[i] = new Vector2((ox[c] + pad + f.x * lo) / size, (oy[c] + pad + f.y * lo) / size);
        }
        return uv;
    }

    // ---------------- 굽기 ----------------
    // 새 메시 텍셀마다: 그 자리·법선에서 원본 표면의 가장 가까운 점(법선이 반대인 면은 뺀다)을 찾고 원본 텍스처 색을 옮긴다. 빈 곳은 이웃 색으로 메운다
    static Color32[] LpBake(LpMesh m, Vector2[] uv, LpSource s, int size, float maxR)
    {
        // 원본 삼각형을 격자 칸에 넣기
        float cell = Mathf.Max(s.b.size.x, Mathf.Max(s.b.size.y, s.b.size.z)) / 48f;
        var grid = new Dictionary<Vector3Int, List<int>>();
        int nt = s.tri.Length / 3;
        var tn = new Vector3[nt];
        for (int t = 0; t < nt; t++)
        {
            Vector3 a = s.p[s.tri[t * 3]], b = s.p[s.tri[t * 3 + 1]], c = s.p[s.tri[t * 3 + 2]];
            tn[t] = Vector3.Cross(b - a, c - a).normalized;
            var lo = Vector3Int.FloorToInt(Vector3.Min(a, Vector3.Min(b, c)) / cell); var hi = Vector3Int.FloorToInt(Vector3.Max(a, Vector3.Max(b, c)) / cell);
            for (int x = lo.x; x <= hi.x; x++) for (int y = lo.y; y <= hi.y; y++) for (int z = lo.z; z <= hi.z; z++)
            {
                var k = new Vector3Int(x, y, z);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                l.Add(t);
            }
        }
        var stamp = new int[nt]; int stampId = 0;
        var px = new Color32[size * size]; var filled = new bool[size * size];
        for (int t = 0; t < m.tri.Count; t += 3)
        {
            int i0 = m.tri[t], i1 = m.tri[t + 1], i2 = m.tri[t + 2];
            if (m.solid.TryGetValue(m.chart[i0], out var scol))
            {
                // 한 색 조각: 세 꼭짓점이 같은 UV 점이다. 그 둘레 4×4 칸을 칠한다
                int cx = Mathf.FloorToInt(uv[i0].x * size), cy = Mathf.FloorToInt(uv[i0].y * size);
                for (int yy = cy - 2; yy <= cy + 1; yy++) for (int xx = cx - 2; xx <= cx + 1; xx++)
                {
                    if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                    px[yy * size + xx] = scol; filled[yy * size + xx] = true;
                }
                continue;
            }
            Vector2 a = uv[i0] * size, b = uv[i1] * size, c = uv[i2] * size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(den) < 1e-9f) continue;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var q = new Vector2(x + 0.5f, y + 0.5f);
                    float wa = ((b.y - c.y) * (q.x - c.x) + (c.x - b.x) * (q.y - c.y)) / den;
                    float wb = ((c.y - a.y) * (q.x - c.x) + (a.x - c.x) * (q.y - c.y)) / den;
                    float wc = 1 - wa - wb;
                    const float e = -0.02f;
                    if (wa < e || wb < e || wc < e) continue;
                    var P = m.pos[i0] * wa + m.pos[i1] * wb + m.pos[i2] * wc;
                    var N = (m.nrm[i0] * wa + m.nrm[i1] * wb + m.nrm[i2] * wc).normalized;
                    // 먼저 법선 방향 선으로 비춰 본다: P를 지나는 선(바깥 maxR ~ 안쪽 maxR)이 원본 면(이쪽을 보는 면)과 만나는 점 중 P에 가장 가까운 것.
                    // 바깥에서 본 모습 그대로라 팬처럼 겹겹인 곳도 깨지지 않는다. 못 만나면 가장 가까운 점으로
                    float bestD = float.MaxValue; int bestT = -1; Vector3 bestQ = Vector3.zero;
                    {
                        stampId++;
                        var O = P + N * maxR;
                        int steps = Mathf.CeilToInt(2 * maxR / (cell * 0.5f));
                        for (int si = 0; si <= steps; si++)
                        {
                            var sp = Vector3Int.FloorToInt((O - N * (2 * maxR * si / steps)) / cell);
                            for (int gx = sp.x - 1; gx <= sp.x + 1; gx++) for (int gy = sp.y - 1; gy <= sp.y + 1; gy++) for (int gz = sp.z - 1; gz <= sp.z + 1; gz++)
                            {
                                if (!grid.TryGetValue(new Vector3Int(gx, gy, gz), out var l)) continue;
                                foreach (var st in l)
                                {
                                    if (stamp[st] == stampId) continue; stamp[st] = stampId;
                                    if (Vector3.Dot(tn[st], N) < 0.05f) continue;
                                    if (!LpRayTri(O, -N, s.p[s.tri[st * 3]], s.p[s.tri[st * 3 + 1]], s.p[s.tri[st * 3 + 2]], out float tt)) continue;
                                    if (tt < 0 || tt > 2 * maxR) continue;
                                    float d = Mathf.Abs(tt - maxR);
                                    if (d < bestD) { bestD = d; bestT = st; bestQ = O - N * tt; }
                                }
                            }
                        }
                    }
                    bestD = float.MaxValue; // 아래 가까운 점 찾기(선이 못 만났을 때만 돈다)는 거리 제곱을 쓴다
                    for (float rr = cell * 0.5f; rr <= maxR && bestT < 0; rr *= 2f)
                    {
                        stampId++;
                        var lo = Vector3Int.FloorToInt((P - Vector3.one * rr) / cell); var hi = Vector3Int.FloorToInt((P + Vector3.one * rr) / cell);
                        for (int gx = lo.x; gx <= hi.x; gx++) for (int gy = lo.y; gy <= hi.y; gy++) for (int gz = lo.z; gz <= hi.z; gz++)
                        {
                            if (!grid.TryGetValue(new Vector3Int(gx, gy, gz), out var l)) continue;
                            foreach (var st in l)
                            {
                                if (stamp[st] == stampId) continue; stamp[st] = stampId;
                                if (Vector3.Dot(tn[st], N) < -0.2f) continue;
                                var cp = LpClosest(P, s.p[s.tri[st * 3]], s.p[s.tri[st * 3 + 1]], s.p[s.tri[st * 3 + 2]]);
                                float d = (cp - P).sqrMagnitude;
                                if (d < bestD && d <= rr * rr) { bestD = d; bestT = st; bestQ = cp; }
                            }
                        }
                    }
                    if (bestT < 0) continue;
                    // 원본 삼각형 안 무게중심 좌표 → 원본 UV → 색
                    Vector3 A = s.p[s.tri[bestT * 3]], B = s.p[s.tri[bestT * 3 + 1]], C = s.p[s.tri[bestT * 3 + 2]];
                    var bc = LpBary(bestQ, A, B, C);
                    var suv = s.uv[s.tri[bestT * 3]] * bc.x + s.uv[s.tri[bestT * 3 + 1]] * bc.y + s.uv[s.tri[bestT * 3 + 2]] * bc.z;
                    px[y * size + x] = s.tex.GetPixelBilinear(suv.x, suv.y);
                    filled[y * size + x] = true;
                }
        }
        // 빈 텍셀 메우기: 이웃 평균으로 몇 번 번지고, 남은 곳은 전체 평균색 (밉맵에서 검은 테두리가 번지지 않게)
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
        var avg = sn > 0 ? new Color32((byte)(sr / sn), (byte)(sg / sn), (byte)(sb / sn), 255) : new Color32(128, 128, 128, 255);
        for (int i = 0; i < px.Length; i++) { if (!filled[i]) px[i] = avg; px[i].a = 255; }
        return px;
    }

    static Vector3 LpClosest(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        // Ericson, Real-Time Collision Detection 5.1.5
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;
        Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        float den = 1f / (va + vb + vc);
        return a + ab * (vb * den) + ac * (vc * den);
    }

    // 선–삼각형 만남 (묄러–트럼보어, 양면). t = 원점에서 거리 (dir은 단위 벡터)
    static bool LpRayTri(Vector3 o, Vector3 dir, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0;
        Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(dir, e2);
        float det = Vector3.Dot(e1, pv);
        if (Mathf.Abs(det) < 1e-12f) return false;
        float inv = 1f / det; Vector3 tv = o - a;
        float u = Vector3.Dot(tv, pv) * inv; if (u < 0 || u > 1) return false;
        Vector3 qv = Vector3.Cross(tv, e1);
        float v = Vector3.Dot(dir, qv) * inv; if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(e2, qv) * inv;
        return true;
    }

    static Vector3 LpBary(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
        float den = d00 * d11 - d01 * d01;
        if (Mathf.Abs(den) < 1e-14f) return new Vector3(1, 0, 0);
        float v = (d11 * d20 - d01 * d21) / den, w = (d00 * d21 - d01 * d20) / den;
        return new Vector3(1 - v - w, v, w);
    }

    // ---------------- 저장 ----------------
    static string LpSave(string id, LpMesh m, Vector2[] uv, Color32[] px, int size, string suf = "_lp", string mipGroup = "", string sub = null)
    {
        // sub: 품질별 하위 폴더 (LpTierDir). 비면 모델 폴더 바로 아래
        string dir = $"{TripoModelDir}/{id}";
        if (!string.IsNullOrEmpty(sub))
        {
            if (!AssetDatabase.IsValidFolder($"{dir}/{sub}")) AssetDatabase.CreateFolder(dir, sub);
            dir = $"{dir}/{sub}";
        }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{dir}/{id}{suf}.asset");
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = id + suf;
        mesh.SetVertices(m.pos); mesh.SetNormals(m.nrm); mesh.SetUVs(0, uv); mesh.SetTriangles(m.tri, 0);
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, $"{dir}/{id}{suf}.asset");
        else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(px); tex.Apply();
        string texPath = $"{dir}/{id}{suf}_diff.png";
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        // 들여오기 크기 = 구운 크기. mipGroup "HighRes"면 GameSettings가 '매우 높음'에서만 원래 크기로, 그 밖에는 반으로 쓴다
        if (ti != null && (ti.maxTextureSize != size || !ti.sRGBTexture || !ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Compressed || ti.alphaSource != TextureImporterAlphaSource.None || ti.mipmapLimitGroupName != mipGroup))
        {
            ti.maxTextureSize = size; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed;
            ti.alphaSource = TextureImporterAlphaSource.None; ti.mipmapLimitGroupName = mipGroup;
            ti.SaveAndReimport();
        }

        var go = new GameObject(id + suf);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>();
        PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{id}{suf}.prefab");
        Object.DestroyImmediate(go);
        return $"{id}{suf}: 삼각형 {m.tri.Count / 3}, 꼭짓점 {m.pos.Count}, 조각 {m.charts}";
    }

    // 품질별 판 (11-22 사용자 "낮음부터 매우 높음 옵션까지 품질을 확연하게 분리"): 실외기 _q0 낮음 · _q1 중간 · _q2 높음 · _q3 매우 높음.
    // 도시에서는 품질 설정이 쓸 수 있는 가장 좋은 판을 정하고(QualitySettings.maximumLODLevel), 멀리 있는 것은 아래 판으로 내려간다
    [MenuItem("Tools/핵폭발 생존/Tripo 품질별 판 만들기 (실외기·죽은 나무)")]
    static void LpBuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[GameSceneBuilder] " + LpMakeAcTier(0) + " / " + LpMakeAcTier(1) + " / " + LpMakeAcTier(2) + " / " + LpMakeAcTier(3) + " / " + LpMakeTreeTier(0) + " / " + LpMakeTreeTier(1) + " / " + LpMakeTreeTier(2) + " / " + LpMakeTreeTier(3));
    }

    // 품질별 결과 폴더 (11-22 사용자 "작업물 폴더도 낮음-중간-높음-매우높음 다 나누어서"). 번호 = 코드의 q = 파일 이름의 _q번호
    static readonly string[] LpTierDir = { "Q0_낮음", "Q1_중간", "Q2_높음", "Q3_매우높음" };

    static string LpMakeAcTier(int q)
    {
        var src = LpLoadSource("ac_unit_01", TripoSpecs["ac_unit_01"].yaw);
        var m = q == 0 ? LpBuildAcSimple() : q == 1 ? LpBuildAc() : HqBuildAc(q == 3);
        int size = q == 0 ? 256 : q == 3 ? 1024 : 512;
        var uv = LpPackUv(m, size, q == 0 ? 3 : 4);
        var px = LpBake(m, uv, src, size, 0.08f);
        Object.DestroyImmediate(src.tex);
        return LpSave("ac_unit_01", m, uv, px, size, "_q" + q, q == 3 ? "HighRes" : "", LpTierDir[q]);
    }
}
