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
    // 탐험 일지에 적을 기록
    float expeditionStartHealth, expeditionTimeLeft, expeditionRedNightSeconds;
    int expeditionZombieKills, expeditionDogKills;
    bool expeditionRedNight;
    AudioSource sfx; // 씬이 바뀌어도 끊기지 않는 소리 (문 덜컥, 잠자는 숨)
    int expeditionStartTurn;
    float starveTimer;
    // 죽거나 엔딩을 본 뒤 메인 메뉴로 돌아가기까지(초). 그 전에 버튼을 눌러도 된다
    const float EndScreenSeconds = 8f;

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
        sfx = gameObject.AddComponent<AudioSource>();
        AudioCategory.Tag(sfx, SoundCategory.Effects);
        sfx.spatialBlend = 0;
        sfx.playOnAwake = false;
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

    // 배고픔이나 갈증이 0이면 starveInterval초마다 체력이 준다 (일시정지(timeScale 0)·컷씬·씬 이동 중에는 멈춘다. 소지품 등 창을 열어 두어도 계속 준다)
    void Update()
    {
        if (State == null || IsGameOver || IsTransitioning || PlayerController.Instance == null || UI.IsPaused) { starveTimer = 0; return; }
        if (State.Hunger > 0 && State.Thirst > 0) { starveTimer = 0; return; }
        var b = GameBalance.Data;
        starveTimer += Time.deltaTime;
        if (starveTimer < b.starveInterval) return;
        starveTimer -= b.starveInterval;
        bool hungry = State.Hunger <= 0;
        UI.ShowMessage(hungry ? "굶주림으로 몸이 점점 약해진다... 무언가 먹어야 한다." : "목이 타들어 간다... 물을 마셔야 한다.", 2.5f);
        Damage(b.starveDamage, hungry ? "끝내 굶주림을 견디지 못했다." : "끝내 갈증을 견디지 못했다.");
    }

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
            Ending($"{GameState.MaxTurns}턴 생존", $"방공호에서 {GameState.MaxTurns}턴을 버텨냈다.\n(엔딩은 스토리와 함께 정해질 예정)");
            return false;
        }
        return true;
    }

    // 받는 피해 (방어구는 피해를 줄이지 않고 최대 체력을 늘린다)
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
        s.Health = Mathf.Clamp(s.Health + def.Heal, 1, s.MaxHealth);
        s.Hunger = Mathf.Min(100, s.Hunger + def.Hunger);
        s.Thirst = Mathf.Min(100, s.Thirst + def.Thirst);
        UI.ShowMessage($"{def.Name} 사용 ({def.Description})");
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.5f);
        if (PlayerHands.Instance != null) PlayerHands.Instance.PlayUse();
        return true;
    }

    // 새 게임: 저장을 지우고 도입 컷씬(사진 3장)을 보여 준 뒤 방공호로.
    // 죽고 나서 '처음부터 다시'는 컷씬 없이 바로 시작한다
    public void NewGame(int slot, bool playIntro = true)
    {
        if (IsTransitioning) return;
        IsGameOver = false;
        SaveSystem.CurrentSlot = slot;
        State = new GameState();
        if (!playIntro) SaveSystem.Delete(slot);
        StartCoroutine(playIntro ? NewGameRoutine() : LoadRoutine(ShelterScene, false));
    }

    IEnumerator NewGameRoutine()
    {
        IsTransitioning = true;
        UI.ClosePanel();
        UI.SetPaused(false);
        yield return UI.Fade(1, 0.6f);
        SilenceScene(); // 메인 메뉴 배경의 바람·좀비 소리를 끈다
        bool done = false;
        UI.PlayCutscene(() => done = true);
        yield return UI.Fade(0, 0.6f);
        while (!done) yield return null;
        // 컷씬 도중 게임을 꺼도 예전 저장이 남도록, 저장은 컷씬이 끝난 뒤에 지운다
        SaveSystem.Delete(SaveSystem.CurrentSlot);
        yield return LoadRoutine(ShelterScene, false);
    }

    // 이어하기: 저장된 상태로 바로 방공호에서 시작 (컷씬 없음)
    // 지금 씬에서 나는 소리를 모두 끈다 (UIManager·GameManager의 소리는 그대로)
    static void SilenceScene()
    {
        MainMenuBackground.Silence();
        var scene = SceneManager.GetActiveScene();
        foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (src.gameObject.scene == scene) src.Stop();
    }

    public void ContinueGame(int slot)
    {
        if (IsTransitioning) return;
        var loaded = SaveSystem.Load(slot);
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
        SaveSystem.CurrentSlot = slot;
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
        expeditionStartHealth = State.Health;
        expeditionZombieKills = expeditionDogKills = 0;
        if (!AdvanceTurn()) return;
        // 출발 직후 상태를 저장한다. 탐험 도중 게임을 끄면 이어하기 때 가져간 물건을 잃고 방공호에서 다시 시작한다
        State.InExpedition = true;
        SaveGame();
        StartCoroutine(LoadRoutine(CityScene, false, true));
    }

    // 탐험 중 쓰러뜨린 적 (탐험 일지에 적는다)
    public void NoteKill(bool dog)
    {
        if (dog) expeditionDogKills++;
        else expeditionZombieKills++;
    }

    public void ReturnToShelter()
    {
        if (IsTransitioning || IsGameOver) return;
        var city = CityDirector.Instance;
        expeditionRedNight = city != null && city.IsRedNight;
        expeditionTimeLeft = city != null ? Mathf.Max(0, city.TimeLeft) : 0;
        expeditionRedNightSeconds = city != null ? city.RedNightElapsed : 0;
        if (!AdvanceTurn()) return;
        StartCoroutine(LoadRoutine(ShelterScene, true, true));
    }

    public void ExpeditionTimeUp()
    {
        GameOver("탐험 실패", "제한시간 안에 방공호로 돌아오지 못했다.\n도시의 어둠이 당신을 삼켰다.");
    }

    // 침대에서 잔다: 누워서(eye = 베개 위 눈 위치) 천장을 보며 화면이 천천히 어두워지고, 1턴이 지난 뒤 깨어 일어난다
    public void Rest(Transform eye = null)
    {
        if (IsTransitioning || IsGameOver) return;
        StartCoroutine(RestRoutine(eye));
    }

    IEnumerator RestRoutine(Transform eye)
    {
        IsTransitioning = true;
        UI.ClosePanel();
        var pc = PlayerController.Instance;
        // 1) 눕는다
        if (pc != null && eye != null)
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 1.1f)
            {
                pc.SetSleep(eye, t);
                yield return null;
            }
        if (pc != null && eye != null) pc.SetSleep(eye, 1);
        // 2) 숨이 느려지며 눈이 감긴다 (마인크래프트처럼 천천히 어두워진다)
        sfx.pitch = 1f;
        sfx.PlayOneShot(ProceduralAudio.SleepBreath(), 0.55f);
        UI.ShowSleepText("Z z z . . .");
        yield return UI.Fade(1, 2.6f);
        bool alive = AdvanceTurn();
        if (alive)
        {
            State.Health = Mathf.Min(State.MaxHealth, State.Health + RestHeal);
            SaveGame();
            UI.ShowSleepText($"턴 {State.Turn} / {GameState.MaxTurns}");
        }
        yield return new WaitForSecondsRealtime(1.1f);
        UI.ShowSleepText(null);
        if (!alive) { IsTransitioning = false; yield break; }
        // 3) 눈을 뜨고 일어난다
        yield return UI.Fade(0, 1.2f);
        if (pc != null && eye != null)
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
            {
                pc.SetSleep(eye, 1 - t);
                yield return null;
            }
        if (pc != null) pc.SetSleep(null, 0);
        UI.ShowMessage($"잘 잤다. 체력 +{RestHeal:0}  (턴 {State.Turn}/{GameState.MaxTurns})", 3f);
        IsTransitioning = false;
    }

    // 방공호 철문·해치가 덜컥 걸리는 소리
    void DoorClunk(float pitch)
    {
        sfx.pitch = pitch;
        sfx.PlayOneShot(ProceduralAudio.Latch(), 0.9f);
    }

    public void Restart() => NewGame(SaveSystem.CurrentSlot, false);

    // 트럭을 다 고쳐 탈출한다 (진짜 엔딩)
    public void TruckEscape()
    {
        if (IsGameOver || IsTransitioning || !State.TruckComplete) return;
        Ending("탈출", $"트럭에 시동을 걸었다. 낡은 엔진이 으르렁거리며 깨어난다.\n폐허가 된 도시를 뒤로하고, 라디오가 말한 서울시 ○○으로 향한다.\n\n탈출까지 걸린 턴: {State.Turn} / {GameState.MaxTurns}");
    }

    // 엔딩: 저장을 지우고 엔딩 화면을 보여 준 뒤 메인 메뉴로 돌아간다
    void Ending(string title, string body)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        SaveSystem.Delete();
        UI.ShowEndScreen(title, body, EndScreenSeconds);
        StartCoroutine(ReturnToMenuAfter(EndScreenSeconds));
    }

    // 끝 화면을 잠깐 보여 준 뒤 자동으로 메인 메뉴로 (그 사이 버튼을 눌러 먼저 나갔으면 아무것도 안 한다)
    IEnumerator ReturnToMenuAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (IsGameOver && !IsTransitioning && !InMenu) GoToMainMenu();
    }

    public bool InMenu => SceneManager.GetActiveScene().name == MenuScene;

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
        UI.ShowEndScreen(title, $"{reason}\n\n버틴 턴: {State.Turn} / {GameState.MaxTurns}", EndScreenSeconds);
        StartCoroutine(ReturnToMenuAfter(EndScreenSeconds));
    }

    // door: 방공호를 나서거나 돌아올 때 문이 덜컥 열리고(떠날 때) 덜컥 닫힌다(도착했을 때)
    IEnumerator LoadRoutine(string scene, bool showResult, bool door = false)
    {
        IsTransitioning = true;
        UI.ClosePanel();
        UI.SetPaused(false);
        if (door) DoorClunk(1f);
        yield return UI.Fade(1, 0.7f);
        var op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone) yield return null;
        yield return null;
        IsTransitioning = false;
        if (door) DoorClunk(0.82f);
        if (showResult) ShowExpeditionResult();
        yield return UI.Fade(0, 1.0f);
    }

    // 방공호에 돌아오면 양피지 수첩에 그날의 탐험을 적는다 (UIManager.Journal)
    void ShowExpeditionResult()
    {
        var r = new UIManager.ExpeditionReport
        {
            Turn = State.Turn,
            TurnsSpent = State.Turn - expeditionStartTurn,
            HealthBefore = expeditionStartHealth,
            HealthAfter = State.Health,
            ZombiesKilled = expeditionZombieKills,
            DogsKilled = expeditionDogKills,
            RedNight = expeditionRedNight,
            TimeLeft = expeditionTimeLeft,
            RedNightSeconds = expeditionRedNightSeconds,
        };
        var now = State.Carried.Snapshot();
        foreach (var kv in State.Carried.Sorted())
        {
            int before = expeditionStartItems != null && expeditionStartItems.TryGetValue(kv.Key, out var b) ? b : 0;
            if (kv.Value > before) r.Gained.Add(new KeyValuePair<string, int>(kv.Key, kv.Value - before));
        }
        if (expeditionStartItems != null)
            foreach (var kv in expeditionStartItems)
            {
                int left = now.TryGetValue(kv.Key, out var n) ? n : 0;
                if (left < kv.Value) r.Spent.Add(new KeyValuePair<string, int>(kv.Key, kv.Value - left));
            }
        UI.ShowExpeditionJournal(r);
    }
}
