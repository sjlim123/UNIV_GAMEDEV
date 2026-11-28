using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 약탈자의 못 박힌 야구방망이 (2026-11-28, 사용자 "방망이 3"): Tripo 모델 (Assets/ThirdParty/Tripo/Enemies/nailbat/nailbat.fbx, 길이 1 단위)을
// 방망이 기준 좌표로 옮긴다: y = 손잡이 → 굵은 끝(못 박힌 쪽), 원점 = 손잡이 끝(노브) 바로 위 주먹이 쥐는 곳, 전체 길이 ZbLength m.
// 품질 4단계(TtSimplifier, 텍스처 128·256·512·1024)를 LOD 묶음 + QualityLod로 묶는다.
// 게임은 Resources/Enemies/Raider/nailbat.prefab을 오른 주먹(rig.GripR)에 꽂는다 (ZombieLooks.BuildBat).
public static partial class GameSceneBuilder
{
    const string ZbDir = "Assets/ThirdParty/Tripo/Enemies/nailbat/";
    const float ZbLength = 0.86f;          // 노브 끝에서 굵은 끝까지 (m, 예전 코드 방망이와 같다)
    const float ZbGripAt = 0.09f;          // 주먹 가운데 = 노브 끝에서 길이의 9% (검은 테이프 아래쪽)
    static readonly int[] ZbTargets = { 1200, 3000, 8000, 20000 };

    public static string ZbBuild(string prefabPath)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ZbDir + "nailbat.fbx");
        if (asset == null) return "nailbat.fbx 없음";
        var mf = asset.GetComponentInChildren<MeshFilter>();
        var src = mf.sharedMesh;
        var M = mf.transform.localToWorldMatrix;
        var sv = src.vertices; var sn = src.normals; var uv = src.uv; var tri = src.triangles;
        var w = new Vector3[sv.Length];
        var b = new Bounds();
        for (int i = 0; i < sv.Length; i++) { w[i] = M.MultiplyPoint3x4(sv[i]); if (i == 0) b = new Bounds(w[i], Vector3.zero); else b.Encapsulate(w[i]); }

        // 축: 가장 긴 축 = 방망이 길이. 못이 박힌 굵은 끝이 위(+y)
        var ext = b.size; var ax = new[] { 0, 1, 2 };
        System.Array.Sort(ax, (p, q) => ext[q].CompareTo(ext[p]));
        Vector3 Axis(int a) { var v = Vector3.zero; v[a] = 1; return v; }
        Vector3 ey = Axis(ax[0]), ex = Axis(ax[1]);
        float H = ext[ax[0]], lo = b.min[ax[0]];
        float WidthAt(float t0, float t1)
        {
            float mn1 = float.MaxValue, mx1 = float.MinValue, mn2 = float.MaxValue, mx2 = float.MinValue;
            foreach (var p in w)
            {
                float t = (p[ax[0]] - lo) / H; if (t < t0 || t > t1) continue;
                mn1 = Mathf.Min(mn1, p[ax[1]]); mx1 = Mathf.Max(mx1, p[ax[1]]); mn2 = Mathf.Min(mn2, p[ax[2]]); mx2 = Mathf.Max(mx2, p[ax[2]]);
            }
            return (mx1 - mn1) + (mx2 - mn2);
        }
        bool flip = WidthAt(0f, 0.3f) > WidthAt(0.6f, 1f);
        if (flip) ey = -ey;
        var ez = Vector3.Cross(ex, ey);

        // 원점: 노브 끝에서 ZbGripAt 높이의 단면 중심
        float gripH = flip ? b.max[ax[0]] - ZbGripAt * H : lo + ZbGripAt * H;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var p in w) if (Mathf.Abs(p[ax[0]] - gripH) < 0.02f * H) { sum += p; n++; }
        var o = n > 0 ? sum / n : b.center;
        o[ax[0]] = gripH;
        float k = ZbLength / H;

        var pos = new Vector3[w.Length]; var nrm = new Vector3[w.Length];
        for (int i = 0; i < w.Length; i++)
        {
            var d = w[i] - o;
            pos[i] = new Vector3(Vector3.Dot(d, ex), Vector3.Dot(d, ey), Vector3.Dot(d, ez)) * k;
            var nn = M.MultiplyVector(sn[i]);
            nrm[i] = new Vector3(Vector3.Dot(nn, ex), Vector3.Dot(nn, ey), Vector3.Dot(nn, ez)).normalized;
        }

        var texSrc = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        texSrc.LoadImage(File.ReadAllBytes(ZbDir + "nailbat_basecolor.jpg"));
        var meshes = new Mesh[4]; var mats = new Material[4]; var counts = new List<string>();
        for (int q = 0; q < 4; q++)
        {
            var simp = new TtSimplifier(pos, uv, tri, false, null);
            simp.Run(ZbTargets[q]);
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
            var m = new Mesh { name = $"nailbat_q{q}", indexFormat = p2.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(p2); m.SetNormals(n2); m.SetUVs(0, u2); m.SetTriangles(t2, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            string path = $"{ZbDir}nailbat_q{q}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { PaOverwriteMesh(m, old); Object.DestroyImmediate(m); m = old; }
            else AssetDatabase.CreateAsset(m, path);
            meshes[q] = m;
            mats[q] = ZbMat(q, texSrc);
            counts.Add((t2.Length / 3).ToString("N0"));
        }
        Object.DestroyImmediate(texSrc);

        var root = new GameObject("NailBatModel");
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
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        return $"방망이: 길이 축 {ax[0]}{(flip ? "(뒤집음)" : "")}, 배율 {k:F3}, 삼각형 {string.Join("·", counts)} → {prefabPath}";
    }

    static Material ZbMat(int q, Texture2D src)
    {
        string path = $"{ZbDir}M_nailbat_q{q}.mat";
        string texPath = $"{ZbDir}nailbat_q{q}_diff.png";
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
        mat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f)); mat.SetFloat("_Smoothness", 0.3f); mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
