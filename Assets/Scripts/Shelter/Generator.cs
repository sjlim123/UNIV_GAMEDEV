using UnityEngine;

// 발전기: F로 연료통을 넣는다. 넣으면 계기판에서 전기가 튀고 엔진이 울컥 빨라졌다가 가라앉는다
public class Generator : Interactable
{
    public AudioSource hum;

    float surge; // 연료를 넣은 직후 엔진이 빨라지는 만큼 (서서히 0으로)

    public override string Prompt => $"발전기 (전력 {GameManager.Instance.State.Power:0}%) - 연료 넣기";

    // 계기판 자리 (몸통 앞면, 로컬 +Z)
    Vector3 PanelPos => transform.TransformPoint(new Vector3(-0.15f, 0.62f, 0.43f));

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        if (s.Power >= 100f)
        {
            gm.UI.ShowMessage("전력이 이미 가득 차 있다.");
            return;
        }
        if (!s.ConsumeFromAnywhere("fuel", 1))
        {
            gm.UI.ShowMessage("연료가 없다. 도시에서 연료통을 찾아야 한다.");
            return;
        }
        s.Power = Mathf.Min(100, s.Power + GameBalance.Data.fuelPower);
        gm.UI.ShowMessage($"연료를 부었다. 전력 {s.Power:0}%");
        ProceduralAudio.PlayAt(ProceduralAudio.Clang(), transform.position, 0.35f, 0.6f);
        Vfx.Zap(PanelPos, transform.forward, 0.22f, 4);
        ProceduralAudio.PlayAt(EffectAudio.Zap(1), PanelPos, 0.7f, Random.Range(0.9f, 1.1f), 1f, 16f);
        surge = 0.35f;
        if (PlayerController.Instance != null) PlayerController.Instance.Shake(0.15f);
    }

    void Start()
    {
        if (hum != null && hum.clip == null) hum.clip = ProceduralAudio.Hum();
    }

    void Update()
    {
        if (hum == null) return;
        bool powered = GameManager.Instance.State.Power > 0;
        if (powered && !hum.isPlaying) hum.Play();
        if (!powered && hum.isPlaying) hum.Stop();
        surge = Mathf.MoveTowards(surge, 0, Time.deltaTime * 0.3f);
        hum.pitch = 0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 0.5f, 0) + surge;
    }
}
