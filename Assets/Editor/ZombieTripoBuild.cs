using UnityEditor;
using UnityEngine;

// 감염자 Tripo 모델 (2026-11-26, 사용자 "힉스필드에서 Tripo로 … 지금까지 만든 것을 Tripo로 다시", "5번 8번 둘 다").
// 그림 5 → zombie_common_a, 그림 8 → zombie_common_b (Assets/ThirdParty/Tripo/Enemies/, 출처는 그 폴더 SOURCE.md).
// 플레이어 모델과 같은 길: Tripo 30만 원본 + Tripo 5만 리깅(뼈 67개) → 가중치를 30만으로 옮김(PlayerModelTools, 리깅 메시가 0.98배라 맞춰 줄임)
// → 품질 4단계 LOD 프리팹(GameSceneBuilder.PmBuildFor) → Resources/Enemies/ZombieCommon/ 에 사본 (게임이 불러 쓴다: CharacterModels.Skinned.cs)
public static class ZombieTripoBuild
{
    public const string Base = "Assets/ThirdParty/Tripo/Enemies/";
    public const string OutDir = "Assets/Resources/Enemies/ZombieCommon/";
    public static readonly string[] Names = { "zombie_common_a", "zombie_common_b" };
    const float Fit = 0.98f;

    [MenuItem("Tools/핵폭발 생존/감염자 Tripo 모델: 가중치·품질 4단계·프리팹 (a·b)")]
    static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[ZombieTripoBuild] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[ZombieTripoBuild] " + BuildAll());
    }

    public static string BuildAll()
    {
        var log = new System.Text.StringBuilder();
        foreach (var n in Names) log.Append(Build(n)).Append(" || ");
        return log.ToString();
    }

    public static string Build(string name)
    {
        string dir = Base + name + "/";
        SetupImport(dir + name + "_tex.fbx", false);
        SetupImport(dir + "_리깅5만/" + name + "_rig50k.fbx", true);
        string t = PlayerModelTools.TransferFor(name, Base, Fit);
        string q = GameSceneBuilder.PmBuildFor(name, false, Base);
        string src = dir + name + ".prefab", dst = OutDir + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null) return $"{name}: 프리팹 없음 ({t} / {q})";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) != null) AssetDatabase.DeleteAsset(dst);
        AssetDatabase.CopyAsset(src, dst);
        // 게임에서는 Zombie 코드가 뼈를 돌리므로 애니메이터는 뺀다
        var pf = PrefabUtility.LoadPrefabContents(dst);
        var anim = pf.GetComponent<Animator>(); if (anim != null) Object.DestroyImmediate(anim);
        PrefabUtility.SaveAsPrefabAsset(pf, dst);
        PrefabUtility.UnloadPrefabContents(pf);
        AssetDatabase.SaveAssets();
        return $"{name}: {t} / {q} → {dst}";
    }

    // 머티리얼·애니메이션은 들여오지 않는다. 리깅 FBX는 Generic (아바타 없음: 게임 관절이 뼈를 돌린다)
    static void SetupImport(string path, bool rig)
    {
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) return;
        bool dirty = false;
        if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
        if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
        var at = rig ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        if (mi.animationType != at) { mi.animationType = at; dirty = true; }
        if (dirty) mi.SaveAndReimport();
    }
}
