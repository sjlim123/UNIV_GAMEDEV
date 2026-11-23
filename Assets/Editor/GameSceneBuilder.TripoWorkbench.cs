using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 방공호 제작대의 낮음·중간 판을 상자로 직접 짜고 매우 높음(Q3) 원본의 텍스처를 구워 입힌다 (2026-11-23, 사용자 "매우 높음 품질을 높인 뒤
// 낮음부터 매우 높음까지 간격을 더 벌려 달라"). Tripo 제작대는 구멍 뚫린 공구판 때문에 면 줄이기가 8천 면 아래로 내려가지 않고, 억지로 줄이면
// 곧은 상자 다리가 먼저 합쳐져 한쪽으로 쏠렸다. 상자로 짜면 다리는 늘 곧고, 면 수는 낮음 ~100 · 중간 ~400이 된다.
// 치수는 원본(앞 = 공구판 반대쪽, 공구판 = +Z, 위 = +Y, 원본 크기)에서 잰다 (WbMeasure). 높음(Q2)은 원본을 줄인 것(GameSceneBuilder.TripoTiers.cs)
public static partial class GameSceneBuilder
{
    const string WbId = "shelter_workbench_01";

    // 원본 = 매우 높음 FBX를 TtMake와 같이 앞 = +Z로 돌린 것 + 색 텍스처
    static LpSource WbSource()
    {
        string d = $"{TripoModelDir}/{WbId}/{LpTierDir[3]}/{WbId}_q3";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(d + ".fbx");
        var mf = model.GetComponentInChildren<MeshFilter>();
        var rootRot = Quaternion.Euler(0, TtYaw[WbId], 0) * model.transform.localRotation;
        var mm = Matrix4x4.Rotate(rootRot) * model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var mesh = mf.sharedMesh;
        var p = mesh.vertices; for (int i = 0; i < p.Length; i++) p[i] = mm.MultiplyPoint3x4(p[i]);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        tex.LoadImage(File.ReadAllBytes(d + "_diff.jpg"));
        tex.wrapMode = TextureWrapMode.Clamp;
        var s = new LpSource { p = p, uv = mesh.uv, tri = mesh.triangles, tex = tex };
        s.b = new Bounds(p[0], Vector3.zero); foreach (var v in p) s.b.Encapsulate(v);
        return s;
    }

    class WbDims
    {
        public Bounds all;
        public float topY, topT, shelfY, shelfT;       // 상판 윗면 높이·두께, 아래 선반 윗면 높이·두께
        public Bounds[] legs = new Bounds[4];           // 다리 넷 (상판 아래 ~ 바닥)
        public Bounds board;                            // 공구판
        public Bounds box;                              // 공구함 (아래 선반 위)
        public Bounds vise;                             // 바이스 (상판 위, 공구판 앞)
        public List<Bounds> tools = new List<Bounds>(); // 공구판에 걸린 공구 (렌치·톱): 판 앞에 튀어나온 점 무리
        public string Log() => $"전체 {all.size:F3}, 상판 y {topY:F3} 두께 {topT:F3}, 선반 y {shelfY:F3} 두께 {shelfT:F3}, 다리 {legs[0].center:F3}/{legs[0].size:F3} {legs[1].center:F3} {legs[2].center:F3} {legs[3].center:F3},공구판 {board.center:F3}/{board.size:F3}, 공구함 {box.center:F3}/{box.size:F3}, 바이스 {vise.center:F3}/{vise.size:F3}, 공구 {tools.Count}개 " + string.Join(" ", tools.ConvertAll(b => b.size.ToString("F3")));
    }

    // 면 방향·높이별 넓이로 상판·선반 높이를, 점 무리로 다리·공구판·공구함·바이스 상자를 잰다
    static WbDims WbMeasure(LpSource s)
    {
        var d = new WbDims { all = s.b };
        float y0 = s.b.min.y, H = s.b.size.y; const int N = 200;
        var up = new float[N]; var dn = new float[N];
        for (int t = 0; t < s.tri.Length; t += 3)
        {
            Vector3 a = s.p[s.tri[t]], b = s.p[s.tri[t + 1]], c = s.p[s.tri[t + 2]];
            var n = Vector3.Cross(b - a, c - a); float ar = n.magnitude * 0.5f; if (ar < 1e-12f) continue; n /= ar * 2f;
            int bin = Mathf.Clamp((int)(((a.y + b.y + c.y) / 3f - y0) / H * N), 0, N - 1);
            if (n.y > 0.9f) up[bin] += ar; else if (n.y < -0.9f) dn[bin] += ar;
        }
        System.Func<float[], int, int, int> Peak = (arr, lo, hi) => { int best = lo; for (int i = lo; i < hi; i++) if (arr[i] > arr[best]) best = i; return best; };
        System.Func<int, float> Y = i => y0 + (i + 0.5f) * H / N;
        // 상판: 높이 30~80 %에서 위를 보는 넓이가 가장 큰 곳, 그 아래 10 % 안의 아래를 보는 넓이가 가장 큰 곳 = 밑면
        int ti = Peak(up, (int)(N * 0.3f), (int)(N * 0.8f)); d.topY = Y(ti) + H / N * 0.5f;
        int tb = Peak(dn, Mathf.Max(0, ti - N / 10), ti + 1); d.topT = Mathf.Clamp(d.topY - (Y(tb) - H / N * 0.5f), 0.01f, 0.08f);
        int si = Peak(up, 0, (int)(N * 0.3f)); d.shelfY = Y(si) + H / N * 0.5f;
        int sb = Peak(dn, Mathf.Max(0, si - N / 10), si + 1); d.shelfT = Mathf.Clamp(d.shelfY - (Y(sb) - H / N * 0.5f), 0.008f, 0.05f);
        // 다리: 바닥 위 ~ 아래 선반 밑 사이(다리만 있는 높이)의 점을 네 모서리(x·z가 가운데의 어느 쪽)로 나눈다. 높이는 바닥 ~ 상판 밑.
        // 처음엔 선반 위 ~ 상판 밑에서 쟀는데 옆 가로대·공구함이 섞여 다리 깊이가 0.16으로 잡혔다
        var low = new List<Vector3>();
        foreach (var p in s.p) if (p.y > y0 + H * 0.02f && p.y < d.shelfY - d.shelfT - 0.01f) low.Add(p);
        Vector3 lc = Vector3.zero; foreach (var p in low) lc += p; lc /= Mathf.Max(1, low.Count);
        // 모서리마다 가장 바깥 점에서 x·z 모두 6 % 안인 점만 (선반 밑 가로대·처진 선반이 섞이지 않게)
        var ext = new Vector2[4]; var hasE = new bool[4];
        foreach (var p in low)
        {
            int k = (p.x > lc.x ? 1 : 0) + (p.z > lc.z ? 2 : 0);
            float ex = (k & 1) == 1 ? p.x : -p.x, ez = (k & 2) == 2 ? p.z : -p.z;
            if (!hasE[k]) { ext[k] = new Vector2(ex, ez); hasE[k] = true; } else ext[k] = Vector2.Max(ext[k], new Vector2(ex, ez));
        }
        var has = new bool[4]; float tol = H * 0.06f;
        foreach (var p in low)
        {
            int k = (p.x > lc.x ? 1 : 0) + (p.z > lc.z ? 2 : 0);
            float ex = (k & 1) == 1 ? p.x : -p.x, ez = (k & 2) == 2 ? p.z : -p.z;
            if (ext[k].x - ex > tol || ext[k].y - ez > tol) continue;
            if (!has[k]) { d.legs[k] = new Bounds(p, Vector3.zero); has[k] = true; } else d.legs[k].Encapsulate(p);
        }
        for (int k = 0; k < 4; k++)
        {
            var b = d.legs[k]; b.SetMinMax(new Vector3(b.min.x, s.b.min.y, b.min.z), new Vector3(b.max.x, d.topY - d.topT, b.max.z)); d.legs[k] = b;
        }
        // 공구판: 상판 위 10 cm(원본 단위 0.1 × 높이)보다 높은 점 = 공구판과 걸린 공구. 두께는 뒤쪽 점만
        bool hb = false;
        foreach (var p in s.p) if (p.y > d.topY + H * 0.12f) { if (!hb) { d.board = new Bounds(p, Vector3.zero); hb = true; } else d.board.Encapsulate(p); }
        // 공구판 판 자체: 뒤 끝(z 최대)에서 3 cm 안 (걸린 공구는 앞에 튀어나온다)
        float zb = d.board.max.z; bool hp = false; Bounds plate = new Bounds();
        foreach (var p in s.p) if (p.y > d.topY + H * 0.05f && p.z > zb - H * 0.035f) { if (!hp) { plate = new Bounds(p, Vector3.zero); hp = true; } else plate.Encapsulate(p); }
        plate.SetMinMax(new Vector3(plate.min.x, d.topY, plate.min.z), plate.max);
        d.board = plate;
        // 공구함: 빨간 점 (텍스처 색). 처음엔 '선반 위·다리 밖'으로 골랐는데 선반 가장자리·가로대가 섞여 폭이 0.74로 잡혔다
        bool hx = false;
        for (int i = 0; i < s.p.Length; i++)
        {
            var p = s.p[i];
            if (p.y < d.shelfY - 0.01f || p.y > d.topY - d.topT) continue;
            var c = s.tex.GetPixelBilinear(s.uv[i].x, s.uv[i].y);
            if (c.r < 0.3f || c.r < c.g * 1.8f || c.r < c.b * 1.8f) continue;
            if (!hx) { d.box = new Bounds(p, Vector3.zero); hx = true; } else d.box.Encapsulate(p);
        }
        d.box.SetMinMax(new Vector3(d.box.min.x, d.shelfY, d.box.min.z), d.box.max);
        // 바이스: 상판보다 2 % 높이 이상 솟고 공구판 앞인 점 중, 가운데(중앙값) 둘레에 모인 것. 처음엔 상판의 울퉁불퉁함이 섞여 폭 0.44로 잡혔다
        var vp = new List<Vector3>();
        foreach (var p in s.p) if (p.y > d.topY + H * 0.02f && p.y < d.topY + H * 0.15f && p.z < d.board.min.z - H * 0.04f) vp.Add(p);
        if (vp.Count > 0)
        {
            var xs = new List<float>(); var zs = new List<float>(); foreach (var p in vp) { xs.Add(p.x); zs.Add(p.z); }
            xs.Sort(); zs.Sort(); float mx = xs[xs.Count / 2], mz = zs[zs.Count / 2];
            bool hv = false;
            foreach (var p in vp)
                if (Mathf.Abs(p.x - mx) < H * 0.15f && Mathf.Abs(p.z - mz) < H * 0.15f)
                { if (!hv) { d.vise = new Bounds(p, Vector3.zero); hv = true; } else d.vise.Encapsulate(p); }
            d.vise.SetMinMax(new Vector3(d.vise.min.x, d.topY, d.vise.min.z), d.vise.max);
        }
        // 공구: 상판 위 8 % 높이보다 높고 공구판 앞면 둘레(6 % 안)이며 녹슨 갈색(빨강 > 파랑 × 1.35)인 점. 회색 공구판의 울퉁불퉁함이 섞이면 무리가 하나로 붙었다.
        // x를 늘어놓아 3 cm 넘게 벌어진 곳에서 무리를 나눈다
        var tp = new List<Vector3>();
        for (int i = 0; i < s.p.Length; i++)
        {
            var p = s.p[i];
            if (p.y < d.topY + H * 0.08f || p.z > d.board.min.z + 0.004f || p.z < d.board.min.z - H * 0.06f) continue;
            var c = s.tex.GetPixelBilinear(s.uv[i].x, s.uv[i].y);
            if (c.r > 0.12f && c.r > c.b * 1.35f && c.r >= c.g) tp.Add(p);
        }
        tp.Sort((a, b) => a.x.CompareTo(b.x));
        for (int i = 0; i < tp.Count; i++)
        {
            if (i == 0 || tp[i].x - tp[i - 1].x > 0.03f) d.tools.Add(new Bounds(tp[i], Vector3.zero));
            else { var b = d.tools[d.tools.Count - 1]; b.Encapsulate(tp[i]); d.tools[d.tools.Count - 1] = b; }
        }
        d.tools.RemoveAll(b => b.size.y < H * 0.05f); // 갈고리·티끌
        return d;
    }

    // q 0 = 낮음 (상자만), 1 = 중간 (상판 앞 테두리·다리 발·공구함 손잡이·바이스 턱·공구판 테두리)
    static LpMesh WbBuild(int q, WbDims d)
    {
        var m = new LpMesh();
        var A = d.all;
        // 상판 (밑면은 가려지지 않으므로 그대로 둔다)
        m.Box(new Vector3(A.min.x, d.topY - d.topT, A.min.z), new Vector3(A.max.x, d.topY, d.board.max.z));
        // 아래 선반 (윗면·옆면, 밑면은 바닥 가까이라 뺀다)
        float lx = d.legs[0].size.x, lz = d.legs[0].size.z;
        m.Box(new Vector3(d.legs[0].min.x, d.shelfY - d.shelfT, d.legs[0].min.z), new Vector3(d.legs[3].max.x, d.shelfY, d.legs[3].max.z), 2);
        // 다리 넷 (위·아래 면은 가려진다)
        foreach (var l in d.legs) m.Box(l.min, l.max, 2, 3);
        // 공구판 (아래 면은 상판에 묻힌다)
        m.Box(d.board.min, d.board.max, 2);
        // 공구함·바이스 (아래 면은 붙은 면)
        m.Box(d.box.min, d.box.max, 2);
        m.Box(d.vise.min, d.vise.max, 2);
        // 걸린 공구: 얇은 판 (공구판에 닿는 뒷면은 뺀다). 판 앞면을 구우면 공구가 아니라 판 색을 가져와 공구가 사라졌다
        foreach (var tb in d.tools) m.Box(new Vector3(tb.min.x, tb.min.y, tb.min.z), new Vector3(tb.max.x, tb.max.y, d.board.min.z), 5);
        if (q >= 1)
        {
            float H = A.size.y;
            // 상판 앞 테두리 (앞으로 조금 내민 띠)
            m.Box(new Vector3(A.min.x, d.topY - d.topT * 1.8f, A.min.z - 0.004f), new Vector3(A.max.x, d.topY - d.topT, A.min.z + 0.012f), 3);
            // 다리 발판 (바닥에서 조금 넓게)
            foreach (var l in d.legs)
                m.Box(new Vector3(l.min.x - 0.006f, l.min.y, l.min.z - 0.006f), new Vector3(l.max.x + 0.006f, l.min.y + 0.012f, l.max.z + 0.006f), 2);
            // 공구판 테두리 (위·좌·우 띠, 앞으로 1 cm)
            var B = d.board; float bw = H * 0.02f;
            m.Box(new Vector3(B.min.x, B.max.y - bw, B.min.z - 0.01f), new Vector3(B.max.x, B.max.y, B.min.z), 3, 5);
            m.Box(new Vector3(B.min.x, B.min.y, B.min.z - 0.01f), new Vector3(B.min.x + bw, B.max.y - bw, B.min.z), 5);
            m.Box(new Vector3(B.max.x - bw, B.min.y, B.min.z - 0.01f), new Vector3(B.max.x, B.max.y - bw, B.min.z), 5);
            // 공구함 손잡이 (뚜껑 위 가운데 가로 막대)
            var X = d.box; float hw = X.size.x * 0.35f, hh = X.size.y * 0.12f;
            m.Box(new Vector3(X.center.x - hw, X.max.y, X.center.z - 0.006f), new Vector3(X.center.x + hw, X.max.y + hh, X.center.z + 0.006f), 2);
            // 바이스: 몸통 위에 앞뒤 두 턱
            var V = d.vise; float jy = V.size.y * 0.35f;
            m.Box(new Vector3(V.min.x, V.max.y, V.center.z - V.size.z * 0.1f), new Vector3(V.max.x, V.max.y + jy, V.center.z + V.size.z * 0.1f), 2);
        }
        return m;
    }

    // 높음(Q2): 원본에서 구멍 뚫린 공구판 판만 빼고(5만 면 원본의 57 %, 구멍 하나하나가 면) 그 자리에 얇은 판 하나를 넣어 공구판 앞면을 구워 입힌다.
    // 다리·선반·공구함·바이스·걸린 공구는 원본 면 그대로라 무너지지 않는다. 이음매를 지키며 줄였더니 2.2만 면에서 멈추고 선반·상판 모서리가 물결쳤다 (11-23).
    // 텍스처 한 장(512): 왼쪽 3/4 = 원본 텍스처를 줄인 것(남긴 면의 UV는 u × 0.75), 오른쪽 1/4 = 공구판 앞면 (긴 변을 세로로)
    static string WbMakeHigh()
    {
        const int size = 512; const float split = 0.75f;
        string d3 = $"{TripoModelDir}/{WbId}/{LpTierDir[3]}/{WbId}_q3";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(d3 + ".fbx");
        var mf = model.GetComponentInChildren<MeshFilter>();
        var rootRot = Quaternion.Euler(0, TtYaw[WbId], 0) * model.transform.localRotation;
        var mm = Matrix4x4.Rotate(rootRot) * model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var nm = mm.inverse.transpose;
        var src = mf.sharedMesh;
        var sp = src.vertices; var sn = src.normals; var su = src.uv; var st = src.triangles;
        for (int i = 0; i < sp.Length; i++) { sp[i] = mm.MultiplyPoint3x4(sp[i]); sn[i] = nm.MultiplyVector(sn[i]).normalized; }
        var s = WbSource();
        var d = WbMeasure(s);
        var B = d.board;

        var m = new LpMesh(); var uv = new List<Vector2>();
        // 1) 공구판 판이 아닌 원본 면 (가운데가 판 앞면보다 2 mm 이상 앞이거나 상판 위 1 cm 아래)
        var remap = new Dictionary<int, int>(); int dropped = 0;
        for (int k = 0; k < st.Length; k += 3)
        {
            var c = (sp[st[k]] + sp[st[k + 1]] + sp[st[k + 2]]) / 3f;
            if (c.y > d.topY + 0.01f && c.z > B.min.z - 0.002f) { dropped++; continue; }
            for (int j = 0; j < 3; j++)
            {
                int w = st[k + j];
                if (!remap.TryGetValue(w, out int nw)) { nw = m.V(sp[w], sn[w], Vector2.zero, 0); uv.Add(new Vector2(su[w].x * split, su[w].y)); remap[w] = nw; }
                m.tri.Add(nw);
            }
        }
        int kept = m.tri.Count / 3;
        // 2) 공구판 판 상자: 앞·뒤 면은 오른쪽 띠 전체(앞면 그림), 위·옆 면은 띠 맨 아래 작은 칸(판 테두리 색)
        var board = new LpMesh();
        board.Poly(new[] { new Vector3(B.min.x, B.min.y, B.min.z), new Vector3(B.max.x, B.min.y, B.min.z), new Vector3(B.max.x, B.max.y, B.min.z), new Vector3(B.min.x, B.max.y, B.min.z) }, Vector3.back, Vector3.right, Vector3.up, 0);
        float u0 = split + 0.5f / size, u1 = 1f - 0.5f / size;
        float vLen = Mathf.Min(1f - 1f / size, (u1 - u0) * size / B.size.y * B.size.x / size); // 판 비율 그대로 (가로 = 띠 폭이 판 높이)
        System.Func<Vector3, Vector2> FrontUv = p => new Vector2(Mathf.Lerp(u0, u1, (p.y - B.min.y) / B.size.y), Mathf.Lerp(0.5f / size, vLen, (B.max.x - p.x) / B.size.x));
        var bpx = new Color32[0];
        {
            var buv = new Vector2[board.pos.Count];
            for (int i = 0; i < board.pos.Count; i++) buv[i] = FrontUv(board.pos[i]);
            bpx = LpBake(board, buv, s, size, s.b.size.magnitude * 0.03f);
        }
        System.Action<Vector3[], Vector3, bool> Quad = (q4, n, front) =>
        {
            var id = new int[4];
            for (int i = 0; i < 4; i++)
            {
                id[i] = m.V(q4[i], n, Vector2.zero, 0);
                uv.Add(front ? FrontUv(q4[i]) : new Vector2(u0 + 0.02f * (i == 1 || i == 2 ? 1 : 0), vLen + 0.004f + 0.004f * (i >= 2 ? 1 : 0)));
            }
            m.T(id[0], id[1], id[2]); m.T(id[0], id[2], id[3]);
        };
        Vector3 a0 = new Vector3(B.min.x, B.min.y, B.min.z), a1 = new Vector3(B.max.x, B.min.y, B.min.z), a2 = new Vector3(B.max.x, B.max.y, B.min.z), a3 = new Vector3(B.min.x, B.max.y, B.min.z);
        Vector3 b0 = new Vector3(B.min.x, B.min.y, B.max.z), b1 = new Vector3(B.max.x, B.min.y, B.max.z), b2 = new Vector3(B.max.x, B.max.y, B.max.z), b3 = new Vector3(B.min.x, B.max.y, B.max.z);
        Quad(new[] { a0, a1, a2, a3 }, Vector3.back, true);                // 앞
        Quad(new[] { b1, b0, b3, b2 }, Vector3.forward, true);             // 뒤 (벽 쪽, 같은 그림)
        Quad(new[] { a3, a2, b2, b3 }, Vector3.up, false);                 // 위
        Quad(new[] { b0, a0, a3, b3 }, Vector3.left, false);               // 왼쪽
        Quad(new[] { a1, b1, b2, a2 }, Vector3.right, false);              // 오른쪽

        // 3) 텍스처: 왼쪽 = 원본을 384 × 512로 (2048 원본의 밉 2단계 512에서 이중 선형), 오른쪽 = 구운 판
        var px = new Color32[size * size];
        int L = Mathf.RoundToInt(size * split);
        var srcTex = s.tex; srcTex.Apply(true);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (x < L) px[y * size + x] = srcTex.GetPixelBilinear((x + 0.5f) / L, (y + 0.5f) / size, 2);
                else px[y * size + x] = bpx[y * size + x];
            }
        // 띠 맨 아래 테두리 칸: 판 앞면 가장자리 색 (공구판 위 모서리 근처 평균)
        var edge = (Color)bpx[Mathf.Clamp(Mathf.RoundToInt(vLen * size) - 3, 0, size - 1) * size + (L + 2)];
        for (int y = Mathf.RoundToInt(vLen * size); y < size; y++) for (int x = L; x < size; x++) px[y * size + x] = edge;
        Object.DestroyImmediate(s.tex);
        string r = LpSave(WbId, m, uv.ToArray(), px, size, "_q2", "", LpTierDir[2]);
        return r + $" (원본 면 {kept} + 공구판 판 12, 뺀 공구판 면 {dropped})";
    }

    // 낮음(256)·중간(512) 판을 짜서 굽고 저장한다. 높음·매우 높음은 TtMake가 만든다
    static string WbMakeLow()
    {
        var s = WbSource();
        var d = WbMeasure(s);
        var log = new List<string> { d.Log() };
        for (int q = 0; q < 2; q++)
        {
            int size = q == 0 ? 256 : 512;
            var m = WbBuild(q, d);
            var uv = LpPackUv(m, size, q == 0 ? 2 : 3);
            var px = LpBake(m, uv, s, size, s.b.size.magnitude * 0.05f);
            log.Add(LpSave(WbId, m, uv, px, size, "_q" + q, "", LpTierDir[q]));
        }
        Object.DestroyImmediate(s.tex);
        return string.Join(" / ", log);
    }
}
