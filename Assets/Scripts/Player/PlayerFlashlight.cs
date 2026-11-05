using UnityEngine;

// 손전등: 상호작용 키(기본 F)로 왼손에 꺼내거나 넣는다 (조준선에 문·장치·아이템이 있으면 그쪽이 먼저다 — PlayerInteractor가 가른다).
// 꺼내면 바로 켜지고 넣으면 꺼진다. 꺼낸 채로는 손전등 키(기본 Q)로 켜고 끈다 (넣어 둔 채 Q를 누르면 꺼내면서 켠다).
// 켜 두면 배터리가 닳고, 다 닳으면 여분 배터리로 자동 교체한다. 꺼낸 상태는 GameState.FlashlightOut에 남아 씬을 옮기거나 저장해도 이어진다.
// 손 모양(꺼내는 동작·드는 자세)은 PlayerHands가 IsOut·IsOn을 읽어 만든다
public class PlayerFlashlight : MonoBehaviour
{
    public static PlayerFlashlight Instance { get; private set; }

    public Light spot;
    public float Dim = 1f; // 연출용 세기 배율 (출발 연출: 50cm 앞 철문을 비추면 하얗게 날아가므로 낮춘다). 씬이 바뀌면 새 플레이어라 되돌릴 필요 없다
    public float baseIntensity = 48f;

    bool on;
    float flickerSeed;

    public bool IsOn => on;
    // 왼손에 꺼내 들고 있는가
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

    // 상호작용 키(보는 것이 없을 때)·핫바 우클릭: 꺼내거나 넣는다. 손전등이 없으면 아무 일도 없다
    public void ToggleOut()
    {
        if (!Has) return;
        SetOut(!GameManager.Instance.State.FlashlightOut);
    }

    public void SetOut(bool value)
    {
        var s = GameManager.Instance.State;
        if (!Has) value = false;
        if (s.FlashlightOut == value) return;
        s.FlashlightOut = value;
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.4f, value ? 1.15f : 0.9f);
        if (value)
        {
            on = true;
            if (s.FlashlightCharge <= 0) ReloadOrFail(s);
            if (on) ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.5f);
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

        if (has && KeyBindings.Pressed(GameAction.Flashlight) && !PlayerController.Instance.InputBlocked)
        {
            if (!s.FlashlightOut) SetOut(true);
            else
            {
                on = !on;
                ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.6f);
                if (on && s.FlashlightCharge <= 0) ReloadOrFail(s);
            }
        }
        if (!s.FlashlightOut) on = false;

        if (on)
        {
            s.FlashlightCharge -= 100f / GameBalance.Data.flashlightSecondsPerBattery * Time.deltaTime;
            if (s.FlashlightCharge <= 0) ReloadOrFail(s);
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

    void ReloadOrFail(GameState s)
    {
        if (s.Carried.Remove("battery"))
        {
            s.FlashlightCharge = 100;
            GameManager.Instance.UI.ShowMessage("배터리를 갈아 끼웠다.");
        }
        else
        {
            s.FlashlightCharge = 0;
            on = false;
            GameManager.Instance.UI.ShowMessage("손전등 배터리가 다 됐다...");
        }
    }
}
