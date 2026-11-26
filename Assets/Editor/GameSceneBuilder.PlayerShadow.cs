using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 플레이어 그림자 몸 (2026-11-24, 사용자 "통과, 그림자 몸부터"): 새 플레이어 모델(Tripo)을 그림자 전용 몸으로 쓴다.
// - 메시: 품질 단계 q0(6천)·q1(2.4만)을 그대로 쓰되, 팔·손을 뼈 줄기 쪽으로 가늘게 줄인 사본.
//   그림자 몸의 팔은 1인칭 팔과 같은 자리에 겹치므로 1인칭 소매보다 굵으면 1인칭 팔에 그림자 자국이 진다 (옛 캡슐 몸도 팔만 가늘게 했다)
// - 프리팹: Resources/Player/player_shadow_body.prefab = Tripo 뼈대(뼈 67개) + 그림자 메시 2판. 애니메이터는 없다 (PlayerShadowBody가 매 프레임 뼈를 돌린다)
// 몇 판을 쓸지는 실행 중에 세부 묘사 품질로 고른다 (PlayerShadowBody)
public static partial class GameSceneBuilder
{
    const string PsOut = "Assets/Resources/Player/player_shadow_body.prefab";
    const string PsMeshDir = PmDir + "_그림자";

    // 뼈 줄기 쪽으로 줄이는 비율 (1 = 그대로). 1인칭 소매 반지름 0.044~0.061 m, 모델 아래팔 반지름 ≈ 0.062 m(키 1.8 m 기준)
    static float PsSlim(string bone)
    {
        if (bone.EndsWith("_UpperArm")) return 0.72f;
        if (bone.EndsWith("_LowerArm")) return 0.55f;
        if (bone.EndsWith("_Hand")) return 0.6f;
        if (bone.Contains("Thumb") || bone.Contains("Index") || bone.Contains("Middle") || bone.Contains("Ring") || bone.Contains("Pinky")) return 0.7f;
        return 1f;
    }

    // 뼈 줄기의 끝 뼈 (팔·손·손가락만)
    static string PsChild(string bone)
    {
        if (bone.EndsWith("_UpperArm")) return bone.Replace("_UpperArm", "_LowerArm");
        if (bone.EndsWith("_LowerArm")) return bone.Replace("_LowerArm", "_Hand");
        if (bone.EndsWith("_Hand")) return bone.Replace("_Hand", "_MiddleProximal");
        if (bone.EndsWith("Proximal")) return bone.Replace("Proximal", "Intermediate");
        if (bone.EndsWith("Intermediate")) return bone.Replace("Intermediate", "Distal");
        if (bone.EndsWith("Distal")) return bone.Replace("Distal", "DistalEnd");
        return null;
    }

    [MenuItem("Tools/핵폭발 생존/플레이어 모델: 그림자 몸 프리팹 만들기")]
    static void PsBuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[GameSceneBuilder] 그림자 몸: " + PsBuild());
    }

    public static string PsBuild()
    {
        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PmRig);
        if (rigAsset == null) return "리깅 FBX 없음";
        var log = new List<string>();
        if (!AssetDatabase.IsValidFolder(PsMeshDir)) AssetDatabase.CreateFolder(PmDir.TrimEnd('/'), "_그림자");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Player")) AssetDatabase.CreateFolder("Assets/Resources", "Player");

        var pf = Object.Instantiate(rigAsset); pf.name = "player_shadow_body";
        var anim = pf.GetComponent<Animator>(); if (anim != null) Object.DestroyImmediate(anim);
        var baseSmr = pf.GetComponentInChildren<SkinnedMeshRenderer>();
        var bones = baseSmr.bones; var rootBone = baseSmr.rootBone; var lt = baseSmr.transform;
        var names = new string[bones.Length];
        for (int i = 0; i < bones.Length; i++) names[i] = bones[i].name;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(PsMeshDir + "/M_player_shadow.mat");
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, PsMeshDir + "/M_player_shadow.mat"); }
        mat.SetColor("_BaseColor", new Color(0.2f, 0.22f, 0.18f)); EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);

        for (int q = 0; q <= 1; q++)
        {
            var src = AssetDatabase.LoadAssetAtPath<Mesh>($"{PmDir}{LpTierDir[q]}/player_survivor_02_q{q}.asset");
            if (src == null) { Object.DestroyImmediate(pf); return $"q{q} 메시 없음 (먼저 '품질 4단계')"; }
            var m = Object.Instantiate(src); m.name = "player_shadow_q" + q;
            int moved = PsSlimArms(m, names);
            string path = $"{PsMeshDir}/player_shadow_q{q}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { PaOverwriteMesh(m, old); Object.DestroyImmediate(m); m = old; }
            else AssetDatabase.CreateAsset(m, path);

            var go = new GameObject("Shadow_q" + q);
            go.transform.SetParent(lt.parent, false);
            go.transform.localPosition = lt.localPosition; go.transform.localRotation = lt.localRotation; go.transform.localScale = lt.localScale;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = m; smr.bones = bones; smr.rootBone = rootBone; smr.sharedMaterial = mat;
            smr.quality = q == 1 ? SkinQuality.Bone4 : SkinQuality.Bone2;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            smr.receiveShadows = false;
            smr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            smr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            smr.skinnedMotionVectors = false;
            smr.localBounds = PmRootBounds(smr, m);
            smr.enabled = false; // PlayerShadowBody가 품질에 맞는 한 판만 켠다
            log.Add($"q{q} 삼각형 {m.triangles.Length / 3:N0}, 팔 꼭짓점 {moved:N0}개 가늘게");
        }
        Object.DestroyImmediate(baseSmr.gameObject);
        PrefabUtility.SaveAsPrefabAsset(pf, PsOut);
        Object.DestroyImmediate(pf);
        return string.Join(" / ", log) + " → " + PsOut;
    }

    // 컷씬 몸 (2026-11-24, 사용자 "통과, 컷씬으로" → "탐험 차림 장면만"): 인트로 장면 3·해치 귀환의 배우가 쓰는 플레이어 모델.
    // Resources/Player/player_body.prefab = Tripo 뼈대 + 품질 4판 스킨 메시(Body_q0..q3, 모두 꺼 둠: CharacterModels.SkinnedPlayer가 하나만 켠다).
    // 애니메이터·LOD 묶음은 없다 (SkinnedPlayerDriver가 매 프레임 뼈를 돌린다)
    const string PbOut = "Assets/Resources/Player/player_body.prefab";

    [MenuItem("Tools/핵폭발 생존/플레이어 모델: 컷씬 몸 프리팹 만들기")]
    static void PbBuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[GameSceneBuilder] 컷씬 몸: " + PbBuild());
    }

    [MenuItem("Tools/핵폭발 생존/플레이어 모델: 컷씬 얼굴 몸(집 차림) 프리팹 만들기")]
    static void PbBuildCivilianMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[GameSceneBuilder] 컷씬 얼굴 몸: " + PbBuildFor("player_civilian_01", "Assets/Resources/Player/player_civilian_body.prefab"));
    }

    public static string PbBuild() => PbBuildFor("player_survivor_02", PbOut);

    // name 모델(Assets/ThirdParty/Tripo/Player/name/)의 품질 4판을 한 프리팹으로 (outPath: Resources 안 경로)
    public static string PbBuildFor(string name, string outPath) => PbBuildFor(name, outPath, "Assets/ThirdParty/Tripo/Player/");

    // baseDir 아래 name 모델 (감염자: Assets/ThirdParty/Tripo/Enemies/ → Resources/Enemies/ZombieCommon/)
    public static string PbBuildFor(string name, string outPath, string baseDir)
    {
        string dir = baseDir + name + "/";
        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "_리깅5만/" + name + "_rig50k.fbx");
        if (rigAsset == null) return "리깅 FBX 없음";
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Player")) AssetDatabase.CreateFolder("Assets/Resources", "Player");
        string outDir = System.IO.Path.GetDirectoryName(outPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(outDir)) return "출력 폴더 없음: " + outDir;
        var meshes = new Mesh[4]; var mats = new Material[4];
        for (int q = 0; q < 4; q++)
        {
            meshes[q] = AssetDatabase.LoadAssetAtPath<Mesh>(q == 3 ? dir + name + "_q3_skinned.asset" : $"{dir}{LpTierDir[q]}/{name}_q{q}.asset");
            mats[q] = AssetDatabase.LoadAssetAtPath<Material>($"{dir}{LpTierDir[q]}/M_{name}_q{q}.mat");
            if (meshes[q] == null || mats[q] == null) return $"q{q} 메시·재질 없음 (먼저 '품질 4단계')";
        }
        var pf = Object.Instantiate(rigAsset); pf.name = System.IO.Path.GetFileNameWithoutExtension(outPath);
        var anim = pf.GetComponent<Animator>(); if (anim != null) Object.DestroyImmediate(anim);
        var baseSmr = pf.GetComponentInChildren<SkinnedMeshRenderer>();
        var bones = baseSmr.bones; var rootBone = baseSmr.rootBone; var lt = baseSmr.transform;
        var log = new List<string>();
        for (int q = 0; q < 4; q++)
        {
            var go = new GameObject("Body_q" + q);
            go.transform.SetParent(lt.parent, false);
            go.transform.localPosition = lt.localPosition; go.transform.localRotation = lt.localRotation; go.transform.localScale = lt.localScale;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = meshes[q]; smr.bones = bones; smr.rootBone = rootBone; smr.sharedMaterial = mats[q];
            smr.quality = q >= 2 ? SkinQuality.Bone4 : SkinQuality.Bone2;
            smr.localBounds = PmRootBounds(smr, meshes[q]);
            smr.enabled = false;
            log.Add($"q{q} {meshes[q].triangles.Length / 3:N0}");
        }
        Object.DestroyImmediate(baseSmr.gameObject);
        PrefabUtility.SaveAsPrefabAsset(pf, outPath);
        Object.DestroyImmediate(pf);
        return "삼각형 " + string.Join(" · ", log) + " → " + outPath;
    }

    // 팔·손·손가락 뼈에 붙은 꼭짓점을 그 뼈 줄기 쪽으로 당긴다 (바인드 자세에서, 줄기 방향 길이는 그대로). 움직인 꼭짓점 수
    static int PsSlimArms(Mesh m, string[] names)
    {
        var bp = m.bindposes; int nb = bp.Length;
        var a = new Vector3[nb]; var b = new Vector3[nb]; var f = new float[nb];
        for (int i = 0; i < nb; i++)
        {
            a[i] = bp[i].inverse.MultiplyPoint3x4(Vector3.zero);
            f[i] = PsSlim(names[i]);
            string c = PsChild(names[i]); int ci = c != null ? System.Array.IndexOf(names, c) : -1;
            b[i] = ci >= 0 ? bp[ci].inverse.MultiplyPoint3x4(Vector3.zero) : a[i];
        }
        var v = m.vertices; var w = m.boneWeights; int moved = 0;
        for (int k = 0; k < v.Length; k++)
        {
            var bw = w[k]; var d = Vector3.zero;
            d += PsPull(v[k], bw.boneIndex0, bw.weight0, a, b, f);
            d += PsPull(v[k], bw.boneIndex1, bw.weight1, a, b, f);
            d += PsPull(v[k], bw.boneIndex2, bw.weight2, a, b, f);
            d += PsPull(v[k], bw.boneIndex3, bw.weight3, a, b, f);
            if (d.sqrMagnitude > 1e-12f) { v[k] += d; moved++; }
        }
        m.vertices = v; m.RecalculateBounds();
        return moved;
    }

    static Vector3 PsPull(Vector3 p, int bi, float wt, Vector3[] a, Vector3[] b, float[] f)
    {
        if (wt <= 0f || f[bi] >= 1f) return Vector3.zero;
        var ab = b[bi] - a[bi];
        float t = ab.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a[bi], ab) / ab.sqrMagnitude) : 0f;
        var axis = a[bi] + ab * t;
        return (axis - p) * (1f - f[bi]) * wt;
    }
}
