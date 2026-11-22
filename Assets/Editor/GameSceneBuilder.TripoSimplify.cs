using System.Collections.Generic;
using UnityEngine;

// 텍스처 이음매를 지키는 면 줄이기 (2026-11-23, Tripo 소품 5종 품질별 판용). Unity 6 메시 LOD 생성기는 UV 이음매를 넘어 꼭짓점을 합쳐
// 낮은 단계에서 텍스처가 찢어지고 조각이 튀어나왔다 (Docs/맵모델링_미리보기/09_Tripo5_품질4단계/_시험1).
// 방법: Garland–Heckbert 오차 이차식(QEM) + 한쪽 끝으로 합치기(half-edge collapse: 새 점을 만들지 않아 UV·법선이 원본 값 그대로).
//  - 같은 자리 꼭짓점을 '점'으로 묶고, 각 꼭짓점(UV가 다른 쪽)은 그 점의 '쐐기'로 본다.
//  - u를 v로 합칠 때 u의 쐐기마다 v에서 같은 UV 조각의 쐐기가 정확히 하나 있어야 한다 (이음매는 이음매를 따라서만 줄어든다).
//  - 열린 가장자리의 점은 그 가장자리를 따라서만, 면이 뒤집히거나(법선) 텍스처가 뒤집히면(UV 넓이 부호) 합치지 않는다, 위상 검사(link condition).
//  - 가장자리·이음매에는 수직 평면을 더해 모양이 안쪽으로 먹히지 않게 한다.
// 법선은 합친 뒤 원래 법선과 60° 안쪽 면들로 다시 평균 낸다 (먼 단계에서 어두운 얼룩이 생기지 않게).
public static partial class GameSceneBuilder
{
    struct TtHeapItem { public double cost; public int u, v, su, sv; }

    class TtHeap
    {
        readonly List<TtHeapItem> a = new List<TtHeapItem>();
        public int Count => a.Count;
        public void Push(TtHeapItem x)
        {
            a.Add(x); int i = a.Count - 1;
            while (i > 0) { int p = (i - 1) >> 1; if (a[p].cost <= a[i].cost) break; var t = a[p]; a[p] = a[i]; a[i] = t; i = p; }
        }
        public TtHeapItem Pop()
        {
            var top = a[0]; int last = a.Count - 1; a[0] = a[last]; a.RemoveAt(last);
            int i = 0, n = a.Count;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < n && a[l].cost < a[m].cost) m = l;
                if (r < n && a[r].cost < a[m].cost) m = r;
                if (m == i) break;
                var t = a[m]; a[m] = a[i]; a[i] = t; i = m;
            }
            return top;
        }
    }

    class TtSimplifier
    {
        public int[] P;           // 쐐기(원본 꼭짓점) → 점
        Vector3[] pp;             // 점 자리
        Vector2[] uv;             // 쐐기 UV
        public int[] tw;          // 삼각형 쐐기 3개씩
        public bool[] deadT;
        bool[] deadP, bndP;
        List<int>[] adj;          // 점 → 살아 있는 삼각형
        double[] Q;               // 점마다 이차식 10개
        int[] stamp;
        public int alive;
        readonly TtHeap heap = new TtHeap();
        public float boundaryWeight = 20f, seamWeight = 5f;
        // true: 합친 점을 오차가 가장 작은 자리로 옮긴다 (UV가 없는 '모양만 줄이기'용. 아주 적게 줄일 때 원통·상자 모양이 덜 무너진다)
        public bool freePlace;

        public TtSimplifier(Vector3[] vpos, Vector2[] vuv, int[] src, bool freePlace = false)
        {
            this.freePlace = freePlace;
            if (freePlace) boundaryWeight = 200f; // 점을 옮길 때는 열린 가장자리를 더 단단히 잡는다 (소화전 마개·차 앞 모서리에 틈이 벌어졌다)
            int W = vpos.Length; uv = vuv;
            P = new int[W];
            var map = new Dictionary<Vector3Int, int>(); var pl = new List<Vector3>();
            for (int w = 0; w < W; w++)
            {
                var key = Vector3Int.RoundToInt(vpos[w] * 100000f);
                if (!map.TryGetValue(key, out int p)) { p = pl.Count; pl.Add(vpos[w]); map[key] = p; }
                P[w] = p;
            }
            pp = pl.ToArray(); int NP = pp.Length;
            var tl = new List<int>();
            for (int t = 0; t < src.Length; t += 3)
            {
                int a = src[t], b = src[t + 1], c = src[t + 2];
                if (P[a] == P[b] || P[b] == P[c] || P[a] == P[c]) continue;
                tl.Add(a); tl.Add(b); tl.Add(c);
            }
            tw = tl.ToArray(); int T = tw.Length / 3; alive = T;
            deadT = new bool[T]; deadP = new bool[NP]; bndP = new bool[NP]; stamp = new int[NP];
            adj = new List<int>[NP]; for (int i = 0; i < NP; i++) adj[i] = new List<int>(6);
            for (int t = 0; t < T; t++) for (int k = 0; k < 3; k++) adj[P[tw[3 * t + k]]].Add(t);
            Q = new double[NP * 10];

            // 면 평면 (넓이 가중)
            var edgeFaces = new Dictionary<long, List<int>>();
            for (int t = 0; t < T; t++)
            {
                Vector3 a = pp[P[tw[3 * t]]], b = pp[P[tw[3 * t + 1]]], c = pp[P[tw[3 * t + 2]]];
                var n = Vector3.Cross(b - a, c - a); float area2 = n.magnitude;
                if (area2 < 1e-12f) continue;
                n /= area2;
                for (int k = 0; k < 3; k++) AddPlane(P[tw[3 * t + k]], n, -Vector3.Dot(n, a), area2 * 0.5);
                for (int k = 0; k < 3; k++)
                {
                    int p0 = P[tw[3 * t + k]], p1 = P[tw[3 * t + (k + 1) % 3]];
                    long key = EKey(p0, p1);
                    if (!edgeFaces.TryGetValue(key, out var lst)) { lst = new List<int>(2); edgeFaces[key] = lst; }
                    lst.Add(t);
                }
            }
            // 가장자리(면 하나)와 이음매(양쪽 면의 쐐기가 다름)에 수직 평면
            foreach (var kv in edgeFaces)
            {
                int p0 = (int)(kv.Key >> 32), p1 = (int)(kv.Key & 0xffffffff);
                var fs = kv.Value;
                bool boundary = fs.Count == 1, seam = false;
                if (fs.Count == 2)
                    seam = WedgeOf(fs[0], p0) != WedgeOf(fs[1], p0) || WedgeOf(fs[0], p1) != WedgeOf(fs[1], p1);
                if (boundary) { bndP[p0] = true; bndP[p1] = true; }
                if (!boundary && !seam) continue;
                var e = pp[p1] - pp[p0]; float len2 = e.sqrMagnitude; if (len2 < 1e-14f) continue;
                foreach (int t in fs)
                {
                    Vector3 a = pp[P[tw[3 * t]]], b = pp[P[tw[3 * t + 1]]], c = pp[P[tw[3 * t + 2]]];
                    var fn = Vector3.Cross(b - a, c - a).normalized;
                    var cn = Vector3.Cross(e, fn).normalized;
                    double wgt = (boundary ? boundaryWeight : seamWeight) * len2;
                    AddPlane(p0, cn, -Vector3.Dot(cn, pp[p0]), wgt);
                    AddPlane(p1, cn, -Vector3.Dot(cn, pp[p0]), wgt);
                }
            }
            foreach (var kv in edgeFaces)
            {
                int p0 = (int)(kv.Key >> 32), p1 = (int)(kv.Key & 0xffffffff);
                PushBest(p0, p1);
            }
        }

        static long EKey(int a, int b) { if (a > b) { int t = a; a = b; b = t; } return ((long)a << 32) | (uint)b; }

        void AddPlane(int p, Vector3 n, float d, double w)
        {
            double a = n.x, b = n.y, c = n.z, dd = d; int o = p * 10;
            Q[o] += w * a * a; Q[o + 1] += w * a * b; Q[o + 2] += w * a * c; Q[o + 3] += w * a * dd;
            Q[o + 4] += w * b * b; Q[o + 5] += w * b * c; Q[o + 6] += w * b * dd;
            Q[o + 7] += w * c * c; Q[o + 8] += w * c * dd; Q[o + 9] += w * dd * dd;
        }

        double Err(int pu, int pv, Vector3 x)
        {
            int a = pu * 10, b = pv * 10;
            double X = x.x, Y = x.y, Z = x.z;
            double q0 = Q[a] + Q[b], q1 = Q[a + 1] + Q[b + 1], q2 = Q[a + 2] + Q[b + 2], q3 = Q[a + 3] + Q[b + 3], q4 = Q[a + 4] + Q[b + 4];
            double q5 = Q[a + 5] + Q[b + 5], q6 = Q[a + 6] + Q[b + 6], q7 = Q[a + 7] + Q[b + 7], q8 = Q[a + 8] + Q[b + 8], q9 = Q[a + 9] + Q[b + 9];
            return q0 * X * X + 2 * q1 * X * Y + 2 * q2 * X * Z + 2 * q3 * X + q4 * Y * Y + 2 * q5 * Y * Z + 2 * q6 * Y + q7 * Z * Z + 2 * q8 * Z + q9;
        }

        // 두 점 이차식의 합이 가장 작은 자리 (3×3 풀기). 풀리지 않거나 너무 멀면 두 끝·가운데 중 가장 작은 곳
        Vector3 Place(int u, int v)
        {
            int a = u * 10, b = v * 10;
            double q0 = Q[a] + Q[b], q1 = Q[a + 1] + Q[b + 1], q2 = Q[a + 2] + Q[b + 2], q3 = Q[a + 3] + Q[b + 3], q4 = Q[a + 4] + Q[b + 4];
            double q5 = Q[a + 5] + Q[b + 5], q6 = Q[a + 6] + Q[b + 6], q7 = Q[a + 7] + Q[b + 7], q8 = Q[a + 8] + Q[b + 8];
            double det = q0 * (q4 * q7 - q5 * q5) - q1 * (q1 * q7 - q5 * q2) + q2 * (q1 * q5 - q4 * q2);
            Vector3 pu = pp[u], pv = pp[v], mid = (pu + pv) * 0.5f;
            float len = (pu - pv).magnitude;
            if (System.Math.Abs(det) > 1e-18)
            {
                double bx = -q3, by = -q6, bz = -q8;
                double x = (bx * (q4 * q7 - q5 * q5) - q1 * (by * q7 - q5 * bz) + q2 * (by * q5 - q4 * bz)) / det;
                double y = (q0 * (by * q7 - q5 * bz) - bx * (q1 * q7 - q5 * q2) + q2 * (q1 * bz - by * q2)) / det;
                double z = (q0 * (q4 * bz - by * q5) - q1 * (q1 * bz - by * q2) + bx * (q1 * q5 - q4 * q2)) / det;
                var r = new Vector3((float)x, (float)y, (float)z);
                if ((r - mid).magnitude <= len * 1.5f) return r;
            }
            Vector3 best = pv; double be = Err(u, v, pv);
            double eu = Err(u, v, pu); if (eu < be) { be = eu; best = pu; }
            double em = Err(u, v, mid); if (em < be) { best = mid; }
            return best;
        }

        // 삼각형 품질 (2√3·넓이×2 / 변 길이 제곱 합, 정삼각형 1). 합친 뒤 0.03보다 가늘고 원래보다 나빠지면 막는다 (바늘 같은 면이 낮은 판 모양을 망쳤다)
        static float Qual(Vector3 a, Vector3 b, Vector3 c, float area2)
        {
            float den = (b - a).sqrMagnitude + (c - b).sqrMagnitude + (a - c).sqrMagnitude;
            return den < 1e-20f ? 0f : 3.4641f * area2 / den;
        }

        int WedgeOf(int t, int p)
        {
            for (int k = 0; k < 3; k++) if (P[tw[3 * t + k]] == p) return tw[3 * t + k];
            return -1;
        }

        bool Has(int t, int p) => P[tw[3 * t]] == p || P[tw[3 * t + 1]] == p || P[tw[3 * t + 2]] == p;

        void PushBest(int a, int b)
        {
            double c1 = Eval(a, b, false), c2 = Eval(b, a, false);
            if (c1 < double.MaxValue) heap.Push(new TtHeapItem { cost = c1, u = a, v = b, su = stamp[a], sv = stamp[b] });
            if (c2 < double.MaxValue) heap.Push(new TtHeapItem { cost = c2, u = b, v = a, su = stamp[b], sv = stamp[a] });
        }

        readonly List<int> shared = new List<int>(4), survive = new List<int>(16);
        readonly Dictionary<int, int> wm = new Dictionary<int, int>();
        readonly HashSet<int> nu = new HashSet<int>(), nv = new HashSet<int>(), opp = new HashSet<int>();

        // u를 v로 합칠 때의 비용 (안 되면 MaxValue). apply면 실제로 합친다
        double Eval(int u, int v, bool apply)
        {
            if (u == v || deadP[u] || deadP[v]) return double.MaxValue;
            shared.Clear(); survive.Clear();
            foreach (int t in adj[u]) { if (Has(t, v)) shared.Add(t); else survive.Add(t); }
            if (shared.Count == 0) return double.MaxValue;
            if (bndP[u] && shared.Count != 1) return double.MaxValue;   // 가장자리 점은 가장자리를 따라서만
            if (shared.Count > 2) return double.MaxValue;               // 겹친 면(비다양체)
            // 위상 검사: u·v 공통 이웃 = 지워질 면의 맞은편 점들
            nu.Clear(); nv.Clear(); opp.Clear();
            foreach (int t in adj[u]) for (int k = 0; k < 3; k++) nu.Add(P[tw[3 * t + k]]);
            foreach (int t in adj[v]) for (int k = 0; k < 3; k++) nv.Add(P[tw[3 * t + k]]);
            foreach (int t in shared) for (int k = 0; k < 3; k++) { int p = P[tw[3 * t + k]]; if (p != u && p != v) opp.Add(p); }
            foreach (int p in nu) if (p != u && p != v && nv.Contains(p) && !opp.Contains(p)) return double.MaxValue;
            // 쐐기 짝 (같은 UV 조각)
            wm.Clear();
            foreach (int t in shared)
            {
                int wu = WedgeOf(t, u), wv = WedgeOf(t, v);
                if (wm.TryGetValue(wu, out int old)) { if (old != wv) return double.MaxValue; }
                else wm[wu] = wv;
            }
            foreach (int t in survive) if (!wm.ContainsKey(WedgeOf(t, u))) return double.MaxValue;
            // 면·텍스처 뒤집힘
            Vector3 pv = freePlace ? Place(u, v) : pp[v];
            if (freePlace)
                foreach (int t in adj[v])
                {
                    if (shared.Contains(t)) continue;
                    Vector3 a = pp[P[tw[3 * t]]], b = pp[P[tw[3 * t + 1]]], c = pp[P[tw[3 * t + 2]]];
                    var no = Vector3.Cross(b - a, c - a); float qo = Qual(a, b, c, no.magnitude);
                    if (P[tw[3 * t]] == v) a = pv; else if (P[tw[3 * t + 1]] == v) b = pv; else c = pv;
                    var nn = Vector3.Cross(b - a, c - a);
                    float mo = no.magnitude, mn = nn.magnitude;
                    if (mn < 1e-10f) return double.MaxValue;
                    if (mo > 1e-12f && Vector3.Dot(no, nn) < 0.2f * mo * mn) return double.MaxValue;
                { float qn = Qual(a, b, c, mn); if (qn < 0.03f && qn < qo) return double.MaxValue; }
                }
            foreach (int t in survive)
            {
                int w0 = tw[3 * t], w1 = tw[3 * t + 1], w2 = tw[3 * t + 2];
                // 합친 뒤 같은 세 점의 면이 이미 있으면 막는다 (사면체 모양 주머니가 등을 맞댄 두 면으로 남았다 — 검수 11-23)
                int pa = P[w0] == u ? v : P[w0], pb = P[w1] == u ? v : P[w1], pc = P[w2] == u ? v : P[w2];
                foreach (int t2 in adj[v])
                {
                    if (shared.Contains(t2)) continue;
                    int qa = P[tw[3 * t2]], qb = P[tw[3 * t2 + 1]], qc = P[tw[3 * t2 + 2]];
                    if ((qa == pa || qa == pb || qa == pc) && (qb == pa || qb == pb || qb == pc) && (qc == pa || qc == pb || qc == pc)) return double.MaxValue;
                }
                Vector3 a = pp[P[w0]], b = pp[P[w1]], c = pp[P[w2]];
                var no = Vector3.Cross(b - a, c - a); float qo = Qual(a, b, c, no.magnitude);
                if (P[w0] == u) a = pv; else if (P[w1] == u) b = pv; else c = pv;
                var nn = Vector3.Cross(b - a, c - a);
                float mo = no.magnitude, mn = nn.magnitude;
                if (mn < 1e-10f) return double.MaxValue;
                if (mo > 1e-12f && Vector3.Dot(no, nn) < 0.2f * mo * mn) return double.MaxValue;
                { float qn = Qual(a, b, c, mn); if (qn < 0.03f && qn < qo) return double.MaxValue; }
                if (freePlace) continue;
                // UV
                Vector2 ua = uv[w0], ub = uv[w1], uc = uv[w2];
                float ao = (ub.x - ua.x) * (uc.y - ua.y) - (ub.y - ua.y) * (uc.x - ua.x);
                int wu = P[w0] == u ? w0 : P[w1] == u ? w1 : w2;
                Vector2 un = uv[wm[wu]];
                if (wu == w0) ua = un; else if (wu == w1) ub = un; else uc = un;
                float an = (ub.x - ua.x) * (uc.y - ua.y) - (ub.y - ua.y) * (uc.x - ua.x);
                if (Mathf.Abs(ao) > 1e-9f && ao * an <= 0f) return double.MaxValue;
            }
            double cost = Err(u, v, pv);
            if (!apply) return cost;

            // 합치기
            foreach (int t in shared)
            {
                deadT[t] = true; alive--;
                for (int k = 0; k < 3; k++) { int p = P[tw[3 * t + k]]; if (p != u) adj[p].Remove(t); }
            }
            foreach (int t in survive)
            {
                for (int k = 0; k < 3; k++) if (P[tw[3 * t + k]] == u) tw[3 * t + k] = wm[tw[3 * t + k]];
                adj[v].Add(t);
            }
            adj[u].Clear(); deadP[u] = true; bndP[v] |= bndP[u];
            if (freePlace) pp[v] = pv;
            for (int i = 0; i < 10; i++) Q[v * 10 + i] += Q[u * 10 + i];
            stamp[v]++;
            var ring = new HashSet<int>(); // nv 등은 PushBest → Eval이 다시 쓰므로 따로
            foreach (int t in adj[v]) for (int k = 0; k < 3; k++) ring.Add(P[tw[3 * t + k]]);
            foreach (int p in ring) if (p != v) PushBest(p, v);
            return cost;
        }

        public void Run(int target)
        {
            // 한 번 막힌 모서리는 끝점이 다시 합쳐질 때만 다시 들어오므로, 더미가 비면 살아 있는 모서리 전부로 다시 채워 본다 (최대 3번, 줄어들지 않으면 그만)
            int reseeds = 0, lastAlive = -1;
            while (alive > target)
            {
                if (heap.Count == 0)
                {
                    if (reseeds++ >= 3 || alive == lastAlive) break;
                    lastAlive = alive;
                    for (int t = 0; t < deadT.Length; t++)
                    {
                        if (deadT[t]) continue;
                        for (int k = 0; k < 3; k++) { int p0 = P[tw[3 * t + k]], p1 = P[tw[3 * t + (k + 1) % 3]]; PushBest(p0, p1); }
                    }
                    if (heap.Count == 0) break;
                }
                var it = heap.Pop();
                if (deadP[it.u] || deadP[it.v] || stamp[it.u] != it.su || stamp[it.v] != it.sv) continue;
                Eval(it.u, it.v, true);
            }
        }

        public Vector3 Pos(int p) => pp[p];

        // 살아 있는 면 (원본 쐐기 번호)
        public int[] Triangles()
        {
            var r = new List<int>(alive * 3);
            for (int t = 0; t < deadT.Length; t++) if (!deadT[t]) { r.Add(tw[3 * t]); r.Add(tw[3 * t + 1]); r.Add(tw[3 * t + 2]); }
            return r.ToArray();
        }

        // 쐐기 법선 다시 내기: 이 쐐기(꼭짓점)를 쓰는 줄인 면들의 평균을 기준으로, 그 점의 면 중 기준과 35° 안인 면을 모서리 각으로 가중 평균.
        // 원래 법선(n0)을 기준으로 하면 테두리처럼 원래 법선이 기운 곳에서 기운 법선이 긴 삼각형 위로 번져 드럼통 옆에 얼룩이 생겼다 (검수 11-23)
        public Vector3 Normal(int w, Vector3 n0)
        {
            int p = P[w]; Vector3 refN = Vector3.zero;
            foreach (int t in adj[p])
            {
                int k = P[tw[3 * t]] == p ? 0 : P[tw[3 * t + 1]] == p ? 1 : 2;
                if (tw[3 * t + k] != w) continue;
                Vector3 a = pp[P[tw[3 * t + k]]], b = pp[P[tw[3 * t + (k + 1) % 3]]], c = pp[P[tw[3 * t + (k + 2) % 3]]];
                var fn = Vector3.Cross(b - a, c - a); if (fn.sqrMagnitude < 1e-20f) continue;
                refN += fn.normalized * Vector3.Angle(b - a, c - a);
            }
            refN = refN.sqrMagnitude > 1e-12f ? refN.normalized : n0;
            Vector3 s = Vector3.zero;
            foreach (int t in adj[p])
            {
                int k = P[tw[3 * t]] == p ? 0 : P[tw[3 * t + 1]] == p ? 1 : 2;
                Vector3 a = pp[P[tw[3 * t + k]]], b = pp[P[tw[3 * t + (k + 1) % 3]]], c = pp[P[tw[3 * t + (k + 2) % 3]]];
                var fn = Vector3.Cross(b - a, c - a); if (fn.sqrMagnitude < 1e-20f) continue;
                fn.Normalize();
                if (Vector3.Dot(fn, refN) < 0.819f) continue; // 35°
                s += fn * Vector3.Angle(b - a, c - a);
            }
            return s.sqrMagnitude > 1e-12f ? s.normalized : refN;
        }
    }
}
