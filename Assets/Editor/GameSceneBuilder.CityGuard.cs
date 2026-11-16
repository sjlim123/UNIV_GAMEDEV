using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 도시 씬만 생성의 재질 중립 장치 (8차 라운드):
//  1) 있는 재질은 불러 쓰기만 한다 (KKeep): 이번에 새로 만든 재질과 KRebuildMaterials에 적은 재질만 값을 쓴다.
//     불러온 재질은 곧바로 메모리의 _EMISSION을 파일에 맞추고 '저장 안 됨' 표시를 지운다 (KSyncKept). URP는 재질을 불러올 때 검증하며
//     m_LightmapFlags 0인 재질의 _EMISSION을 끄고 '저장 안 됨'으로 남긴다 — 그대로 두면 다음에 누가 저장할 때 켜 둔 간판이 꺼진 채 저장된다
//  2) 간판 발광(M_SignLit_*)은 배포판 모습(KGlowingSigns·KGlowingSignGi)과 어긋난 파일만 키워드·표시를 되돌려 저장한다 (KRepairSignGlow).
//     지금 파일은 모두 맞아서 아무것도 쓰지 않는다
//  3) 전체 저장(AssetDatabase.SaveAssets) 대신 도시 결과물(메시 묶음·프로파일)과 이번에 쓴 재질만 저장한다 (KSaveCityAssets)
//  4) 생성 전·뒤 정리: 메모리의 _EMISSION을 파일에 맞추고 재질의 '저장 안 됨' 표시를 지운다 (전체 목록은 Library/CityGuard/preclean_*.txt)
//  5) 지킴 검사: 모든 재질과 밸런스 파일의 사본을 떠 두고, 허용 목록 밖에서 바뀐 재질은 되돌린다 (재질이 아닌 파일은 알리기만 한다). 2초 뒤 한 번 더 본다
//  6) 도시 결과물(씬·메시 묶음·내비메시·프로파일·오클루전)의 사본을 떠 두고, 생성이 도중에 멈추면 만들다 만 씬을 버리고 결과물을 되돌린다
// 게임 씬 전체 생성·방공호·컷씬 메뉴는 예전처럼 재질을 다시 쓰고 전체 저장한다 (kKeepMaterials가 꺼져 있다).
// 이 파일은 Apt102*.cs(아직 커밋 전)에 기대지 않는다: 지킴 도우미를 따로 둔다
public static partial class GameSceneBuilder
{
    static bool kKeepMaterials;
    static readonly HashSet<Material> kCreated = new HashSet<Material>();      // 이번 실행에서 새로 만든 재질 (값을 쓰고 저장한다)
    static readonly HashSet<Material> kWritten = new HashSet<Material>();      // 값을 쓴 재질 (KSaveCityAssets가 저장한다)
    static readonly HashSet<Material> kSignRepaired = new HashSet<Material>(); // 배포판 모습으로 되돌린 간판 재질 (지킴 검사가 받아들인다)
    static readonly List<string> kSyncedOnLoad = new List<string>();           // 불러올 때 메모리의 _EMISSION이 파일과 달라 맞춘 재질

    // 이번 라운드에 일부러 다시 쓰는 재질 파일 이름 (확장자 없이). 지킴 검사는 이 파일들의 변화를 되돌리지 않는다. 8차 라운드: 없음
    static readonly string[] KRebuildMaterials = { };

    // 도시 씬만 생성 중이고 이번에 만든 것도, 다시 쓰라고 적은 것도 아니면 true: 부르는 쪽은 값을 쓰지 않고 그대로 돌려준다
    static bool KKeep(Material m)
    {
        if (m == null) return false;
        bool keep = kKeepMaterials && !kCreated.Contains(m) && System.Array.IndexOf(KRebuildMaterials, m.name) < 0;
        if (!keep) kWritten.Add(m);
        return keep;
    }

    // 그대로 두는 재질을 막 불러온 뒤: 메모리의 _EMISSION을 파일에 맞추고 '저장 안 됨' 표시를 지운다 (파일은 건드리지 않는다)
    static void KSyncKept(Material m)
    {
        string path = AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", System.StringComparison.Ordinal) || !path.EndsWith(".mat", System.StringComparison.Ordinal)) return;
        string file = KGuardRoot + "/" + path;
        if (!File.Exists(file)) return;
        bool disk = KFileValidKeyword(File.ReadAllText(file), "_EMISSION");
        if (disk != m.IsKeywordEnabled("_EMISSION"))
        {
            if (disk) m.EnableKeyword("_EMISSION");
            else m.DisableKeyword("_EMISSION");
            kSyncedOnLoad.Add(m.name);
        }
        if (EditorUtility.IsDirty(m)) EditorUtility.ClearDirty(m);
    }

    // 간판 발광 재질 하나를 배포판 모습으로 되돌려 저장 목록에 넣는다 (도시 씬만 생성에서 파일이 어긋나 있을 때만 부른다)
    static void KRepairSignGlow(Material m, bool glow, MaterialGlobalIlluminationFlags gi)
    {
        if (glow) m.EnableKeyword("_EMISSION");
        else m.DisableKeyword("_EMISSION");
        m.globalIlluminationFlags = gi;
        EditorUtility.SetDirty(m);
        kWritten.Add(m);
        kSignRepaired.Add(m);
        Debug.LogWarning("[GameSceneBuilder] City: 간판 발광 재질을 배포판 모습으로 되돌려 저장합니다: " + m.name + (glow ? " (켬)" : " (끔)"));
    }

    // 바이트가 다를 때만 쓴다. 썼으면 true (같은 그림이면 파일·들여오기가 그대로다)
    static bool KWriteIfChanged(string path, byte[] bytes)
    {
        if (File.Exists(path))
        {
            var old = File.ReadAllBytes(path);
            if (old.Length == bytes.Length && System.Linq.Enumerable.SequenceEqual(old, bytes)) return false;
        }
        File.WriteAllBytes(path, bytes);
        return true;
    }

    // 재질 파일 글에서 m_ValidKeywords 목록에 keyword가 있는가 (m_InvalidKeywords는 보지 않는다)
    static bool KFileValidKeyword(string matText, string keyword)
    {
        int at = matText.IndexOf("m_ValidKeywords:", System.StringComparison.Ordinal);
        if (at < 0) return false;
        int end = matText.IndexOf("m_InvalidKeywords:", at, System.StringComparison.Ordinal);
        string block = end > at ? matText.Substring(at, end - at) : matText.Substring(at);
        return block.Contains("- " + keyword + "\n") || block.Contains("- " + keyword + "\r\n");
    }

    // 파일 글에서 "key 정수" 한 줄의 정수 (없으면 -1)
    static int KFileInt(string text, string key)
    {
        int at = text.IndexOf(key, System.StringComparison.Ordinal);
        if (at < 0) return -1;
        int start = at + key.Length, end = text.IndexOf('\n', start);
        string s = (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).Trim();
        return int.TryParse(s, out int v) ? v : -1;
    }

    // 켜진 간판 재질 파일이 배포판 모습(KGlowingSigns의 _EMISSION, m_LightmapFlags)과 같은지 파일 기준으로 확인만 한다. 어긋난 수를 돌려준다
    static int KCheckSignGlowFiles()
    {
        var wrong = new List<string>();
        for (int i = 0; i < KShops.Length; i++)
        {
            string path = KGuardRoot + "/" + MatDir + "/M_SignLit_" + i + ".mat";
            if (!File.Exists(path)) { wrong.Add("M_SignLit_" + i + " 없음"); continue; }
            string text = File.ReadAllText(path);
            bool glow = System.Array.IndexOf(KGlowingSigns, i) >= 0;
            bool on = KFileValidKeyword(text, "_EMISSION");
            if (on != glow) wrong.Add("M_SignLit_" + i + (on ? " 켜짐" : " 꺼짐"));
            int flags = KFileInt(text, "m_LightmapFlags:");
            if (flags != (int)(glow ? KGlowingSignGi : MaterialGlobalIlluminationFlags.None)) wrong.Add("M_SignLit_" + i + " m_LightmapFlags " + flags);
        }
        if (wrong.Count > 0) Debug.LogError("[GameSceneBuilder] City: 간판 발광 재질이 배포판 모습과 다릅니다 (파일 기준): " + string.Join(", ", wrong));
        else Debug.Log("[GameSceneBuilder] City: 간판 발광 확인 - M_SignLit 1·3·4·10·13번만 켜짐 (파일 기준)");
        return wrong.Count;
    }

    // 같은 확인을 메모리에 올라와 있는 간판 재질에 한다 (새로 불러오지 않는다). 키워드가 다르거나 '저장 안 됨'이면 어긋난 것
    static int KCheckSignGlowMemory(string when)
    {
        var wrong = new List<string>();
        int loaded = 0;
        for (int i = 0; i < KShops.Length; i++)
        {
            string path = MatDir + "/M_SignLit_" + i + ".mat";
            if (!AssetDatabase.IsMainAssetAtPathLoaded(path)) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) continue;
            loaded++;
            bool glow = System.Array.IndexOf(KGlowingSigns, i) >= 0;
            if (m.IsKeywordEnabled("_EMISSION") != glow) wrong.Add(m.name + (glow ? " 꺼짐" : " 켜짐"));
            if (EditorUtility.IsDirty(m)) wrong.Add(m.name + " 저장 안 됨");
        }
        if (wrong.Count > 0) Debug.LogError($"[GameSceneBuilder] City: 간판 발광 재질이 배포판 모습과 다릅니다 (메모리 기준, {when}): " + string.Join(", ", wrong));
        else Debug.Log($"[GameSceneBuilder] City: 간판 발광 확인 - 메모리에 올라온 {loaded}개가 파일과 같음 (메모리 기준, {when})");
        return wrong.Count;
    }

    // 도시 씬 저장 직전: 게임 씬 전체 생성은 예전처럼 전체 저장, 도시 씬만 생성은 메시 묶음·도시 프로파일·이번에 쓴 재질만 저장한다
    static void KSaveCityAssets()
    {
        if (!kKeepMaterials)
        {
            AssetDatabase.SaveAssets();
            return;
        }
        EditorUtility.SetDirty(meshContainer);
        AssetDatabase.SaveAssetIfDirty(meshContainer);
        string profilePath = SettingsDir + "/CityProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profilePath);
        int parts = 0;
        if (profile != null)
        {
            // MakeProfile이 지우고 새로 만든 파일: 본체와 효과 여섯을 모두 저장할 것으로 표시한 뒤 저장한다
            EditorUtility.SetDirty(profile);
            foreach (var c in profile.components)
                if (c != null) { EditorUtility.SetDirty(c); parts++; }
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        foreach (var m in kWritten)
            if (m != null && EditorUtility.IsPersistent(m)) AssetDatabase.SaveAssetIfDirty(m);
        long bytes = new FileInfo(KGuardRoot + "/" + GenDir + "/CityMeshes.asset").Length;
        if (bytes < 64 * 1024)
            Debug.LogError($"[GameSceneBuilder] City: 메시 묶음이 디스크에 저장되지 않은 것 같습니다 ({bytes}바이트). 씬을 다시 열어 메시가 비지 않았는지 확인하세요.");
        // 프로파일 파일에 본체 + 효과 수만큼 문서가 있어야 한다 (없으면 다시 불러올 때 톤매핑·색 보정·비네트·그레인·블룸이 빠진다)
        string profileFile = KGuardRoot + "/" + profilePath;
        int docs = File.Exists(profileFile) ? KCountOccurrences(File.ReadAllText(profileFile), "--- !u!114 ") : 0;
        if (profile == null || docs != parts + 1)
            Debug.LogError($"[GameSceneBuilder] City: 후처리 프로파일 파일이 덜 저장된 것 같습니다 (문서 {docs}개, 기대 {parts + 1}개): {profilePath}");
    }

    static int KCountOccurrences(string text, string needle)
    {
        int n = 0;
        for (int at = text.IndexOf(needle, System.StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, System.StringComparison.Ordinal)) n++;
        return n;
    }

    // 메뉴 '도시 씬만 생성'의 본체 (GameSceneBuilder.Ruins.cs의 메뉴가 프리팹을 찾은 뒤 부른다)
    static void BuildCityOnlyGuarded(GameObject player, GameObject zombie, GameObject dog)
    {
        const string abort = "[GameSceneBuilder] City 생성 중단: ";
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(abort + "플레이 모드에서는 씬을 만들지 않습니다.");
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning(abort + "스크립트 컴파일이나 에셋 들여오기가 끝난 뒤에 다시 실행하세요.");
            return;
        }
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning(abort + "프리팹 편집 화면이 열려 있습니다. 닫은 뒤 다시 실행하세요.");
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var open = SceneManager.GetSceneAt(i);
            if (!open.isDirty) continue;
            Debug.LogWarning(abort + $"열려 있는 씬 '{(string.IsNullOrEmpty(open.name) ? "이름 없음" : open.name)}'에 저장하지 않은 변경이 있습니다 (새 씬을 열면 말없이 사라집니다).");
            return;
        }
        var files = KCityGuardFiles();
        var dirty = KDirtyNonMaterialAssets();
        var blocking = dirty.FindAll(p => files.Contains(p));
        if (blocking.Count > 0)
        {
            // 지킴 검사가 그 파일을 사본과 견준다. 주인이 저장하거나 버린 뒤에 다시 실행한다
            Debug.LogWarning(abort + "지킴 목록의 에셋이 저장되지 않은 채입니다: " + string.Join(", ", blocking));
            return;
        }
        if (dirty.Count > 0)
            Debug.LogWarning("[GameSceneBuilder] City: 저장되지 않은 다른 에셋·설정 " + dirty.Count + "개 (이 메뉴는 저장하지 않지만, 생성 중 유니티가 스스로 저장하면 같이 저장될 수 있습니다): " + string.Join(", ", dirty.GetRange(0, Mathf.Min(10, dirty.Count))));
        Debug.Log("[GameSceneBuilder] City 생성 전 정리: " + KPrecleanMaterials("before"));
        KGuard guard;
        try
        {
            guard = KCityGuardSnapshot(files);
            KBackupCityOutputs();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Debug.LogError(abort + "지킬 파일의 사본을 뜨지 못했습니다. " + e.Message);
            return;
        }

        System.Exception error = null;
        // 같은 에디터 세션에서 앞서 돈 생성기(방공호·컷씬)가 M에 남긴 재질을 쓰지 않는다: 스크립트를 막 다시 읽은 에디터와 같은 결과가 나온다
        // (RuinMaterials 건너뛰기, 해치 문짝의 doorRust/gaugeFace 선택이 M에 달려 있다 — Ruins.cs:16, Doors.cs:9)
        M.Clear();
        kKeepMaterials = true;
        kCreated.Clear();
        kWritten.Clear();
        kSignRepaired.Clear();
        kSyncedOnLoad.Clear();
        try
        {
            CreateMaterials();
            BuildCityScene(player, zombie, dog);
        }
        catch (System.Exception e)
        {
            error = e;
        }
        finally
        {
            kKeepMaterials = false;
            KCityGuardVerify(guard, "생성 직후");
            Debug.Log("[GameSceneBuilder] City 생성 뒤 정리: " + KPrecleanMaterials("after"));
            KCheckSignGlowFiles();
            KCheckSignGlowMemory("생성 직후");
            KScheduleGuardRecheck(guard);
        }
        if (error != null)
        {
            Debug.LogException(error);
            // 만들다 만 씬은 버리고 도시 결과물을 생성 전으로 되돌린다 (빈 메시 묶음을 가리키는 씬이 남지 않게)
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            string left;
            try
            {
                left = KRestoreCityOutputs();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                left = " (도시 결과물을 되돌리지 못했습니다: " + e.Message + ". " + KCityGuardDir + "/out 의 사본을 손으로 되돌리세요)";
            }
            Debug.LogError(abort + error.GetType().Name + ": " + error.Message + left);
            return;
        }
        if (kCreated.Count > 0)
            Debug.LogWarning($"[GameSceneBuilder] City: 재질 파일 {kCreated.Count}개를 새로 만들었습니다. URP가 다음 에디터 틱에 프로젝트 전체를 저장합니다 (저장하지 않은 다른 에셋도 함께 저장됩니다) — 2초 뒤 지킴 검사를 확인하세요");
        Debug.Log($"[GameSceneBuilder] City 씬만 다시 생성 완료 (새로 만든 재질 {kCreated.Count}개, 되돌린 간판 재질 {kSignRepaired.Count}개, 불러올 때 _EMISSION을 파일에 맞춘 재질 {kSyncedOnLoad.Count}개"
            + (kSyncedOnLoad.Count > 0 ? ": " + string.Join(", ", kSyncedOnLoad.GetRange(0, Mathf.Min(20, kSyncedOnLoad.Count))) : "") + ")");
    }

    // 메모리에 올라와 '저장 안 됨' 표시가 있는 에셋·설정(재질 빼고, Assets/와 ProjectSettings/ 아래)의 경로
    static List<string> KDirtyNonMaterialAssets()
    {
        var list = new List<string>();
        foreach (var o in Resources.FindObjectsOfTypeAll<Object>())
        {
            if (o == null || o is Material || !EditorUtility.IsDirty(o)) continue;
            string path = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(path) || list.Contains(path)) continue;
            if (!path.StartsWith("Assets/", System.StringComparison.Ordinal) && !path.StartsWith("ProjectSettings/", System.StringComparison.Ordinal)) continue;
            if (path.StartsWith("Assets/", System.StringComparison.Ordinal) && !EditorUtility.IsPersistent(o)) continue;
            list.Add(path);
        }
        return list;
    }

    // 정리 (파일은 건드리지 않는다): 메모리의 _EMISSION을 디스크에 맞추고 재질의 '저장 안 됨' 표시를 지운다.
    // 다른 세션이 저장하지 않은 재질 변경도 표시가 지워지므로 전체 목록을 Library/CityGuard/preclean_<tag>.txt에 남긴다
    static string KPrecleanMaterials(string tag)
    {
        int fixedN = 0, cleared = 0;
        var names = new List<string>();
        foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
        {
            string path = AssetDatabase.GetAssetPath(m);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", System.StringComparison.Ordinal) || !path.EndsWith(".mat", System.StringComparison.Ordinal)) continue;
            string file = KGuardRoot + "/" + path;
            if (!File.Exists(file)) continue;
            bool disk = KFileValidKeyword(File.ReadAllText(file), "_EMISSION");
            string what = null;
            if (disk != m.IsKeywordEnabled("_EMISSION"))
            {
                if (disk) m.EnableKeyword("_EMISSION");
                else m.DisableKeyword("_EMISSION");
                fixedN++;
                what = disk ? "_EMISSION 켬" : "_EMISSION 끔";
            }
            if (EditorUtility.IsDirty(m))
            {
                EditorUtility.ClearDirty(m);
                cleared++;
                what = what == null ? "표시 지움" : what + ", 표시 지움";
            }
            if (what != null) names.Add(path + " : " + what);
        }
        try
        {
            string dir = KGuardRoot + "/" + KCityGuardDir;
            Directory.CreateDirectory(dir);
            File.WriteAllLines(dir + "/preclean_" + tag + ".txt", names);
        }
        catch (System.Exception)
        {
            // 목록을 못 남겨도 정리는 끝났다
        }
        var shortNames = new List<string>();
        foreach (string n in names) { if (shortNames.Count >= 20) break; shortNames.Add(Path.GetFileNameWithoutExtension(n.Substring(0, n.IndexOf(" : ", System.StringComparison.Ordinal)))); }
        return $"_EMISSION을 디스크에 맞춘 재질 {fixedN}개, 저장 안 됨 표시를 지운 재질 {cleared}개" + (shortNames.Count > 0 ? " (" + string.Join(", ", shortNames) + (names.Count > 20 ? " … 전체는 " + KCityGuardDir + "/preclean_" + tag + ".txt" : "") + ")" : "");
    }

    // ================= 지킴 검사 (Apt102의 것과 같은 방식, 도시용 허용 목록) =================

    sealed class KGuard
    {
        public readonly Dictionary<string, string> hash = new Dictionary<string, string>(); // 프로젝트 기준 경로 → 지문 ("" = 없던 파일)
        public readonly HashSet<string> copied = new HashSet<string>();                      // 사본이 있는 것
        public readonly HashSet<string> dirtyBefore = new HashSet<string>();                 // 사본이 있는 것 가운데 생성 전부터 메모리에서 바뀌어 있던 에셋
    }

    const string KCityGuardDir = "Library/CityGuard";

    static string KGuardRoot => Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

    static string KGuardSha1(string path)
    {
        if (!File.Exists(path)) return "";
        using (var sha = System.Security.Cryptography.SHA1.Create())
        using (var stream = File.OpenRead(path))
            return System.BitConverter.ToString(sha.ComputeHash(stream));
    }

    // 사본을 뜨는 파일: Assets 아래 모든 재질(+meta)과 밸런스 에셋(+meta)
    static List<string> KCityGuardFiles()
    {
        string root = KGuardRoot;
        var list = new List<string>();
        foreach (string f in Directory.GetFiles(root + "/Assets", "*.mat", SearchOption.AllDirectories))
            if (f.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)) list.Add(f.Replace('\\', '/').Substring(root.Length + 1));
        list.Add("Assets/Resources/GameBalance.asset");
        for (int i = 0, n = list.Count; i < n; i++) list.Add(list[i] + ".meta");
        list.Sort(string.CompareOrdinal);
        return list;
    }

    // 지문만 보는 파일: 다른 씬·생성 에셋·설정 에셋·프리팹, 프로젝트 설정 셋, 방공호 생성 그림, 도시 그림의 .meta
    static List<string> KCityHashOnly()
    {
        string root = KGuardRoot;
        var list = new List<string> { "ProjectSettings/ProjectSettings.asset", "ProjectSettings/EditorBuildSettings.asset", "ProjectSettings/URPProjectSettings.asset" };
        foreach (var (dir, pattern) in new[] { (SceneDir, "*.unity"), (GenDir, "*.asset"), (SettingsDir, "*.asset"), (PrefabDir, "*.prefab"), (GrimeDir, "*.png"), (CityGenDir, "*.meta") })
        {
            if (!Directory.Exists(root + "/" + dir)) continue;
            foreach (string f in Directory.GetFiles(root + "/" + dir, pattern, SearchOption.TopDirectoryOnly))
            {
                string rel = f.Replace('\\', '/').Substring(root.Length + 1);
                if (!KCityOutput(rel)) list.Add(rel);
            }
        }
        return list;
    }

    // 이 생성기의 결과 (지킴 검사 밖): 도시 씬·메시 묶음·내비메시·프로파일 (오클루전은 Assets/Scenes/City/ 아래라 목록에 없다)
    static bool KCityOutput(string rel) =>
        rel == SceneDir + "/City.unity" || rel == GenDir + "/CityMeshes.asset" || rel == GenDir + "/CityNavMesh.asset" || rel == SettingsDir + "/CityProfile.asset";

    // 도시 결과물 파일 (도중에 멈췄을 때 되돌릴 사본을 뜬다)
    static string[] KCityOutputFiles() => new[]
    {
        SceneDir + "/City.unity", SceneDir + "/City/OcclusionCullingData.asset", GenDir + "/CityMeshes.asset", GenDir + "/CityNavMesh.asset", SettingsDir + "/CityProfile.asset",
    };

    static void KBackupCityOutputs()
    {
        string root = KGuardRoot;
        foreach (string asset in KCityOutputFiles())
            foreach (string rel in new[] { asset, asset + ".meta" })
            {
                string src = root + "/" + rel, copy = root + "/" + KCityGuardDir + "/out/" + rel;
                if (File.Exists(copy)) File.Delete(copy); // 지난 실행의 사본이 남아 섞이지 않게
                if (!File.Exists(src)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(src, copy, true);
            }
    }

    // 생성이 도중에 멈춘 뒤: 사본과 달라진 도시 결과물을 되돌리고 다시 들여온다. 중단 줄 끝에 붙일 말을 돌려준다
    static string KRestoreCityOutputs()
    {
        string root = KGuardRoot;
        var back = new List<string>();
        foreach (string asset in KCityOutputFiles())
        {
            bool changed = false;
            foreach (string rel in new[] { asset, asset + ".meta" })
            {
                string path = root + "/" + rel, copy = root + "/" + KCityGuardDir + "/out/" + rel;
                if (!File.Exists(copy) || KGuardSha1(path) == KGuardSha1(copy)) continue;
                File.Copy(copy, path, true);
                changed = true;
            }
            if (!changed) continue;
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
            back.Add(Path.GetFileName(asset));
        }
        return back.Count > 0
            ? " (만들다 만 씬은 버리고 도시 결과물 " + back.Count + "개를 생성 전으로 되돌렸습니다: " + string.Join(", ", back) + ")"
            : " (만들다 만 씬은 버렸습니다. 디스크의 도시 결과물은 그대로입니다)";
    }

    // 메모리에 올라와 있는 에셋에 저장하지 않은 변경이 있는가 (.meta는 늘 false, 올라와 있지 않은 에셋은 불러오지 않는다)
    static bool KUnsaved(string rel)
    {
        if (rel.EndsWith(".meta", System.StringComparison.Ordinal) || !AssetDatabase.IsMainAssetAtPathLoaded(rel)) return false;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(rel))
            if (o != null && EditorUtility.IsDirty(o)) return true;
        return false;
    }

    // 바뀐 파일이 누구 손에 바뀌었는지 짐작할 실마리 (Apt102GuardHint와 같은 판단)
    static string KCityGuardHint(KGuard g, string asset)
    {
        try
        {
            if (KUnsaved(asset)) return ". 메모리의 에셋은 저장되지 않은 채입니다: 유니티가 아니라 밖에서(다른 세션·git) 디스크의 파일을 고친 것으로 보입니다";
            if (g.dirtyBefore.Contains(asset)) return ". 생성 전부터 메모리에서 바뀌어 있던 에셋을 유니티가 저장한 것으로 보입니다";
            return ". 생성 전에는 메모리에서 바뀌어 있지 않던 에셋입니다: 이 생성기가 고쳐서 저장했거나 밖에서(다른 세션·git) 디스크의 파일을 고친 것입니다";
        }
        catch (System.Exception)
        {
            return "";
        }
    }

    static KGuard KCityGuardSnapshot(List<string> files)
    {
        string root = KGuardRoot;
        var g = new KGuard();
        foreach (string rel in files)
        {
            string src = root + "/" + rel, copy = root + "/" + KCityGuardDir + "/" + rel;
            g.hash[rel] = KGuardSha1(src);
            if (!File.Exists(src)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            File.Copy(src, copy, true);
            g.copied.Add(rel);
            if (KUnsaved(rel)) g.dirtyBefore.Add(rel);
        }
        foreach (string rel in KCityHashOnly())
            if (!g.hash.ContainsKey(rel)) g.hash[rel] = KGuardSha1(root + "/" + rel);
        if (g.copied.Count < 20) throw new IOException("지킬 재질 파일을 찾지 못했습니다: " + root + "/Assets");
        return g;
    }

    // 바뀌어도 되돌리지 않는 파일: 이번에 새로 만든 재질, KRebuildMaterials에 적은 재질(이름으로, Assets/Materials와 Resources/Effects 아래), 되돌린 간판 재질
    static bool KCityAllowed(string rel)
    {
        string asset = rel.EndsWith(".meta", System.StringComparison.Ordinal) ? rel.Substring(0, rel.Length - 5) : rel;
        foreach (var m in kCreated)
            if (m != null && AssetDatabase.GetAssetPath(m) == asset) return true;
        foreach (var m in kSignRepaired)
            if (m != null && AssetDatabase.GetAssetPath(m) == asset) return true;
        if (asset.EndsWith(".mat", System.StringComparison.Ordinal) && (asset.StartsWith("Assets/Materials/", System.StringComparison.Ordinal) || asset.StartsWith(EffectDir + "/", System.StringComparison.Ordinal))
            && System.Array.IndexOf(KRebuildMaterials, Path.GetFileNameWithoutExtension(asset)) >= 0) return true;
        return false;
    }

    // 달라진 파일 수를 돌려준다. 사본이 있는 재질은 되돌리고(바뀐 쪽은 .changed로 남긴다), 재질이 아닌 파일과 사본이 없는 파일은 알리기만 한다
    static int KCityGuardVerify(KGuard g, string when)
    {
        string root = KGuardRoot, tag = "[GameSceneBuilder] City 지킴 검사(" + when + "): ";
        int diffs = 0, allowed = 0;
        var reimport = new List<string>();
        try
        {
            foreach (var kv in g.hash)
            {
                string rel = kv.Key, path = root + "/" + rel;
                if (KGuardSha1(path) == kv.Value) continue;
                if (KCityAllowed(rel))
                {
                    allowed++;
                    Debug.Log(tag + "허용된 변경: " + rel);
                    continue;
                }
                diffs++;
                string asset = rel.EndsWith(".meta", System.StringComparison.Ordinal) ? rel.Substring(0, rel.Length - 5) : rel;
                string hint = KCityGuardHint(g, asset); // 되돌린 파일을 다시 들여오면 메모리의 흔적이 지워진다. 그 전에 본다
                if (!g.copied.Contains(rel))
                {
                    Debug.LogError(tag + "바뀌면 안 되는 파일이 바뀌었습니다 (되돌리지 않음): " + rel + hint);
                    continue;
                }
                if (!asset.EndsWith(".mat", System.StringComparison.Ordinal))
                {
                    // 밸런스 에셋: 이 생성기는 쓰지 않고, 저장되지 않은 채면 생성 전에 멈춘다. 그런데도 바뀌었으면 밖에서 고친 것일 수 있어 되돌리지 않는다
                    Debug.LogError(tag + "재질이 아닌 지킴 파일이 바뀌었습니다 (되돌리지 않음, 생성 전 사본은 " + KCityGuardDir + "/" + rel + "): " + rel + hint);
                    continue;
                }
                string copy = root + "/" + KCityGuardDir + "/" + rel;
                if (File.Exists(path)) File.Copy(path, copy + ".changed", true);
                File.Copy(copy, path, true);
                if (!reimport.Contains(asset)) reimport.Add(asset);
                Debug.LogError(tag + "바뀐 파일을 되돌렸습니다: " + rel + " (바뀐 내용은 " + KCityGuardDir + "/" + rel + ".changed" + hint + ")");
            }
            foreach (string asset in reimport) AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
            // 되돌린 재질은 다시 들여오면서 셰이더 검증을 거쳐 메모리 값이 파일과 달라질 수 있다 (_EMISSION). 메모리를 파일에 맞춘다
            if (reimport.Count > 0) Debug.Log(tag + "되돌린 뒤 정리: " + KPrecleanMaterials("restore"));
            var dirtied = new List<string>();
            foreach (string rel in KCityGuardFiles())
            {
                if (!g.hash.ContainsKey(rel))
                {
                    if (!File.Exists(root + "/" + rel)) continue;
                    if (KCityAllowed(rel)) { allowed++; continue; }
                    diffs++;
                    Debug.LogError(tag + "만들기로 하지 않은 새 파일이 생겼습니다: " + rel);
                    continue;
                }
                // 디스크는 그대로라도 메모리의 에셋이 바뀌어 있으면 다음에 누가 저장할 때 같이 저장된다 (재질은 바로 뒤 정리가 표시를 지운다)
                if (!g.dirtyBefore.Contains(rel) && KUnsaved(rel)) dirtied.Add(Path.GetFileNameWithoutExtension(rel));
            }
            if (dirtied.Count > 0)
                Debug.LogWarning($"{tag}디스크 파일은 그대로지만 생성 중에 메모리에서 바뀐 에셋 {dirtied.Count}개: {string.Join(", ", dirtied.GetRange(0, Mathf.Min(20, dirtied.Count)))}");
        }
        catch (System.Exception e)
        {
            diffs++;
            Debug.LogError(tag + "검사를 끝내지 못했습니다 (" + e.Message + ")");
        }
        if (diffs > 0) Debug.LogError($"{tag}달라진 파일 {diffs}개 (파일 {g.hash.Count}개 확인, 허용된 변경 {allowed}개)");
        else Debug.Log($"{tag}파일 {g.hash.Count}개 확인, 허용된 변경 {allowed}개, 나머지 그대로");
        return diffs;
    }

    // 2초 뒤 한 번 더 본다 (파일 검사 + 메모리 정리 + 간판 메모리 확인). 새 재질을 만들면 URP가 다음 에디터 틱에 전체 저장(SaveAssets)을 부른다
    // (MaterialPostprocessor). delayCall은 MCP로 부른 코드에서 오지 않은 적이 있어 update에 건다
    static void KScheduleGuardRecheck(KGuard g)
    {
        double due = EditorApplication.timeSinceStartup + 2.0;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (EditorApplication.timeSinceStartup < due) return;
            EditorApplication.update -= tick;
            KCityGuardVerify(g, "2초 뒤");
            Debug.Log("[GameSceneBuilder] City 2초 뒤 정리: " + KPrecleanMaterials("recheck"));
            KCheckSignGlowMemory("2초 뒤");
        };
        EditorApplication.update += tick;
    }
}
