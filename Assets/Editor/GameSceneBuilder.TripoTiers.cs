using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tripo 소품 5종(세단·뒤집힌 세단·소화전·드럼통·버스정류장)의 품질별 판 (2026-11-23, 사용자 "5개 낮은 단계 만들기").
// 매우 높음(Q3) = 11-22에 다시 만든 Tripo FBX 그대로. 높음·중간·낮음(Q2·Q1·Q0) = 그 메시를 텍스처 이음매를 지키며 줄인 것 (TtSimplifier,
// GameSceneBuilder.TripoSimplify.cs). 처음엔 Unity 6 메시 LOD 생성기(MeshLodUtility)를 썼는데 이음매를 넘어 합쳐 텍스처가 찢어졌다.
// 남는 꼭짓점의 UV가 원본 그대로라 텍스처는 Q3 텍스처를 줄여 쓰면 된다 (다시 굽지 않는다).
// 결과 (모델 폴더의 품질별 하위 폴더, 실외기·나무와 같은 이름): Q0_낮음/{id}_q0.asset·_q0_diff.png·_q0.prefab, Q1_중간/…_q1…, Q2_높음/…_q2…,
// Q3_매우높음/{id}_q3.prefab (메시는 같은 폴더의 _q3.fbx를 그대로 쓴다: 2만 삼각형 메시를 .asset으로 또 두면 저장소만 커진다).
// 좌표: 실외기·나무 판과 같이 '앞 = +Z, 위 = +Y' (TripoSpecs의 yaw를 메시에 미리 돌려 넣음). 뒤집힌 차는 모델 자체가 뒤집힌 자세 그대로 (roll은 넣을 때)
public static partial class GameSceneBuilder
{
    // 목표 삼각형 수 {낮음, 중간, 높음}. 이음매·가장자리를 지키느라 더 못 줄이면 그 자리에서 멈춘다 (로그에 실제 수)
    // 기준: 높음 ≈ 매우 높음의 1/2(작은 소품은 1/5), 중간 ≈ 높음의 1/4, 낮음 ≈ 중간의 1/4 — 실외기(124·704·3,954·7,418)·나무처럼 단계마다 확연히 다르게
    static readonly Dictionary<string, int[]> TtTargets = new Dictionary<string, int[]>
    {
        { "wrecked_sedan_01",         new[] { 700, 2700, 10000 } },
        { "wrecked_sedan_flipped_01", new[] { 1000, 2500, 10000 } },
        { "fire_hydrant_01",          new[] { 640, 1280, 5000 } },
        { "steel_drum_01",            new[] { 320, 1280, 5000 } },
        { "bus_stop_01",              new[] { 700, 1400, 7000 } },
        // 트럭 (11-23 다시 만듦, 1만 판 12,970). 도시에 하나뿐이고 수리 목표라 가까이서 오래 보므로 낮음도 넉넉히
        { "pickup_truck_01",          new[] { 1200, 3000, 7000 } },
    };

    // TripoSpecs(재질 값이 정해진 모델)에 아직 없는 모델의 돌림 각도 (앞 = +Z)
    static readonly Dictionary<string, float> TtYaw = new Dictionary<string, float> { { "pickup_truck_01", 90f } };

    [MenuItem("Tools/핵폭발 생존/Tripo 품질별 판 만들기 (차·뒤집힌 차·소화전·드럼통·정류장·트럭)")]
    static void TtBuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        var log = new List<string>();
        foreach (var kv in TtTargets) log.Add(TtMake(kv.Key, kv.Value));
        Debug.Log("[GameSceneBuilder] Tripo 품질별 판: " + string.Join(" / ", log));
    }

    static string TtMake(string id, int[] targets)
    {
        string dir = $"{TripoModelDir}/{id}";
        string q3Dir = $"{dir}/{LpTierDir[3]}";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{q3Dir}/{id}_q3.fbx");
        if (model == null) return id + ": Q3 FBX 없음";
        var mf = model.GetComponentInChildren<MeshFilter>();
        var src = mf.sharedMesh;
        float yaw = TripoSpecs.TryGetValue(id, out var spec) ? spec.yaw : TtYaw[id];
        var rootRot = Quaternion.Euler(0, yaw, 0) * model.transform.localRotation;
        var mm = Matrix4x4.Rotate(rootRot) * model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var nm = mm.inverse.transpose;

        // 원본 꼭짓점을 '앞 = +Z' 좌표로
        var pos = src.vertices; var nrm = src.normals; var uv = src.uv; var tri0 = src.triangles;
        for (int i = 0; i < pos.Length; i++) { pos[i] = mm.MultiplyPoint3x4(pos[i]); nrm[i] = nm.MultiplyVector(nrm[i]).normalized; }
        var srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        srcTex.LoadImage(File.ReadAllBytes($"{q3Dir}/{id}_q3_diff.jpg"));
        srcTex.wrapMode = TextureWrapMode.Clamp; // 굽기에서 아틀라스 가장자리 색이 반대쪽과 섞이지 않게

        var srcB = new Bounds(pos[0], Vector3.zero); foreach (var v in pos) srcB.Encapsulate(v);
        var bakeSrc = new LpSource { p = pos, uv = uv, tri = tri0, tex = srcTex, b = srcB };

        var parts = new List<string>();
        for (int q = 2; q >= 0; q--)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int size = q == 0 ? 256 : 512; // 높음·중간 512, 낮음 256 (실외기·나무와 같다)
            List<Vector3> p2, n2; List<Vector2> u2; int[] t2; Color32[] px = null; string how;

            // 1) 텍스처 이음매를 지키며 줄이기 (원본 UV·텍스처 그대로). 목표의 115 % 안에 들면 이것을 쓴다
            var simp = new TtSimplifier(pos, uv, tri0);
            simp.Run(targets[q]);
            if (simp.alive <= targets[q] * 1.15f)
            {
                how = "UV 그대로";
                var idx = simp.Triangles();
                var remap = new Dictionary<int, int>();
                p2 = new List<Vector3>(); n2 = new List<Vector3>(); u2 = new List<Vector2>();
                t2 = new int[idx.Length];
                for (int i = 0; i < idx.Length; i++)
                {
                    if (!remap.TryGetValue(idx[i], out int w))
                    {
                        w = p2.Count; remap[idx[i]] = w;
                        // 법선은 줄인 면들로 다시 낸다 (원본 꼭짓점 법선의 작은 울퉁불퉁함이 길어진 삼각형 위로 번져 드럼통 옆에 얼룩이 생겼다 — 검수 11-23)
                        p2.Add(pos[idx[i]]); n2.Add(simp.Normal(idx[i], nrm[idx[i]])); u2.Add(uv[idx[i]]);
                    }
                    t2[i] = w;
                }
            }
            else
            {
                // 2) 이음매가 너무 잘게 나뉘어 더 못 줄이면: 모양만 줄이고(같은 자리 꼭짓점을 하나로), 새로 펼쳐 원본 텍스처를 구워 옮긴다
                how = "새로 구움 (이음매 지키기로는 " + simp.alive + "에서 멈춤)";
                var m = TtRebakeMesh(pos, tri0, targets[q], q == 0 ? 0.05f : 0f, out int dropped);
                var uvb = LpPackUv(m, size, q == 0 ? 2 : 3);
                int outside = 0; foreach (var w in uvb) if (w.x < 0f || w.y < 0f || w.x > 1f || w.y > 1f) outside++;
                if (outside > 0) Debug.LogWarning($"[GameSceneBuilder] {id} q{q}: 텍스처 한 장에 조각이 다 들어가지 않음 (꼭짓점 {outside}개가 밖)");
                px = LpBake(m, uvb, bakeSrc, size, 0.06f);
                p2 = m.pos; n2 = m.nrm; u2 = new List<Vector2>(uvb); t2 = m.tri.ToArray();
                how += ", 조각 " + m.charts + (dropped > 0 ? ", 작은 덩어리 면 " + dropped + "개 뺌" : "");
            }
            long ms = sw.ElapsedMilliseconds;

            string sub = $"{dir}/{LpTierDir[q]}";
            if (!AssetDatabase.IsValidFolder(sub)) AssetDatabase.CreateFolder(dir, LpTierDir[q]);
            string suf = "_q" + q;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{sub}/{id}{suf}.asset");
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = id + suf;
            mesh.indexFormat = p2.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(p2); mesh.SetNormals(n2); mesh.SetUVs(0, u2); mesh.SetTriangles(t2, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            if (isNew) AssetDatabase.CreateAsset(mesh, $"{sub}/{id}{suf}.asset");
            else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }

            // 텍스처: UV 그대로면 Q3 원본(2048)의 밉 단계(상자 평균), 새로 구웠으면 구운 것
            Texture2D tex;
            if (px == null)
            {
                int mip = Mathf.RoundToInt(Mathf.Log(srcTex.width / (float)size, 2f));
                int mw = Mathf.Max(1, srcTex.width >> mip), mh = Mathf.Max(1, srcTex.height >> mip);
                tex = new Texture2D(mw, mh, TextureFormat.RGBA32, false);
                tex.SetPixels32(srcTex.GetPixels32(mip));
            }
            else { tex = new Texture2D(size, size, TextureFormat.RGBA32, false); tex.SetPixels32(px); }
            tex.Apply();
            string texPath = $"{sub}/{id}{suf}_diff.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null && (ti.maxTextureSize != size || !ti.sRGBTexture || !ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Compressed || ti.alphaSource != TextureImporterAlphaSource.None || ti.mipmapLimitGroupName != ""))
            {
                ti.maxTextureSize = size; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed;
                ti.alphaSource = TextureImporterAlphaSource.None; ti.mipmapLimitGroupName = "";
                ti.SaveAndReimport();
            }

            var go = new GameObject(id + suf);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{sub}/{id}{suf}.prefab");
            Object.DestroyImmediate(go);
            parts.Insert(0, $"q{q} {t2.Length / 3}(목표 {targets[q]}, 꼭짓점 {p2.Count}, 텍스처 {size}, {how}, {ms} ms)");
        }
        Object.DestroyImmediate(srcTex);

        // 매우 높음: FBX 메시를 그대로, 같은 좌표가 되게 자식을 돌려 둔 프리팹
        {
            var go = new GameObject(id + "_q3");
            var child = new GameObject("M");
            child.transform.SetParent(go.transform, false);
            child.transform.localRotation = rootRot;
            child.AddComponent<MeshFilter>().sharedMesh = src;
            child.AddComponent<MeshRenderer>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{q3Dir}/{id}_q3.prefab");
            Object.DestroyImmediate(go);
        }
        parts.Add($"q3 {src.GetIndexCount(0) / 3}(FBX)");
        return id + ": " + string.Join(", ", parts);
    }

    // 모양만 줄이고(같은 자리 꼭짓점을 한 점으로 묶어 이음매 없이 QEM) 새 조각으로 나눈 메시. 조각 = 이웃 면을 따라 번지되
    // 조각 평균 법선과 45° 안, 바로 옆 면과 35° 안인 면까지 (평면에 비춰 펼친다). 법선은 그 점의 면 중 이 조각 쪽 법선과 60° 안인 면의 평균
    // minPart > 0: 전체 대각선의 그 비율보다 작은 따로 떨어진 조각(볼트·너트 등)을 먼저 뺀다 (낮음 판: 작은 조각이 면을 붙잡아 큰 모양이 먼저 무너졌다)
    static LpMesh TtRebakeMesh(Vector3[] wpos, int[] wtri, int target, float minPart, out int dropped)
    {
        var map = new Dictionary<Vector3Int, int>(); var pl = new List<Vector3>(); var P = new int[wpos.Length];
        for (int w = 0; w < wpos.Length; w++)
        {
            var key = Vector3Int.RoundToInt(wpos[w] * 100000f);
            if (!map.TryGetValue(key, out int p)) { p = pl.Count; pl.Add(wpos[w]); map[key] = p; }
            P[w] = p;
        }
        var ptri = new int[wtri.Length]; for (int i = 0; i < wtri.Length; i++) ptri[i] = P[wtri[i]];
        var pts = pl.ToArray();
        dropped = 0;
        if (minPart > 0f)
        {
            // 이어진 덩어리 (점 기준 합집합 찾기)
            var par = new int[pts.Length]; for (int i = 0; i < par.Length; i++) par[i] = i;
            System.Func<int, int> Find = null;
            Find = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
            for (int t = 0; t < ptri.Length; t += 3) { int a = Find(ptri[t]), b = Find(ptri[t + 1]), c = Find(ptri[t + 2]); par[b] = a; par[Find(c)] = a; }
            var lo = new Dictionary<int, Vector3>(); var hi = new Dictionary<int, Vector3>();
            var all = new Bounds(pts[0], Vector3.zero);
            for (int i = 0; i < pts.Length; i++)
            {
                int r = Find(i); all.Encapsulate(pts[i]);
                if (!lo.ContainsKey(r)) { lo[r] = pts[i]; hi[r] = pts[i]; } else { lo[r] = Vector3.Min(lo[r], pts[i]); hi[r] = Vector3.Max(hi[r], pts[i]); }
            }
            float lim = all.size.magnitude * minPart;
            var keep = new List<int>();
            for (int t = 0; t < ptri.Length; t += 3)
            {
                int r = Find(ptri[t]);
                if ((hi[r] - lo[r]).magnitude < lim) { dropped++; continue; }
                keep.Add(ptri[t]); keep.Add(ptri[t + 1]); keep.Add(ptri[t + 2]);
            }
            ptri = keep.ToArray();
        }
        var simp = new TtSimplifier(pts, new Vector2[pts.Length], ptri, true);
        simp.Run(target);
        for (int i = 0; i < pts.Length; i++) pts[i] = simp.Pos(i);
        var tri = simp.Triangles(); // 점 번호 (쐐기 = 점)
        tri = TtDropTinyShells(tri, pts.Length, 4); // 줄이다 남은 면 4개 이하 조각(볼트 찌꺼기)은 뺀다
        int T = tri.Length / 3;
        var fn = new Vector3[T]; var fa = new float[T];
        var pf = new Dictionary<int, List<int>>();
        var ef = new Dictionary<long, List<int>>();
        for (int t = 0; t < T; t++)
        {
            Vector3 a = pts[tri[3 * t]], b = pts[tri[3 * t + 1]], c = pts[tri[3 * t + 2]];
            var n = Vector3.Cross(b - a, c - a); fa[t] = n.magnitude; fn[t] = fa[t] > 1e-12f ? n / fa[t] : Vector3.up;
            for (int k = 0; k < 3; k++)
            {
                int p0 = tri[3 * t + k], p1 = tri[3 * t + (k + 1) % 3];
                if (!pf.TryGetValue(p0, out var l)) pf[p0] = l = new List<int>();
                l.Add(t);
                long key = p0 < p1 ? ((long)p0 << 32) | (uint)p1 : ((long)p1 << 32) | (uint)p0;
                if (!ef.TryGetValue(key, out var e)) ef[key] = e = new List<int>(2);
                e.Add(t);
            }
        }
        // 조각 나누기 (넓은 면부터 씨앗)
        var chartOf = new int[T]; for (int t = 0; t < T; t++) chartOf[t] = -1;
        var order = new List<int>(); for (int t = 0; t < T; t++) order.Add(t);
        order.Sort((x, y) => fa[y].CompareTo(fa[x]));
        var m = new LpMesh();
        var chartN = new List<Vector3>();
        // 조각 평균과 60° 안, 바로 옆 면과 50° 안 (처음 45°·35°는 면 1~2개 조각이 절반이라 텍스처 대부분이 여백이었다 — 검수 11-23)
        float cos45 = Mathf.Cos(60f * Mathf.Deg2Rad), cos35 = Mathf.Cos(50f * Mathf.Deg2Rad);
        foreach (int seed in order)
        {
            if (chartOf[seed] >= 0) continue;
            int c = m.NewChart(); chartOf[seed] = c;
            var sum = fn[seed] * fa[seed]; var avg = fn[seed];
            var queue = new Queue<int>(); queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                int f = queue.Dequeue();
                for (int k = 0; k < 3; k++)
                {
                    int p0 = tri[3 * f + k], p1 = tri[3 * f + (k + 1) % 3];
                    long key = p0 < p1 ? ((long)p0 << 32) | (uint)p1 : ((long)p1 << 32) | (uint)p0;
                    foreach (int g in ef[key])
                    {
                        if (chartOf[g] >= 0 || g == f) continue;
                        if (Vector3.Dot(fn[g], avg) < cos45 || Vector3.Dot(fn[g], fn[f]) < cos35) continue;
                        chartOf[g] = c; sum += fn[g] * fa[g]; avg = sum.normalized; queue.Enqueue(g);
                    }
                }
            }
            chartN.Add(avg);
        }
        // 면 5개보다 작은 조각은 이웃 조각 중 법선이 가장 비슷한 것(75° 안)에 붙인다
        {
            var cnt = new int[m.charts]; foreach (int c in chartOf) cnt[c]++;
            var csum = new Vector3[m.charts]; for (int t = 0; t < T; t++) csum[chartOf[t]] += fn[t] * fa[t];
            float cos75 = Mathf.Cos(75f * Mathf.Deg2Rad);
            for (int pass = 0; pass < 3; pass++)
                for (int t = 0; t < T; t++)
                {
                    int c = chartOf[t]; if (cnt[c] >= 5) continue;
                    int best = -1; float bd = cos75;
                    for (int k = 0; k < 3; k++)
                    {
                        int p0 = tri[3 * t + k], p1 = tri[3 * t + (k + 1) % 3];
                        long key = p0 < p1 ? ((long)p0 << 32) | (uint)p1 : ((long)p1 << 32) | (uint)p0;
                        foreach (int g in ef[key])
                        {
                            int cg = chartOf[g]; if (cg == c || cnt[cg] < cnt[c]) continue;
                            float d = Vector3.Dot(fn[t], csum[cg].normalized);
                            if (d > bd) { bd = d; best = cg; }
                        }
                    }
                    if (best < 0) continue;
                    cnt[c]--; csum[c] -= fn[t] * fa[t]; chartOf[t] = best; cnt[best]++; csum[best] += fn[t] * fa[t];
                }
            // 빈 조각 번호를 당겨 채운다
            var renum = new int[m.charts]; for (int c = 0; c < renum.Length; c++) renum[c] = -1;
            int nc = 0; var newN = new List<Vector3>();
            for (int t = 0; t < T; t++) { int c = chartOf[t]; if (renum[c] < 0) { renum[c] = nc++; newN.Add(csum[c].sqrMagnitude > 1e-20f ? csum[c].normalized : chartN[c]); } chartOf[t] = renum[c]; }
            m.charts = nc; chartN = newN;
        }
        // 조각마다 평면 축 (조각 평균 법선에 수직)
        var ax = new Vector3[m.charts]; var ay = new Vector3[m.charts];
        for (int c = 0; c < m.charts; c++)
        {
            var n = chartN[c];
            var x = Vector3.Cross(Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right, n).normalized;
            ax[c] = x; ay[c] = Vector3.Cross(n, x);
        }
        // 꼭짓점 = (점, 조각)
        var wk = new Dictionary<long, int>();
        for (int t = 0; t < T; t++)
        {
            int c = chartOf[t];
            for (int k = 0; k < 3; k++)
            {
                int p = tri[3 * t + k];
                long key = ((long)p << 20) | (uint)c;
                if (!wk.TryGetValue(key, out int vi))
                {
                    // 이 조각 쪽 기준 법선: 이 점에서 이 조각에 든 면들의 평균
                    Vector3 refN = Vector3.zero;
                    foreach (int g in pf[p]) if (chartOf[g] == c) refN += fn[g] * fa[g];
                    refN = refN.sqrMagnitude > 1e-20f ? refN.normalized : chartN[c];
                    Vector3 s = Vector3.zero;
                    foreach (int g in pf[p])
                    {
                        if (Vector3.Dot(fn[g], refN) < 0.5f) continue;
                        int kk = tri[3 * g] == p ? 0 : tri[3 * g + 1] == p ? 1 : 2;
                        Vector3 a = pts[tri[3 * g + kk]], b = pts[tri[3 * g + (kk + 1) % 3]], cc = pts[tri[3 * g + (kk + 2) % 3]];
                        s += fn[g] * Vector3.Angle(b - a, cc - a);
                    }
                    var nrmv = s.sqrMagnitude > 1e-12f ? s.normalized : refN;
                    var pt = pts[p];
                    vi = m.V(pt, nrmv, new Vector2(Vector3.Dot(pt, ax[c]), Vector3.Dot(pt, ay[c])), c);
                    wk[key] = vi;
                }
                m.tri.Add(vi);
            }
        }
        return m;
    }

    // 면 수가 limit 이하인 따로 떨어진 덩어리(점으로 이어진 것)를 뺀 삼각형 (점 번호)
    static int[] TtDropTinyShells(int[] tri, int np, int limit)
    {
        var par = new int[np]; for (int i = 0; i < np; i++) par[i] = i;
        System.Func<int, int> Find = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
        for (int t = 0; t < tri.Length; t += 3) { int a = Find(tri[t]); par[Find(tri[t + 1])] = a; par[Find(tri[t + 2])] = Find(a); }
        var cnt = new Dictionary<int, int>();
        for (int t = 0; t < tri.Length; t += 3) { int r = Find(tri[t]); cnt.TryGetValue(r, out int n); cnt[r] = n + 1; }
        var keep = new List<int>(tri.Length);
        for (int t = 0; t < tri.Length; t += 3) if (cnt[Find(tri[t])] > limit) { keep.Add(tri[t]); keep.Add(tri[t + 1]); keep.Add(tri[t + 2]); }
        return keep.ToArray();
    }
}
