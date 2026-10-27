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
        s.Health = Mathf.Clamp(s.Health + def.Heal, 1, 100);
        s.Hunger = Mathf.Min(100, s.Hunger + def.Hunger);
        s.Thirst = Mathf.Min(100, s.Thirst + def.Thirst);
        UI.ShowMessage($"{def.Name} 사용 ({def.Description})");
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.5f);
        if (PlayerHands.Instance != null) PlayerHands.Instance.PlayUse();
        return true;
    }

    public void NewGame()
    {
        IsGameOver = false;
        State = new GameState();
        StartCoroutine(LoadRoutine(ShelterScene, false));
    }

    public void GoToMainMenu()
    {
        IsGameOver = false;
        StartCoroutine(LoadRoutine(MenuScene, false));
    }

    public void StartExpedition()
    {
        if (IsTransitioning || IsGameOver) return;
        expeditionStartItems = State.Carried.Snapshot();
        expeditionStartTurn = State.Turn;
        if (!AdvanceTurn()) return;
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
            State.Health = Mathf.Min(100, State.Health + RestHeal);
            UI.ShowMessage($"잠시 눈을 붙였다. 체력 +{RestHeal:0}  (턴 {State.Turn}/{GameState.MaxTurns})", 3f);
        }
        yield return new WaitForSecondsRealtime(0.6f);
        IsTransitioning = false;
        yield return UI.Fade(0, 0.8f);
    }

    public void Restart() => NewGame();

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
