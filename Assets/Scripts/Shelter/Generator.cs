using UnityEngine;

public class Generator : Interactable
{
    public AudioSource hum;

    public override string Prompt => $"발전기 (전력 {GameManager.Instance.State.Power:0}%) - 연료 넣기";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        if (!s.ConsumeFromAnywhere("fuel", 1))
        {
            gm.UI.ShowMessage("연료가 없다. 도시에서 연료통을 찾아야 한다.");
            return;
        }
        s.Power = Mathf.Min(100, s.Power + GameBalance.Data.fuelPower);
        gm.UI.ShowMessage($"연료를 부었다. 전력 {s.Power:0}%");
        ProceduralAudio.PlayAt(ProceduralAudio.Clang(), transform.position, 0.5f, 0.6f);
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
        hum.pitch = 0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 0.5f, 0);
    }
}
