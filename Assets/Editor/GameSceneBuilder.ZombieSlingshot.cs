using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 원거리 감염자의 새총 (2026-11-27, 사용자 "새총 4번"): Tripo가 만든 Y자 나무 틀만 모델로 쓰고, 고무줄·가죽 주머니·돌은 지금처럼 코드가 만든다.
// Tripo 새총(Assets/ThirdParty/Tripo/Enemies/slingshot/slingshot.fbx, 높이 1 단위)을 새총 기준 좌표로 옮긴다:
//   y = 갈래 쪽, x = 두 갈래가 벌어진 쪽, z = 두께(겨누는 쪽), 원점 = 테이프 손잡이 가운데(주먹이 쥐는 곳), 전체 높이 ZsHeight m.
// 품질 4단계(TtSimplifier, 텍스처 128·256·512·1024)를 LOD 묶음 + QualityLod로 묶고, 두 갈래 끝(고무 캡 가운데)에 빈 TipL·TipR을 둔다.
// 게임은 Resources/Enemies/ZombieRanged/slingshot.prefab을 왼손 주먹에 꽂고 TipL·TipR에서 고무줄을 건다 (ZombieLooks.BuildSlingshot).
public static partial class GameSceneBuilder
{
    const string ZsDir = "Assets/ThirdParty/Tripo/Enemies/slingshot/";
    const float ZsHeight = 0.27f;          // 손잡이 끝에서 갈래 끝까지 (m)
    const float ZsGripAt = 0.20f;          // 손잡이 가운데 = 아래에서 높이의 20% (테이프 감은 곳)
    static readonly int[] ZsTargets = { 600, 1500, 4000, 10000 };

    public static string ZsBuild(string prefabPath)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ZsDir + "slingshot.fbx");
        if (asset == null) return "slingshot.fbx 없음";
        var mf = asset.GetComponentInChildren<MeshFilter>();
        var src = mf.sharedMesh;
        var M = mf.transform.localToWorldMatrix;
        var sv = src.vertices; var sn = src.normals; var uv = src.uv; var tri = src.triangles;
        var w = new Vector3[sv.Length];
        var b = new Bounds();
        for (int i = 0; i < sv.Length; i++) { w[i] = M.MultiplyPoint3x4(sv[i]); if (i == 0) b = new Bounds(w[i], Vector3.zero); else b.Encapsulate(w[i]); }

        // 축: 가장 긴 축 = 위(갈래), 다음 = 벌어진 쪽, 나머지 = 두께
        var ext = b.size; var ax = new[] { 0, 1, 2 };
        System.Array.Sort(ax, (p, q) => ext[q].CompareTo(ext[p]));
        Vector3 Axis(int a) { var v = Vector3.zero; v[a] = 1; return v; }
        Vector3 ey = Axis(ax[0]), ex = Axis(ax[1]);
        float H = ext[ax[0]], lo = b.min[ax[0]];
        // 갈래 쪽: 위·아래 20%에서 벌어진 폭이 넓은 끝
        float SpreadAt(float t0, float t1)
        {
            float mn = float.MaxValue, mx = float.MinValue;
            foreach (var p in w) { float t = (p[ax[0]] - lo) / H; if (t >= t0 && t <= t1) { mn = Mathf.Min(mn, p[ax[1]]); mx = Mathf.Max(mx, p[ax[1]]); } }
            return mx - mn;
        }
        bool flip = SpreadAt(0f, 0.2f) > SpreadAt(0.8f, 1f);
        if (flip) ey = -ey;
        var ez = Vector3.Cross(ex, ey); // 회전만 (거울 없음)

        // 원점: 손잡이 가운데 단면의 중심
        float gripH = flip ? b.max[ax[0]] - ZsGripAt * H : lo + ZsGripAt * H;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var p in w) if (Mathf.Abs(p[ax[0]] - gripH) < 0.03f * H) { sum += p; n++; }
        var o = n > 0 ? sum / n : b.center;
        o[ax[0]] = gripH;
        float k = ZsHeight / H;

        var pos = new Vector3[w.Length]; var nrm = new Vector3[w.Length];
        for (int i = 0; i < w.Length; i++)
        {
            var d = w[i] - o;
            pos[i] = new Vector3(Vector3.Dot(d, ex), Vector3.Dot(d, ey), Vector3.Dot(d, ez)) * k;
            var nn = M.MultiplyVector(sn[i]);
            nrm[i] = new Vector3(Vector3.Dot(nn, ex), Vector3.Dot(nn, ey), Vector3.Dot(nn, ez)).normalized;
        }
        // 갈래 끝: 맨 위 8% 점을 좌우로 나눈 가운데 (고무 캡)
        float top = float.MinValue; foreach (var p in pos) top = Mathf.Max(top, p.y);
        float bottom = float.MaxValue; foreach (var p in pos) bottom = Mathf.Min(bottom, p.y);
        Vector3 tl = Vector3.zero, tr = Vector3.zero; int nl = 0, nr = 0;
        foreach (var p in pos)
            if (p.y > top - 0.08f * (top - bottom)) { if (p.x < 0) { tl += p; nl++; } else { tr += p; nr++; } }
        tl = nl > 0 ? tl / nl : new Vector3(-0.04f, top, 0); tr = nr > 0 ? tr / nr : new Vector3(0.04f, top, 0);

        // 품질 4단계
        var texSrc = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        texSrc.LoadImage(File.ReadAllBytes(ZsDir + "slingshot_basecolor.jpg"));
        var meshes = new Mesh[4]; var mats = new Material[4]; var counts = new List<string>();
        for (int q = 0; q < 4; q++)
        {
            var simp = new TtSimplifier(pos, uv, tri, false, null);
            simp.Run(ZsTargets[q]);
            var idx = simp.Triangles();
            var map = new Dictionary<int, int>(); var p2 = new List<Vector3>(); var n2 = new List<Vector3>(); var u2 = new List<Vector2>();
            var t2 = new int[idx.Length];
            for (int i = 0; i < idx.Length; i++)
            {
                if (!map.TryGetValue(idx[i], out int v))
                {
                    v = p2.Count; map[idx[i]] = v;
                    p2.Add(pos[idx[i]]); n2.Add(simp.Normal(idx[i], nrm[idx[i]])); u2.Add(uv[idx[i]]);
                }
                t2[i] = v;
            }
            var m = new Mesh { name = $"slingshot_q{q}", indexFormat = p2.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(p2); m.SetNormals(n2); m.SetUVs(0, u2); m.SetTriangles(t2, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            string path = $"{ZsDir}slingshot_q{q}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { PaOverwriteMesh(m, old); Object.DestroyImmediate(m); m = old; }
            else AssetDatabase.CreateAsset(m, path);
            meshes[q] = m;
            mats[q] = ZsMat(q, texSrc);
            counts.Add((t2.Length / 3).ToString("N0"));
        }
        Object.DestroyImmediate(texSrc);

        // 프리팹: LOD 묶음 + 갈래 끝 표시
        var root = new GameObject("SlingshotModel");
        try
        {
            var lods = new LOD[4]; var tiers = new Renderer[4];
            for (int l = 0; l < 4; l++)
            {
                int q = 3 - l;
                var go = new GameObject("q" + q);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[q];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats[q];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                lods[l] = new LOD(TripoLodHeights[l], new Renderer[] { mr });
                tiers[q] = mr;
            }
            var lg = root.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None; lg.SetLODs(lods); lg.RecalculateBounds();
            var ql = root.AddComponent<QualityLod>(); ql.tiers = tiers; ql.heights = (float[])TripoLodHeights.Clone();
            new GameObject("TipL").transform.SetParent(root.transform, false); root.transform.Find("TipL").localPosition = tl;
            new GameObject("TipR").transform.SetParent(root.transform, false); root.transform.Find("TipR").localPosition = tr;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        return $"새총: 위 축 {ax[0]}{(flip ? "(뒤집음)" : "")}, 배율 {k:F3}, 갈래 끝 {tl.ToString("F3")} {tr.ToString("F3")}, 삼각형 {string.Join("·", counts)} → {prefabPath}";
    }

    static Material ZsMat(int q, Texture2D src)
    {
        string path = $"{ZsDir}M_slingshot_q{q}.mat";
        string texPath = $"{ZsDir}slingshot_q{q}_diff.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            int size = ZgTex[q];
            int mip = Mathf.Max(0, Mathf.RoundToInt(Mathf.Log(src.width / (float)size, 2f)));
            int mw = Mathf.Max(1, src.width >> mip), mh = Mathf.Max(1, src.height >> mip);
            var t = new Texture2D(mw, mh, TextureFormat.RGBA32, false); t.SetPixels32(src.GetPixels32(mip)); t.Apply();
            File.WriteAllBytes(texPath, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            ti.maxTextureSize = size; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed;
            ti.alphaSource = TextureImporterAlphaSource.None; ti.mipmapLimitGroupName = q == 3 ? HighResGroup : "";
            ti.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", tex); mat.SetTexture("_MainTex", tex);
        mat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f)); mat.SetFloat("_Smoothness", 0.25f); mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
