using UnityEngine;

// 방공호 서쪽 벽의 낡은 벽시계: 3시 31분에 멈춘 채 붉은 초침만 째깍인다 (한 칸 가서 살짝 넘쳤다 돌아온다, 가까이서는 째깍 소리).
// 시침·분침은 빌더(GameSceneBuilder.ShelterWall.cs)가 멈춘 각도로 세워 두고, 초침 피벗만 여기에 넘긴다 (로컬 z 둘레로 시계 방향 +).
// F로 보면 멈춘 시계 메시지가 뜬다. 배터리 시계라 정전과 상관없이 간다
public class WallClock : Interactable
{
    public Transform secondHand;      // 초침 피벗 (빌더가 넣는다)
    public float tickVolume = 0.05f;  // 째깍 소리 (6m 안에서만 들린다)

    const float StepSeconds = 0.11f;  // 초침이 한 칸(6°) 가는 시간
    const float Overshoot = 1.6f;     // 칸 끝에서 넘치는 각도 (도)

    int lastSecond = -1;
    float tickAt = -10f;
    AudioClip tick;

    public override string Prompt => "벽시계 보기";

    public override void Interact(PlayerInteractor player)
    {
        GameManager.Instance.UI.ShowMessage("3시 31분.... 초침은 돌아가지만 나머지는 멈춰 있다. 고장난 듯하다.", 4f);
    }

    void Start()
    {
        lastSecond = Mathf.FloorToInt(Time.time) % 60;
        tickAt = Time.time - 1f;
    }

    void Update()
    {
        if (secondHand == null) return;
        int sec = Mathf.FloorToInt(Time.time) % 60;
        if (sec != lastSecond)
        {
            lastSecond = sec;
            tickAt = Time.time;
            var pc = PlayerController.Instance;
            if (tickVolume > 0 && pc != null && (pc.transform.position - transform.position).sqrMagnitude < 36f)
            {
                if (tick == null) tick = ProceduralAudio.Click();
                ProceduralAudio.PlayAt(tick, transform.position, tickVolume, sec % 2 == 0 ? 1.75f : 1.5f, 1f, 6f, SoundCategory.Ambient);
            }
        }
        // 지난 칸에서 이번 칸으로: 빨리 가서 끝에서 살짝 넘쳤다가 멈춘다
        float u = Mathf.Clamp01((Time.time - tickAt) / StepSeconds);
        float ease = 1f - (1f - u) * (1f - u) * (1f - u);
        float angle = lastSecond * 6f - 6f * (1f - ease) + Overshoot * Mathf.Sin(u * Mathf.PI);
        secondHand.localRotation = Quaternion.Euler(0, 0, angle);
    }
}
