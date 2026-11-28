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

    // ---- 가죽 감염자 (2026-11-27): 사용자 "갑바 정도의 군복" → 감염된 군인 몸 a·b(그림 5·7) + 떨어지는 장비 4종 (GameSceneBuilder.ZombieGear.cs) ----
    public const string LeatherOutDir = "Assets/Resources/Enemies/ZombieLeather/";
    public static readonly string[] LeatherNames = { "zombie_leather_a", "zombie_leather_b" };

    [MenuItem("Tools/핵폭발 생존/가죽 감염자 Tripo 모델: 몸 a·b + 장비 맞춰 붙이기")]
    static void BuildLeatherMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[ZombieTripoBuild] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[ZombieTripoBuild] " + BuildLeather());
    }

    public static string BuildLeather()
    {
        if (!AssetDatabase.IsValidFolder(LeatherOutDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Resources/Enemies", "ZombieLeather");
        var log = new System.Text.StringBuilder();
        foreach (var n in LeatherNames)
        {
            log.Append(Build(n, LeatherOutDir)).Append(" / 장비: ");
            log.Append(GameSceneBuilder.ZgBuild(n, LeatherOutDir + n + ".prefab")).Append(" || ");
        }
        return log.ToString();
    }

    // ---- 원거리 감염자 (2026-11-27): 사용자 "2와 4"(몸 a·b), "새총 4번" → 후드 쓴 마른 감염자 + Tripo Y자 새총 틀 (GameSceneBuilder.ZombieSlingshot.cs) ----
    public const string RangedOutDir = "Assets/Resources/Enemies/ZombieRanged/";
    public static readonly string[] RangedNames = { "zombie_ranged_a", "zombie_ranged_b" };

    [MenuItem("Tools/핵폭발 생존/원거리 감염자 Tripo 모델: 몸 a·b + 새총")]
    static void BuildRangedMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[ZombieTripoBuild] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[ZombieTripoBuild] " + BuildRanged());
    }

    public static string BuildRanged()
    {
        if (!AssetDatabase.IsValidFolder(RangedOutDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Resources/Enemies", "ZombieRanged");
        var log = new System.Text.StringBuilder();
        foreach (var n in RangedNames) log.Append(Build(n, RangedOutDir)).Append(" || ");
        log.Append(BuildSlingshot());
        return log.ToString();
    }

    public static string BuildSlingshot()
    {
        SetupImport(Base + "slingshot/slingshot.fbx", false);
        return GameSceneBuilder.ZsBuild(RangedOutDir + "slingshot.prefab");
    }

    // ---- 거대 감염자 (2026-11-28): 사용자 "5678 스타일로", "10 14번 좋네요" → 공사장 작업복 변이 거인 a(그림 10)·b(그림 14 좌우 반전) ----
    public const string HeavyOutDir = "Assets/Resources/Enemies/ZombieHeavy/";
    public static readonly string[] HeavyNames = { "zombie_heavy_a", "zombie_heavy_b" };

    [MenuItem("Tools/핵폭발 생존/거대 감염자 Tripo 모델: 몸 a·b")]
    static void BuildHeavyMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[ZombieTripoBuild] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[ZombieTripoBuild] " + BuildHeavy());
    }

    public static string BuildHeavy()
    {
        if (!AssetDatabase.IsValidFolder(HeavyOutDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Resources/Enemies", "ZombieHeavy");
        var log = new System.Text.StringBuilder();
        foreach (var n in HeavyNames) log.Append(Build(n, HeavyOutDir)).Append(" || ");
        return log.ToString();
    }

    public static string Build(string name) => Build(name, OutDir);

    public static string Build(string name, string outDir)
    {
        string dir = Base + name + "/";
        SetupImport(dir + name + "_tex.fbx", false);
        SetupImport(dir + "_리깅5만/" + name + "_rig50k.fbx", true);
        string t = PlayerModelTools.TransferFor(name, Base, Fit);
        string q = GameSceneBuilder.PmBuildFor(name, false, Base);
        string src = dir + name + ".prefab", dst = outDir + name + ".prefab";
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
