using System.IO;
using UnityEngine;

// 저장 파일 하나(save.json)에 진행 상태를 저장한다.
// 위치: Application.persistentDataPath (Windows: %USERPROFILE%\AppData\LocalLow\<회사>\<제품>\save.json)
// 방공호에 있을 때(도착·휴식·탐험 출발·메뉴로 나가기·게임 종료) 자동 저장하고, 죽거나 엔딩을 보면 지운다.
public static class SaveSystem
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");
    static string TempPath => FilePath + ".tmp";

    public static bool Exists
    {
        get
        {
            Recover();
            return File.Exists(FilePath);
        }
    }

    // 바꿔치기 도중 꺼져서 임시 파일만 남았으면 그것을 저장 파일로 되살린다
    static void Recover()
    {
        try
        {
            if (!File.Exists(FilePath) && File.Exists(TempPath)) File.Move(TempPath, FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 임시 저장 복구 실패: " + e.Message);
        }
    }

    public static void Save(GameState state)
    {
        try
        {
            var json = JsonUtility.ToJson(state.ToSaveData(), true);
            File.WriteAllText(TempPath, json);
            // 저장 도중 꺼져도 이전 저장이 깨지지 않게 임시 파일을 한 번에 바꿔치기한다
            if (File.Exists(FilePath)) File.Replace(TempPath, FilePath, null);
            else File.Move(TempPath, FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 저장 실패: " + e.Message);
        }
    }

    public static SaveData LoadData()
    {
        try
        {
            if (!Exists) return null;
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 불러오기 실패: " + e.Message);
            return null;
        }
    }

    public static GameState Load()
    {
        var d = LoadData();
        return d != null ? GameState.FromSaveData(d) : null;
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 삭제 실패: " + e.Message);
        }
    }

    // 메인 메뉴 '이어하기' 옆에 보여 줄 한 줄 요약
    public static string Summary()
    {
        var d = LoadData();
        return d == null ? "" : $"턴 {d.turn} · {d.savedAt}";
    }
}
