using UnityEngine;
using UnityEngine.InputSystem;

// Q로 손전등을 켜고 끈다. 켜 두면 배터리가 닳고, 다 닳으면 여분 배터리로 자동 교체한다.
public class PlayerFlashlight : MonoBehaviour
{
    public Light spot;
    public float baseIntensity = 48f;

    bool on;
    float flickerSeed;

    public bool IsOn => on;

    void Start()
    {
        flickerSeed = Random.value * 100;
        spot.enabled = false;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        bool has = s.Carried.Count("flashlight") > 0;
        if (!has) on = false;

        if (has && KeyBindings.Pressed(GameAction.Flashlight) && !PlayerController.Instance.InputBlocked)
        {
            on = !on;
            ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.6f);
            if (on && s.FlashlightCharge <= 0) ReloadOrFail(s);
        }

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
        spot.enabled = on;
        spot.intensity = intensity;
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
