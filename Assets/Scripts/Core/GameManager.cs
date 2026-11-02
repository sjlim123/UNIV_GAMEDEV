using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 턴·상태·씬 이동·게임오버를 관리한다. 어느 씬에서 플레이를 눌러도 자동으로 생긴다.
public class GameManager : MonoBehaviour
{
    public const string MenuScene = "MainMenu";
    public const string ShelterScene = "Shelter";
    public const string CityScene = "City";

    // 턴마다 줄어드는 양은 GameBalance 에셋에서 조정한다
    public static float HungerPerTurn => GameBalance.Data.hungerPerTurn;
    public static float ThirstPerTurn => GameBalance.Data.thirstPerTurn;
    public static float PowerPerTurn => GameBalance.Data.powerPerTurn;
    public static float StarveDamagePerTurn => GameBalance.Data.starveDamagePerTurn;
    public static float RestHeal => GameBalance.Data.restHeal;

    static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null) new GameObject("GameManager").AddComponent<GameManager>();
            return instance;
        }
    }

    public GameState State { get; private set; }
    public UIManager UI { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsTransitioning { get; private set; }

    Dictionary<string, int> expeditionStartItems;
    int expeditionStartTurn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        instance = null;
        _ = Instance;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        GameSettings.Load();
        GameSettings.ApplyGlobal();
        State = new GameState();
        UI = gameObject.AddComponent<UIManager>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameSettings.RestoreAssets();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UI.OnSceneLoaded(scene.name);
        GameSettings.CaptureScene();
        GameSettings.ApplyScene();
        // 방공호에 도착할 때마다 자동 저장
        if (scene.name == ShelterScene)
        {
            State.InExpedition = false;
            SaveGame();
        }
    }

    public bool InShelter => SceneManager.GetActiveScene().name == ShelterScene;

    // 방공호에 있을 때만 저장한다 (도시 탐험 도중 상태는 저장하지 않음)
    public void SaveGame()
    {
        if (IsGameOver || State == null) return;
        // 정전으로 들어온 좀비도 함께 저장한다 (나갔다 들어와서 피할 수 없게)
        if (InShelter) State.ShelterInvaders = ShelterInvasion.Instance != null ? ShelterInvasion.Instance.InvaderCount : 0;
        SaveSystem.Save(State);
    }

    void OnApplicationQuit()
    {
        if (InShelter && !IsTransitioning) SaveGame();
    }

    // 1턴 진행. 죽거나 마지막 턴이 끝나면 false
    public bool AdvanceTurn()
    {
        var s = State;
        s.Turn++;
        s.Hunger = Mathf.Max(0, s.Hunger - HungerPerTurn);
        s.Thirst = Mathf.Max(0, s.Thirst - ThirstPerTurn);
        if (s.Power > 0) s.Power = Mathf.Max(0, s.Power - PowerPerTurn);

        bool starving = s.Hunger <= 0 || s.Thirst <= 0;
        if (starving)
        {
            s.Health -= StarveDamagePerTurn;
            UI.ShowMessage(s.Hunger <= 0 ? "굶주림으로 몸이 점점 약해진다..." : "목이 타들어 간다. 몸이 버티지 못한다...", 3.5f);
        }
        if (s.Health <= 0)
        {
            s.Health = 0;
            GameOver("사망", "굶주림과 갈증을 끝내 견디지 못했다.");
            return false;
        }
        if (s.Turn >= GameState.MaxTurns)
        {
            IsGameOver = true;
            SaveSystem.Delete();
            UI.ShowEndScreen($"{GameState.MaxTurns}턴 생존", $"방공호에서 {GameState.MaxTurns}턴을 버텨냈다.\n(엔딩은 스토리와 함께 정해질 예정)");
            return false;
        }
        return true;
    }

    public void Damage(float amount, string cause)
    {
        if (IsGameOver || IsTransitioning) return;
        State.Health -= amount;
        UI.FlashDamage(Mathf.Clamp01(amount / 30f));
        if (PlayerController.Instance != null) PlayerController.Instance.Shake(0.35f);
        if (State.Health <= 0)
        {
            State.Health = 0;
            GameOver("사망", cause);
        }
    }

    public bool UseItem(string id, Inventory from)
    {
        var def = ItemDatabase.Get(id);
        if (!def.IsConsumable || !from.Remove(id)) return false;
        var s = State;
        s.Health = Mathf.Clamp(s.Health + def.Heal, 1, GameBalance.Data.playerMaxHealth);
        s.Hunger = Mathf.Min(100, s.Hunger + def.Hunger);
        s.Thirst = Mathf.Min(100, s.Thirst + def.Thirst);
        UI.ShowMessage($"{def.Name} 사용 ({def.Description})");
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.5f);
        if (PlayerHands.Instance != null) PlayerHands.Instance.PlayUse();
        return true;
    }

    // 새 게임: 저장을 지우고 도입 컷씬(사진 3장)을 보여 준 뒤 방공호로.
    // 죽고 나서 '처음부터 다시'는 컷씬 없이 바로 시작한다
    public void NewGame(bool playIntro = true)
    {
        if (IsTransitioning) return;
        IsGameOver = false;
        State = new GameState();
        if (!playIntro) SaveSystem.Delete();
        StartCoroutine(playIntro ? NewGameRoutine() : LoadRoutine(ShelterScene, false));
    }

    IEnumerator NewGameRoutine()
    {
        IsTransitioning = true;
        UI.ClosePanel();
        UI.SetPaused(false);
        yield return UI.Fade(1, 0.6f);
        bool done = false;
        UI.PlayCutscene(() => done = true);
        yield return UI.Fade(0, 0.6f);
        while (!done) yield return null;
        // 컷씬 도중 게임을 꺼도 예전 저장이 남도록, 저장은 컷씬이 끝난 뒤에 지운다
        SaveSystem.Delete();
        yield return LoadRoutine(ShelterScene, false);
    }

    // 이어하기: 저장된 상태로 바로 방공호에서 시작 (컷씬 없음)
    public void ContinueGame()
    {
        if (IsTransitioning) return;
        var loaded = SaveSystem.Load();
        if (loaded == null)
        {
            UI.OpenPanel("이어하기", c => UI.Line(c, "저장된 게임을 불러오지 못했다. 저장 파일이 손상되었을 수 있다."));
            return;
        }
        string notice = null;
        if (loaded.InExpedition)
        {
            // 탐험 도중 메뉴로 나가거나 게임을 껐다: 가져간 물건을 모두 잃고 방공호에서 다시 시작한다
            loaded.InExpedition = false;
            foreach (var kv in loaded.Carried.Sorted()) loaded.Carried.Remove(kv.Key, kv.Value);
            notice = "탐험 도중 정신을 잃었다가 간신히 방공호로 돌아왔다. 가져간 물건을 모두 잃었다.";
        }
        IsGameOver = false;
        State = loaded;
        StartCoroutine(ContinueRoutine(notice));
    }

    IEnumerator ContinueRoutine(string notice)
    {
        yield return LoadRoutine(ShelterScene, false);
        if (notice != null) UI.ShowMessage(notice, 6f);
    }

    public void GoToMainMenu()
    {
        if (InShelter) SaveGame();
        IsGameOver = false;
        StartCoroutine(LoadRoutine(MenuScene, false));
    }

    public void StartExpedition()
    {
        if (IsTransitioning || IsGameOver) return;
        expeditionStartItems = State.Carried.Snapshot();
        expeditionStartTurn = State.Turn;
        if (!AdvanceTurn()) return;
        // 출발 직후 상태를 저장한다. 탐험 도중 게임을 끄면 이어하기 때 가져간 물건을 잃고 방공호에서 다시 시작한다
        State.InExpedition = true;
        SaveGame();
        StartCoroutine(LoadRoutine(CityScene, false));
    }

    public void ReturnToShelter()
    {
        if (IsTransitioning || IsGameOver) return;
        if (!AdvanceTurn()) return;
        StartCoroutine(LoadRoutine(ShelterScene, true));
    }

    public void ExpeditionTimeUp()
    {
        GameOver("탐험 실패", "제한시간 안에 방공호로 돌아오지 못했다.\n도시의 어둠이 당신을 삼켰다.");
    }

    public void Rest()
    {
        if (IsTransitioning || IsGameOver) return;
        StartCoroutine(RestRoutine());
    }

    IEnumerator RestRoutine()
    {
        IsTransitioning = true;
        UI.ClosePanel();
        yield return UI.Fade(1, 0.8f);
        bool alive = AdvanceTurn();
        if (alive)
        {
            State.Health = Mathf.Min(GameBalance.Data.playerMaxHealth, State.Health + RestHeal);
            UI.ShowMessage($"잠시 눈을 붙였다. 체력 +{RestHeal:0}  (턴 {State.Turn}/{GameState.MaxTurns})", 3f);
            SaveGame();
        }
        yield return new WaitForSecondsRealtime(0.6f);
        IsTransitioning = false;
        yield return UI.Fade(0, 0.8f);
    }

    public void Restart() => NewGame(false);

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void GameOver(string title, string reason)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        SaveSystem.Delete(); // 죽으면 저장도 사라진다
        UI.ShowEndScreen(title, $"{reason}\n\n버틴 턴: {State.Turn} / {GameState.MaxTurns}");
    }

    IEnumerator LoadRoutine(string scene, bool showResult)
    {
        IsTransitioning = true;
        UI.ClosePanel();
        UI.SetPaused(false);
        yield return UI.Fade(1, 0.7f);
        var op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone) yield return null;
        yield return null;
        IsTransitioning = false;
        if (showResult) ShowExpeditionResult();
        yield return UI.Fade(0, 1.0f);
    }

    void ShowExpeditionResult()
    {
        var gained = new List<string>();
        foreach (var kv in State.Carried.Sorted())
        {
            int before = expeditionStartItems != null && expeditionStartItems.TryGetValue(kv.Key, out var b) ? b : 0;
            if (kv.Value > before) gained.Add($"{ItemDatabase.Name(kv.Key)} x{kv.Value - before}");
        }
        int turns = State.Turn - expeditionStartTurn;
        UI.OpenPanel("탐험 결과", c =>
        {
            UI.Line(c, gained.Count > 0 ? "획득한 아이템" : "아무것도 챙기지 못했다.", 26, UIManager.Accent);
            foreach (var g in gained) UI.Line(c, "  · " + g);
            UI.Line(c, "");
            UI.Line(c, $"소모된 턴: {turns}   (현재 {State.Turn} / {GameState.MaxTurns})");
            UI.Line(c, "창고(사물함)에서 물건을 정리하세요.", 20, UIManager.Dim);
        });
    }
}
