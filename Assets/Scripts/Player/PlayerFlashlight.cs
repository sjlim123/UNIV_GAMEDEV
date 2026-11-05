using UnityEngine;

// 손전등: 손전등 키(기본 Q)로 왼손에 꺼내 켜고, 다시 누르면 끄며 넣는다 (상호작용 키는 손전등을 건드리지 않는다. 핫바 우클릭도 같은 일을 한다).
// 켜 두면 배터리가 닳고, 다 닳으면 불이 꺼진 채 문구로 알린다. 저절로 갈아 끼우지 않는다: 장전 키(기본 R)를 눌러 여분 배터리로 직접 교체한다
// (SwapBelow% 아래로 떨어졌을 때만 — 멀쩡한 배터리를 실수로 버리지 않게).
// 꺼낸 상태는 GameState.FlashlightOut에 남아 씬을 옮기거나 저장해도 이어진다. 손 모양(꺼내는 동작·드는 자세)은 PlayerHands가 IsOut·IsOn을 읽어 만든다
public class PlayerFlashlight : MonoBehaviour
{
    public static PlayerFlashlight Instance { get; private set; }

    public Light spot;
    public float Dim = 1f; // 연출용 세기 배율 (출발 연출: 50cm 앞 철문을 비추면 하얗게 날아가므로 낮춘다). 씬이 바뀌면 새 플레이어라 되돌릴 필요 없다
    public float baseIntensity = 48f;

    // 배터리가 이만큼(%) 아래로 내려와야 갈아 끼울 수 있다
    public const float SwapBelow = 25f;

    bool on;
    float flickerSeed;

    public bool IsOn => on;
    // 왼손에 꺼내 들고 있는가 (배터리가 다 됐으면 꺼진 채 들고 있다)
    public bool IsOut => Has && GameManager.Instance.State.FlashlightOut;

    static bool Has => GameManager.Instance != null && GameManager.Instance.State.Carried.Count("flashlight") > 0;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        flickerSeed = Random.value * 100;
        if (spot != null) spot.enabled = false;
        // 꺼낸 채 씬을 옮겨 왔으면(또는 저장을 이어 하면) 켜진 채로 시작한다
        var s = GameManager.Instance.State;
        on = Has && s.FlashlightOut && s.FlashlightCharge > 0;
    }

    // 손전등 키·핫바 우클릭: 꺼내며 켜거나, 끄며 넣는다. 손전등이 없으면 아무 일도 없다
    public void Toggle()
    {
        if (!Has) return;
        SetOut(!GameManager.Instance.State.FlashlightOut);
    }

    // 예전 이름 (연출 코드가 출발 때 넣었다가 다시 꺼내는 데 쓴다). Toggle과 같다 — 넣기/꺼내기를 꼭 집어 하려면 SetOut(false)/SetOut(true)
    public void ToggleOut() => Toggle();

    public void SetOut(bool value)
    {
        var s = GameManager.Instance.State;
        if (!Has) value = false;
        if (s.FlashlightOut == value) return;
        s.FlashlightOut = value;
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.4f, value ? 1.15f : 0.9f);
        if (value)
        {
            on = s.FlashlightCharge > 0;
            ProceduralAudio.Play2D(ProceduralAudio.Click(), on ? 0.5f : 0.35f);
            if (!on) Depleted(s);
        }
        else on = false;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        bool has = Has;
        if (!has)
        {
            on = false;
            s.FlashlightOut = false;
        }

        bool blocked = PlayerController.Instance == null || PlayerController.Instance.InputBlocked;
        if (has && !blocked)
        {
            if (KeyBindings.Pressed(GameAction.Flashlight)) Toggle();
            if (KeyBindings.Pressed(GameAction.Reload)) TrySwapBattery(s);
        }
        if (!s.FlashlightOut) on = false;

        if (on)
        {
            s.FlashlightCharge -= 100f / GameBalance.Data.flashlightSecondsPerBattery * Time.deltaTime;
            if (s.FlashlightCharge <= 0)
            {
                // 다 됐다: 불이 꺼지고 (손에는 든 채) 갈아 끼우라고 알린다
                s.FlashlightCharge = 0;
                on = false;
                ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.4f, 0.8f);
                Depleted(s);
            }
        }

        // 배터리가 얼마 안 남으면 깜빡인다
        float intensity = baseIntensity * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(s.FlashlightCharge / 40f));
        if (s.FlashlightCharge < 15f)
        {
            float n = Mathf.PerlinNoise(Time.time * 9f, flickerSeed);
            intensity *= n < 0.3f ? 0.1f : 1f;
        }
        if (spot == null) return;
        spot.enabled = on;
        spot.intensity = intensity * Dim;
    }

    // 배터리가 다 됐을 때 문구: 여분이 있으면 장전 키를 알려 준다
    void Depleted(GameState s)
    {
        string swap = s.Carried.Count("battery") > 0
            ? $"{KeyBindings.Label(GameAction.Reload)} 키를 눌러 배터리를 갈아 끼우자."
            : "여분 배터리가 없다.";
        GameManager.Instance.UI.ShowMessage("손전등 배터리가 다 됐다. " + swap, 3.5f);
    }

    // 장전 키: 여분 배터리로 갈아 끼운다 (SwapBelow% 아래일 때만). 꺼내 든 채였으면 바로 다시 켜진다
    public void TrySwapBattery(GameState s)
    {
        var ui = GameManager.Instance.UI;
        if (s.FlashlightCharge >= SwapBelow)
        {
            ui.ShowMessage($"배터리가 아직 {s.FlashlightCharge:0}% 남아 있다. {SwapBelow:0}% 아래로 떨어지면 갈아 끼울 수 있다.");
            return;
        }
        if (!s.Carried.Remove("battery"))
        {
            ui.ShowMessage("여분 배터리가 없다. 배터리를 찾거나 제작대에서 만들자.");
            return;
        }
        s.FlashlightCharge = 100;
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.45f, 1.05f);
        ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.55f, 1.1f);
        if (s.FlashlightOut) on = true;
        ui.ShowMessage("배터리를 갈아 끼웠다." + (s.FlashlightOut ? " 손전등이 다시 켜진다." : ""));
    }
}
