using UnityEngine;

// 처음 하는 사람을 위한 안내. 게임의 목적(트럭을 고쳐 서울시 ○○ 대피소로 탈출)을 늘 보여 주고,
// 첫 바퀴를 '지금 할 일' 한 줄씩 이끈다: 라디오 → 제작대 → 탐색 → 줍기 → 복귀 → 잠 → 정비소.
// 할 일의 대상 위에는 맥동하는 호박색 불빛(Beacon)이 뜬다. 진행은 저장에 들어가고(GameState.TutorialStep),
// 다 끝나면 그 자리에 트럭 부품 진행도가 남는다. 한 번만 보여 주는 상황 힌트는 GameState.HintsSeen 비트로 기억한다.
// 화면 그리기는 UIManager.Hud(지금 할 일 명판)가 맡고, 게임 안 사건은 Notify로 들어온다
public static class Tutorial
{
    public struct Step { public string Title, Why, Target, Event; }

    public static readonly Step[] Steps =
    {
        new Step { Title = "라디오를 들어 본다", Why = "바깥 소식을 알아야 어디로 가야 할지 안다", Target = "Radio", Event = "radio" },
        new Step { Title = "제작대를 살펴본다", Why = "주운 재료로 무기·장비·트럭 부품을 만든다", Target = "Workbench", Event = "workbench" },
        new Step { Title = "해치로 나가 도시를 탐색한다", Why = "방공호엔 아무것도 없다. 재료는 밖에 있다", Target = "ShelterExit", Event = "expedition" },
        new Step { Title = "쓸 만한 물건을 3개 줍는다", Why = "건물 안을 뒤져라. 길바닥엔 돌뿐이다", Target = "", Event = "pickup" },
        new Step { Title = "붉은 밤이 오기 전에 해치로 돌아온다", Why = "화면 위 나침반의 집 표식이 해치. 남은 시간이 0이면 체력이 계속 준다", Target = "CityExit", Event = "return" },
        new Step { Title = "침대에서 잠을 자 하루를 넘긴다", Why = "잠을 자야 턴이 넘어가고 체력이 조금 돈다", Target = "Bed", Event = "rest" },
        new Step { Title = "트럭 정비소를 살펴본다", Why = "트럭이 완성되면 이 도시를 탈출한다", Target = "TruckSite", Event = "truck" },
    };

    public const string Purpose = "트럭을 고쳐 서울시 ○○ 대피소로 탈출한다";
    const int PickupGoal = 3;

    static GameState State => GameManager.Instance != null ? GameManager.Instance.State : null;
    public static int StepIndex => State != null ? State.TutorialStep : Steps.Length;
    public static bool Done => StepIndex >= Steps.Length;
    public static Step Current => Steps[Mathf.Clamp(StepIndex, 0, Steps.Length - 1)];

    // 지금 할 일 한 줄. 다 끝났으면 트럭 부품 진행도
    public static string Line
    {
        get
        {
            var s = State;
            if (s == null) return "";
            if (!Done)
            {
                var c = Current;
                return c.Event == "pickup" ? $"{c.Title}  ({Mathf.Min(s.TutorialCount, PickupGoal)} / {PickupGoal})" : c.Title;
            }
            int have = 0, need = 0;
            foreach (var kv in ItemDatabase.TruckParts)
            {
                need += kv.Value;
                have += Mathf.Min(s.Truck.Count(kv.Key), kv.Value);
            }
            return $"트럭 부품 {have} / {need}   ·   남은 턴 {Mathf.Max(0, GameState.MaxTurns - s.Turn)}";
        }
    }

    public static string Why => Done ? "부품은 제작대에서 만들어 트럭 정비소에 넣는다" : Current.Why;

    // 게임 안 사건이 지금 할 일(또는 그 뒤의 할 일)과 맞으면 그 단계까지 넘어간다.
    // 안내를 건너뛰고 먼저 밖에 나가 버려도 '제작대를 살펴본다'에 갇히지 않게 한다
    public static void Notify(string evt)
    {
        var s = State;
        if (s == null || Done) return;
        var c = Current;
        if (evt == "pickup")
        {
            if (c.Event != evt) return;
            s.TutorialCount++;
            if (s.TutorialCount < PickupGoal) return;
            s.TutorialStep++;
        }
        else
        {
            int at = -1;
            for (int i = s.TutorialStep; i < Steps.Length && at < 0; i++)
                if (Steps[i].Event == evt) at = i;
            if (at < 0) return;
            c = Steps[at];
            s.TutorialStep = at + 1;
        }
        if (!GameSettings.Tips) { RefreshBeacon(true); return; } // 안내를 꺼 두었으면 조용히 진행만 센다
        ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.5f, 1.6f, SoundCategory.UI);
        GameManager.Instance.UI.ShowMessage(Done ? "이제 혼자 해 나갈 수 있다. 트럭 부품을 모아 이 도시를 탈출하자." : $"<color=#d8a523>할 일 완료</color>  {c.Title}", 4f);
        RefreshBeacon(true);
    }

    // ---------- 한 번만 보여 주는 상황 힌트 ----------

    public const int HintInventory = 1, HintWorkbench = 2, HintBattery = 4, HintZombie = 8, HintHeavy = 16, HintRedNight = 32;

    public static void Hint(int bit, string text)
    {
        var s = State;
        if (s == null || !GameSettings.Tips || (s.HintsSeen & bit) != 0) return; // 꺼 두었으면 본 것으로 치지 않는다 (켜면 다음에 나온다)
        s.HintsSeen |= bit;
        GameManager.Instance.UI.ShowMessage("<color=#d8a523>힌트</color>  " + text, 6f);
    }

    // ---------- 대상 표시: 할 일의 대상 위에 떠서 맥동하는 호박색 불빛 ----------

    static GameObject beacon;
    static string beaconFor;

    public static void RefreshBeacon(bool force = false)
    {
        string want = Done || State == null || !GameSettings.Tips ? "" : Current.Target;
        string key = want + "@" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!force && key == beaconFor && (beacon != null || want == "")) return;
        beaconFor = key;
        if (beacon != null) Object.Destroy(beacon);
        beacon = null;
        if (want == "") return;
        var type = System.Type.GetType(want);
        if (type == null) return;
        var target = Object.FindFirstObjectByType(type) as Component;
        if (target == null) return;
        var b = new Bounds(target.transform.position, Vector3.one * 0.5f);
        foreach (var r in target.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        beacon = new GameObject("TutorialBeacon");
        beacon.transform.position = new Vector3(b.center.x, b.max.y + 0.45f, b.center.z);
        beacon.AddComponent<Beacon>();
    }

    class Beacon : MonoBehaviour
    {
        Light glow;
        Transform orb;
        float t;

        void Start()
        {
            var mat = ProcTex.Glow(new Color(1f, 0.7f, 0.25f), new Color(3f, 1.8f, 0.5f));
            orb = RuntimeVisuals.Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.12f, Color.white).transform;
            orb.GetComponent<Renderer>().sharedMaterial = mat;
            var ring = RuntimeVisuals.Part(PrimitiveType.Sphere, transform, Vector3.zero, new Vector3(0.34f, 0.015f, 0.34f), Color.white).transform;
            ring.GetComponent<Renderer>().sharedMaterial = mat;
            glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.72f, 0.3f);
            glow.range = 3.5f;
            glow.intensity = 1.2f;
            glow.shadows = LightShadows.None;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            t += Time.deltaTime;
            float p = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            orb.localPosition = new Vector3(0, 0.08f * Mathf.Sin(t * 2f), 0);
            orb.localScale = Vector3.one * (0.1f + 0.05f * p);
            glow.intensity = 0.8f + 1.0f * p;
            transform.Rotate(0, 50f * Time.deltaTime, 0);
        }
    }
}
