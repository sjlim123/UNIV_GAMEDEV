using System.IO;
using UnityEngine;

// 저장 슬롯 3개(save1~3.json)에 진행 상태를 저장한다. 지금 하는 게임은 CurrentSlot에 저장된다.
// 위치: Application.persistentDataPath (Windows: %USERPROFILE%\AppData\LocalLow\<회사>\<제품>\save1.json)
// 방공호에 있을 때(도착·휴식·탐험 출발·메뉴로 나가기·게임 종료) 자동 저장하고, 죽거나 엔딩을 보면 그 슬롯을 지운다.
public static class SaveSystem
{
    public const int SlotCount = 3;

    // 지금 하는 게임의 슬롯 (1 ~ SlotCount)
    public static int CurrentSlot { get; set; } = 1;
    // 새 게임·이어하기로 슬롯을 골랐는가. 고르기 전(에디터에서 방공호 씬을 바로 Play했을 때)에는 어느 슬롯에도 저장하지 않는다
    public static bool SlotChosen { get; set; }

    static string Dir => Application.persistentDataPath;
    static string PathOf(int slot) => Path.Combine(Dir, $"save{slot}.json");
    static string TempOf(int slot) => PathOf(slot) + ".tmp";
    // 슬롯이 생기기 전(26.0.0.4)의 저장 파일. 처음 읽을 때 1번 슬롯으로 옮긴다
    static string LegacyPath => Path.Combine(Dir, "save.json");

    static bool migrated;

    static void Prepare(int slot)
    {
        try
        {
            if (!migrated)
            {
                migrated = true;
                if (File.Exists(LegacyPath) && !File.Exists(PathOf(1))) File.Move(LegacyPath, PathOf(1));
                if (File.Exists(LegacyPath + ".tmp") && !File.Exists(PathOf(1))) File.Move(LegacyPath + ".tmp", PathOf(1));
            }
            // 바꿔치기 도중 꺼져서 임시 파일만 남았으면 그것을 저장 파일로 되살린다
            if (!File.Exists(PathOf(slot)) && File.Exists(TempOf(slot))) File.Move(TempOf(slot), PathOf(slot));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 저장 파일 정리 실패: " + e.Message);
        }
    }

    public static bool Exists(int slot)
    {
        Prepare(slot);
        return File.Exists(PathOf(slot));
    }

    // 하나라도 저장된 슬롯이 있는가 ('이어하기' 버튼)
    public static bool AnyExists
    {
        get
        {
            for (int i = 1; i <= SlotCount; i++) if (Exists(i)) return true;
            return false;
        }
    }

    // 가장 최근에 저장된 슬롯 (없으면 0)
    public static int LatestSlot
    {
        get
        {
            int best = 0;
            System.DateTime bestTime = System.DateTime.MinValue;
            for (int i = 1; i <= SlotCount; i++)
            {
                if (!Exists(i)) continue;
                var t = File.GetLastWriteTimeUtc(PathOf(i));
                if (t > bestTime) { bestTime = t; best = i; }
            }
            return best;
        }
    }

    public static void Save(GameState state) => Save(CurrentSlot, state);

    public static void Save(int slot, GameState state)
    {
        try
        {
            var json = JsonUtility.ToJson(state.ToSaveData(), true);
            string path = PathOf(slot), temp = TempOf(slot);
            File.WriteAllText(temp, json);
            // 저장 도중 꺼져도 이전 저장이 깨지지 않게 임시 파일을 한 번에 바꿔치기한다
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 저장 실패: " + e.Message);
        }
    }

    public static SaveData LoadData(int slot)
    {
        try
        {
            if (!Exists(slot)) return null;
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf(slot)));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 불러오기 실패: " + e.Message);
            return null;
        }
    }

    public static GameState Load(int slot)
    {
        var d = LoadData(slot);
        return d != null ? GameState.FromSaveData(d) : null;
    }

    public static void Delete() => Delete(CurrentSlot);

    public static void Delete(int slot)
    {
        try
        {
            if (File.Exists(PathOf(slot))) File.Delete(PathOf(slot));
            if (File.Exists(TempOf(slot))) File.Delete(TempOf(slot));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SaveSystem] 삭제 실패: " + e.Message);
        }
    }

    // 슬롯 목록에 보여 줄 한 줄 요약 (비었으면 "", 읽을 수 없으면 "저장 파일이 손상됨")
    public static string Summary(int slot)
    {
        if (!Exists(slot)) return "";
        var d = LoadData(slot);
        return d == null ? "저장 파일이 손상됨" : $"턴 {d.turn} · {d.savedAt}";
    }
}
