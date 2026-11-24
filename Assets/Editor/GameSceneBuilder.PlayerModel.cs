using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 플레이어 모델 (2026-11-24, 사용자 "품질 단계·마스크 고치기"). Tripo 30만 T자세 모델 + Tripo 5만 리깅(뼈 67개, Unity Humanoid).
// 1) 마스크 고치기: 후드 안 턱·목 둘레에 비치는 빨간 살/안감(검수 11-24)을 텍스처에서 어둡게 칠한다 (얼굴 없는 플레이어)
// 2) 품질 4단계 (모두 같은 뼈대·바인드 포즈, 스킨 메시):
//    매우 높음 q3 = 30만 원본에 Tripo 가중치를 옮긴 메시(PlayerModelTools) + 텍스처 2048(HighRes 묶음)
//    높음 q2 = Tripo 5만 리깅 메시 그대로 + 리깅 텍스처 1024
//    중간 q1·낮음 q0 = 5만 리깅 메시를 이음매를 지키며 줄인 것(꼭짓점을 옮기지 않고 지우기만 해서 남은 꼭짓점의 가중치가 그대로) + 512·256
// 결과: player_survivor_02/Q0_낮음..Q3_매우높음/, player_survivor_02.prefab (Animator·아바타, LOD 묶음, QualityLod)
public static partial class GameSceneBuilder
{
    const string PmDir = "Assets/ThirdParty/Tripo/Player/player_survivor_02/";
    const string PmRig = PmDir + "_리깅5만/player_survivor_02_rig50k.fbx";
    static readonly int[] PmTargets = { 6000, 24000 };      // 낮음·중간 삼각형 (높음 9.5만은 리깅 메시 그대로, 매우 높음 29만)
    static readonly int[] PmTexSize = { 256, 512, 1024, 2048 };

    [MenuItem("Tools/핵폭발 생존/플레이어 모델: 마스크 아래 빨간 곳 칠하기 + 품질 4단계")]
    static void PmBuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[GameSceneBuilder] 플레이어 모델: " + PmBuild());
    }

    // 지금 만드는 모델 (PmBuildFor가 바꾼다). PmDir·PmRig 상수는 방독면 모델(1인칭 팔·그림자 몸 도구가 쓴다)
    static string pmCur = "player_survivor_02";
    static bool pmDarkenMask = true;
    static string PmCurDir => "Assets/ThirdParty/Tripo/Player/" + pmCur + "/";

    [MenuItem("Tools/핵폭발 생존/플레이어 모델: 얼굴 모델(집 차림) 품질 4단계")]
    static void PmBuildCivilianMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[GameSceneBuilder] 얼굴 모델: " + PmBuildFor("player_civilian_01", false));
    }

    public static string PmBuild() => PmBuildFor("player_survivor_02", true);

    // name 모델의 품질 4단계. darkenMask: 방독면 아래 빨간 살 칠하기 (방독면 모델만)
    public static string PmBuildFor(string name, bool darkenMask)
    {
        pmCur = name; pmDarkenMask = darkenMask;
        try { return PmBuildCur(); }
        finally { pmCur = "player_survivor_02"; pmDarkenMask = true; }
    }

    static string PmBuildCur()
    {
        var log = new List<string>();
        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PmCurDir + "_리깅5만/" + pmCur + "_rig50k.fbx");
        var q3Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PmCurDir + pmCur + "_q3_skinned.asset");
        if (rigAsset == null || q3Mesh == null) return "리깅 FBX 또는 30만 스킨 메시 없음 (먼저 'Tripo 가중치를 30만 메시로 옮기기')";
        var rigSmr = rigAsset.GetComponentInChildren<SkinnedMeshRenderer>();
        var rigMesh = rigSmr.sharedMesh;
        // 리깅 메시 로컬 → 바로 선 자세 (smr 회전 X 270)
        var up = rigSmr.transform.rotation;

        // ---- 1) 텍스처 고치기 ----
        var srcHi = PmLoadTex(PmCurDir + pmCur + "_tex_basecolor.jpg");
        int nHi = pmDarkenMask ? PmDarkenUnderMask(srcHi, q3Mesh, up) : 0;
        var srcLo = PmLoadTex(PmCurDir + "_리깅5만/" + pmCur + "_rig50k_basecolor.jpg");
        int nLo = pmDarkenMask ? PmDarkenUnderMask(srcLo, rigMesh, up) : 0;
        log.Add($"빨간 곳 칠함: 30만 텍스처 {nHi}화소, 리깅 텍스처 {nLo}화소 ({srcHi.width}·{srcLo.width})");

        // ---- 2) 품질 4단계 ----
        var meshes = new Mesh[4]; var texs = new Texture2D[4];
        // q3
        meshes[3] = q3Mesh;
        texs[3] = PmSaveTex(srcHi, 3);
        // q2 = 리깅 메시 사본 (FBX 안의 메시는 따로 저장해야 프리팹이 FBX 들여오기에 묶이지 않는다)
        meshes[2] = PmSaveMesh(Object.Instantiate(rigMesh), 2);
        texs[2] = PmSaveTex(srcLo, 2);
        // q1·q0 = 줄이기
        var pos = rigMesh.vertices; var uv = rigMesh.uv; var nrm = rigMesh.normals; var bw = rigMesh.boneWeights; var tri = rigMesh.triangles;
        for (int q = 1; q >= 0; q--)
        {
            var simp = new TtSimplifier(pos, uv, tri, false, null);
            simp.Run(PmTargets[q]);
            var idx = simp.Triangles();
            var remap = new Dictionary<int, int>(); var p2 = new List<Vector3>(); var n2 = new List<Vector3>(); var u2 = new List<Vector2>(); var w2 = new List<BoneWeight>();
            var t2 = new int[idx.Length];
            for (int i = 0; i < idx.Length; i++)
            {
                if (!remap.TryGetValue(idx[i], out int w))
                {
                    w = p2.Count; remap[idx[i]] = w;
                    p2.Add(pos[idx[i]]); n2.Add(simp.Normal(idx[i], nrm[idx[i]])); u2.Add(uv[idx[i]]); w2.Add(bw[idx[i]]);
                }
                t2[i] = w;
            }
            var m = new Mesh { name = pmCur + "_q" + q, indexFormat = p2.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(p2); m.SetNormals(n2); m.SetUVs(0, u2); m.SetTriangles(t2, 0); m.boneWeights = w2.ToArray(); m.bindposes = rigMesh.bindposes;
            m.RecalculateBounds(); m.RecalculateTangents();
            meshes[q] = PmSaveMesh(m, q);
            texs[q] = PmSaveTex(srcLo, q);
            log.Add($"q{q} 삼각형 {t2.Length / 3:N0} (목표 {PmTargets[q]:N0})");
        }
        log.Insert(1, $"q3 {q3Mesh.triangles.Length / 3:N0} · q2 {rigMesh.triangles.Length / 3:N0}");
        Object.DestroyImmediate(srcHi); Object.DestroyImmediate(srcLo);

        // ---- 프리팹: Tripo 뼈대 + LOD 4판 ----
        var pf = Object.Instantiate(rigAsset); pf.name = pmCur;
        var baseSmr = pf.GetComponentInChildren<SkinnedMeshRenderer>();
        var bones = baseSmr.bones; var rootBone = baseSmr.rootBone; var parent = baseSmr.transform.parent; var lt = baseSmr.transform;
        var lods = new LOD[4]; var tiers = new Renderer[4];
        for (int l = 0; l < 4; l++)
        {
            int q = 3 - l;
            var go = new GameObject($"LOD{l}_q{q}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lt.localPosition; go.transform.localRotation = lt.localRotation; go.transform.localScale = lt.localScale;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = meshes[q]; smr.bones = bones; smr.rootBone = rootBone;
            smr.sharedMaterial = PmMat(q, texs[q]);
            smr.localBounds = PmRootBounds(smr, meshes[q]);
            smr.quality = q >= 2 ? SkinQuality.Bone4 : SkinQuality.Bone2;
            lods[l] = new LOD(TripoLodHeights[l], new Renderer[] { smr });
            tiers[q] = smr;
        }
        Object.DestroyImmediate(baseSmr.gameObject);
        var lg = pf.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None; lg.SetLODs(lods); lg.RecalculateBounds();
        var ql = pf.AddComponent<QualityLod>(); ql.tiers = tiers; ql.heights = (float[])TripoLodHeights.Clone();
        PrefabUtility.SaveAsPrefabAsset(pf, PmCurDir + pmCur + ".prefab");
        Object.DestroyImmediate(pf);
        return string.Join(" / ", log);
    }

    static Texture2D PmLoadTex(string path)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, true); t.LoadImage(File.ReadAllBytes(path)); return t;
    }

    // 머리 둘레(바로 선 자세 높이 0.74 이상, 몸 가운데 ±0.14) 꼭짓점 중 빨간 꼭짓점이 하나라도 있는 면을 UV로 칠할 영역으로 삼고,
    // 그 안의 빨간 화소만 어두운 고무색으로 바꾼다 (밝기는 조금 남겨 결이 죽지 않게). 바뀐 화소 수
    static int PmDarkenUnderMask(Texture2D tex, Mesh mesh, Quaternion up)
    {
        var v = mesh.vertices; var uv = mesh.uv; var t = mesh.triangles;
        float ymax = float.MinValue; foreach (var p in v) ymax = Mathf.Max(ymax, (up * p).y);
        System.Func<Color, bool> Red = c => c.r > 0.16f && c.r > c.g * 1.35f && c.r > c.b * 1.35f;
        var redV = new bool[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var w = up * v[i];
            if (w.y < ymax * 0.74f || Mathf.Abs(w.x) > ymax * 0.14f) continue;
            redV[i] = Red(tex.GetPixelBilinear(uv[i].x, uv[i].y));
        }
        int W = tex.width, H = tex.height; var mask = new bool[W * H];
        for (int k = 0; k < t.Length; k += 3)
        {
            if (!redV[t[k]] && !redV[t[k + 1]] && !redV[t[k + 2]]) continue;
            Vector2 a = Vector2.Scale(uv[t[k]], new Vector2(W, H)), b = Vector2.Scale(uv[t[k + 1]], new Vector2(W, H)), c = Vector2.Scale(uv[t[k + 2]], new Vector2(W, H));
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 3), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 3);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 3), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 3);
            if ((x1 - x0) * (y1 - y0) > 40000) continue; // UV가 넓게 퍼진 면은 건너뛴다
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) mask[y * W + x] = true; // 면 상자 + 3화소 (작은 면이라 충분)
        }
        var px = tex.GetPixels32(); int n = 0;
        for (int i = 0; i < px.Length; i++)
        {
            if (!mask[i]) continue;
            Color c = px[i];
            if (!Red(c)) continue;
            float lum = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            float g = Mathf.Clamp(0.05f + lum * 0.35f, 0.04f, 0.14f);
            px[i] = new Color(g * 0.95f, g, g * 1.02f, c.a); n++;
        }
        tex.SetPixels32(px); tex.Apply(true);
        return n;
    }

    static Texture2D PmSaveTex(Texture2D src, int q)
    {
        int size = PmTexSize[q];
        int mip = Mathf.Max(0, Mathf.RoundToInt(Mathf.Log(src.width / (float)size, 2f)));
        int mw = Mathf.Max(1, src.width >> mip), mh = Mathf.Max(1, src.height >> mip);
        var t = new Texture2D(mw, mh, TextureFormat.RGBA32, false); t.SetPixels32(src.GetPixels32(mip)); t.Apply();
        string sub = PmCurDir + LpTierDir[q];
        if (!AssetDatabase.IsValidFolder(sub)) AssetDatabase.CreateFolder(PmCurDir.TrimEnd('/'), LpTierDir[q]);
        string path = $"{sub}/{pmCur}_q{q}_diff.png";
        File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        ti.maxTextureSize = size; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed;
        ti.alphaSource = TextureImporterAlphaSource.None; ti.mipmapLimitGroupName = q == 3 ? HighResGroup : "";
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Mesh PmSaveMesh(Mesh m, int q)
    {
        string sub = PmCurDir + LpTierDir[q];
        if (!AssetDatabase.IsValidFolder(sub)) AssetDatabase.CreateFolder(PmCurDir.TrimEnd('/'), LpTierDir[q]);
        string path = $"{sub}/{pmCur}_q{q}.asset";
        m.name = pmCur + "_q" + q;
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { PaOverwriteMesh(m, old); Object.DestroyImmediate(m); return old; }
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Material PmMat(int q, Texture2D tex)
    {
        string path = $"{PmCurDir}{LpTierDir[q]}/M_{pmCur}_q{q}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f)); m.SetFloat("_Smoothness", 0.22f); m.SetFloat("_Metallic", 0.05f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 화면 밖 판정 상자: 뿌리 뼈 기준, 크게 굽혀도 들어가게 넉넉히 (PlayerModelTools와 같다)
    static Bounds PmRootBounds(SkinnedMeshRenderer smr, Mesh mesh)
    {
        var root = smr.rootBone != null ? smr.rootBone : smr.transform;
        var mb = mesh.bounds; var rb = new Bounds(root.InverseTransformPoint(smr.transform.TransformPoint(mb.center)), Vector3.zero);
        for (int k = 0; k < 8; k++)
            rb.Encapsulate(root.InverseTransformPoint(smr.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)))));
        rb.Expand(rb.size.magnitude * 0.35f);
        return rb;
    }
}
