using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 턴·상태·씬 이동·게임오버를 관리한다. 어느 씬에서 플레이를 눌러도 자동으로 생긴다.
public class GameManager : MonoBehaviour
{
    public const string MenuScene = "MainMenu";
    public const string ShelterScene = "Shelter";
    public const string CityScene = "City";
    public const string CutsceneScene = "Cutscene"; // 새 게임 도입 컷씬 (실시간 3D)

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
    public bool IsDying { get; private set; } // 죽는 연출(쓰러짐 → 검은 화면 → 끝 화면)이 진행 중

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

    // 방공호에 있을 때만 저장한다 (도시 탐험 도중 상태는 저장하지 않음). 새 게임·이어하기로 슬롯을 고르기 전에는 저장하지 않는다
    // (에디터에서 방공호 씬을 열어 둔 채 Play를 누르면 예전에는 슬롯 1이 새 게임으로 덮어써졌다)
    public void SaveGame()
    {
        if (IsGameOver || State == null || !SaveSystem.SlotChosen) return;
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
        if (PlayerHands.Instance != null) PlayerHands.Instance.PlayUse(id);
        return true;
    }

    // 새 게임: 도입 컷씬(Cutscene 씬에서 실시간으로 재생)을 보여 준 뒤 저장을 지우고 방공호로.
    // 죽고 나서 '처음부터 다시'는 컷씬 없이 바로 시작한다
    public void NewGame(int slot, bool playIntro = true)
    {
        if (IsTransitioning) return;
        IsGameOver = false;
        SaveSystem.CurrentSlot = slot;
        SaveSystem.SlotChosen = true;
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
        if (Application.CanStreamedLevelBeLoaded(CutsceneScene))
        {
            // 로딩 화면 없이 검은 화면 뒤에서 컷씬 씬을 열고, 연출이 시작되면 화면을 밝힌다
            var op = SceneManager.LoadSceneAsync(CutsceneScene);
            while (!op.isDone) yield return null;
            yield return null;
            UI.PlayCutscene(() => done = true);
            yield return UI.Fade(0, 1.2f);
        }
        else
        {
            Debug.LogWarning("[GameManager] Cutscene 씬이 빌드 목록에 없어 컷씬을 건너뜁니다 (Tools > 핵폭발 생존 > 컷씬 씬 생성).");
            done = true;
        }
        while (!done) yield return null;
        // 컷씬 도중 게임을 꺼도 예전 저장이 남도록, 저장은 컷씬이 끝난 뒤에 지운다
        SaveSystem.Delete(SaveSystem.CurrentSlot);
        yield return LoadRoutine(ShelterScene, false, false, "방공호로 들어가는 중");
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
        SaveSystem.SlotChosen = true;
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
        if (IsTransitioning) return; // 버튼을 연달아 누르거나 씬을 옮기는 중에 또 부르면 메뉴 씬이 두 번 열린다
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
        Tutorial.Notify("expedition");
        // 출발 직후 상태를 저장한다. 탐험 도중 게임을 끄면 이어하기 때 가져간 물건을 잃고 방공호에서 다시 시작한다
        State.InExpedition = true;
        SaveGame();
        StartCoroutine(DepartRoutine());
    }

    // 철문을 손으로 열고 나가는 1인칭 연출 뒤에 도시 씬을 연다
    IEnumerator DepartRoutine()
    {
        IsTransitioning = true;
        UI.ClosePanel();
        UI.SetPaused(false);
        yield return ShelterDeparture.Play();
        yield return LoadRoutine(CityScene, false, true);
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
        Tutorial.Notify("return");
        StartCoroutine(ArriveRoutine());
    }

    // 주인공이 해치로 걸어 들어가는 3인칭 연출 뒤에 방공호 씬을 연다
    IEnumerator ArriveRoutine()
    {
        IsTransitioning = true;
        UI.ClosePanel();
        UI.SetPaused(false);
        yield return HatchArrival.Play();
        yield return LoadRoutine(ShelterScene, true, true);
    }

    public void ExpeditionTimeUp()
    {
        GameOver("탐험 실패", "제한시간 안에 방공호로 돌아오지 못했다.\n도시의 어둠이 당신을 삼켰다.");
    }

    // 침대에서 잔다: 누워서(eye = 베개 위 눈 위치) 천장을 보며 화면이 천천히 어두워지고, 1턴이 지난 뒤 깨어 일어난다
    public void Rest(Transform eye = null)
    {
        if (IsTransitioning || IsGameOver) return;
        Tutorial.Notify("rest");
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
        // 잠드는 동안은 글자 없이 조용히 어두워진다 ('Z z z' 같은 만화풍 표시는 분위기에 맞지 않는다)
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
        if (!alive)
        {
            // 마지막 턴이 끝났다: 검은 화면을 걷어야 그 아래 끝 화면이 보인다 (걷지 않으면 8초 동안 까만 화면만 보인다).
            // 자다가 죽었으면 죽음 연출(DeathRoutine)이 알아서 걷는다
            if (!IsDying) yield return UI.Fade(0, 1.0f);
            IsTransitioning = false;
            yield break;
        }
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
        StartCoroutine(DeathRoutine(title, $"{reason}\n\n버틴 턴: {State.Turn} / {GameState.MaxTurns}"));
    }

    // 죽음: 쓰러지며(카메라가 바닥으로 기울고, 색이 빠지고, 소리가 먹먹해진다) 화면이 검어진 뒤 끝 화면이 천천히 나타난다.
    // 자다가 죽어 이미 검은 화면이면 쓰러지는 부분은 건너뛴다
    IEnumerator DeathRoutine(string title, string body)
    {
        IsDying = true;
        UI.ClosePanel();
        const float fallSeconds = 1.7f, fadeIn = 1.4f;
        if (UI.FadeAlpha < 0.99f)
        {
            var pc = PlayerController.Instance;
            if (pc != null) pc.PlayDeath(fallSeconds);
            sfx.pitch = 0.75f;
            sfx.PlayOneShot(ProceduralAudio.Hurt(), 0.9f);
            sfx.PlayOneShot(ProceduralAudio.Heartbeat(), 0.8f);
            var grade = new DeathGrade();
            for (float t = 0; t < fallSeconds; t += Time.unscaledDeltaTime)
            {
                grade.Apply(Mathf.Clamp01(t / fallSeconds));
                yield return null;
            }
            grade.Apply(1);
            sfx.pitch = 0.6f;
            sfx.PlayOneShot(ProceduralAudio.Heartbeat(), 0.5f);
            yield return UI.Fade(1, 1.3f);
            sfx.pitch = 1f;
        }
        yield return new WaitForSecondsRealtime(0.8f);
        UI.ShowEndScreen(title, body, EndScreenSeconds + fadeIn);
        StartCoroutine(ReturnToMenuAfter(EndScreenSeconds + fadeIn));
        yield return UI.Fade(0, fadeIn);
        IsDying = false;
    }

    // 죽을 때 화면 색을 뺀다: 채도·노출이 떨어지고 가장자리가 어두워지며 소리가 먹먹해진다 (이 씬의 포스트 프로세싱 복사본만 바꾼다)
    class DeathGrade
    {
        readonly ColorAdjustments color;
        readonly Vignette vignette;
        readonly AudioLowPassFilter lowPass;
        readonly float saturation, exposure, edge;

        public DeathGrade()
        {
            var volume = FindAnyObjectByType<Volume>();
            var profile = volume != null ? volume.profile : null;
            if (profile != null)
            {
                if (profile.TryGet(out color)) { saturation = color.saturation.value; exposure = color.postExposure.value; }
                if (profile.TryGet(out vignette)) edge = vignette.intensity.value;
            }
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener != null)
            {
                lowPass = listener.GetComponent<AudioLowPassFilter>();
                if (lowPass == null) lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
                lowPass.cutoffFrequency = 22000f;
            }
        }

        public void Apply(float k)
        {
            float e = k * k * (3 - 2 * k);
            if (color != null)
            {
                color.saturation.value = Mathf.Lerp(saturation, -85f, e);
                color.postExposure.value = exposure - 1.0f * e;
            }
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(edge, 0.8f, e);
            if (lowPass != null) lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(380f), e));
        }
    }

    // door: 방공호를 나서거나 돌아올 때 문이 덜컥 열리고(떠날 때) 덜컥 닫힌다(도착했을 때). loadingTitle: 로딩 화면 제목 (없으면 씬에 맞게)
    IEnumerator LoadRoutine(string scene, bool showResult, bool door = false, string loadingTitle = null)
    {
        IsTransitioning = true;
        UI.ClosePanel();
        UI.SetPaused(false);
        if (door) DoorClunk(1f);
        yield return UI.Fade(1, 0.7f);
        // 검은 화면 대신 로딩 화면 (적어도 1.2초는 보여 준다)
        UI.ShowLoading(loadingTitle ?? (scene == ShelterScene ? "방공호로 돌아가는 중" : scene == CityScene ? "도시로 나가는 중" : "메인 메뉴로 나가는 중"));
        float shownAt = Time.unscaledTime;
        var op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone) yield return null;
        yield return null;
        while (Time.unscaledTime - shownAt < 1.2f) yield return null;
        UI.HideLoading();
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
