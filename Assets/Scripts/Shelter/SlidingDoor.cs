using UnityEngine;
using UnityEngine.AI;

// 미닫이 철문 (발전기실 입구): F로 드르륵 밀어 열고 닫는다. 문짝은 머리 위 레일을 따라 옆(로컬 -X)으로 미끄러지고 끝에서 탁 하고 멈춘다.
// 닫혀 있으면 좀비 길을 막고(NavMeshObstacle), 발전기 소리가 새어 나오지 않는다 (Generator가 Openness와 IsInside를 읽어 소리를 줄인다).
// 모양은 GameSceneBuilder.Shelter.cs의 BuildGenRoomDoor가 만든다
public class SlidingDoor : Interactable
{
    public static SlidingDoor Instance { get; private set; }

    public Transform leaf;          // 문짝 (로컬 -X 쪽으로 밀린다)
    public NavMeshObstacle obstacle;
    public AudioSource roll;        // 레일 위를 굴러가는 소리 (반복, 움직일 때만)
    public float slideDistance = 1.55f;
    public float seconds = 1.2f;    // 다 열리거나 닫히는 데 걸리는 시간
    public Vector3 insideDirection = Vector3.right; // 문 평면에서 발전기실 쪽 (월드)

    Vector3 closedPos;
    float openness, target, velocity; // 0 닫힘 ~ 1 열림
    bool moving;

    public float Openness => openness;
    public bool IsOpen => target > 0.5f;
    public override string Prompt => IsOpen ? "철문 닫기" : "철문 열기";

    // 이 자리가 발전기실 안쪽인가 (문 평면 기준)
    public bool IsInside(Vector3 p) => Vector3.Dot(p - transform.position, insideDirection) > 0f;

    void Awake()
    {
        Instance = this;
        if (leaf != null) closedPos = leaf.localPosition;
        if (roll != null)
        {
            roll.clip = EffectAudio.DoorRoll();
            roll.loop = true;
        }
        Apply();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void Interact(PlayerInteractor player)
    {
        target = IsOpen ? 0f : 1f;
        moving = true;
        if (roll != null && !roll.isPlaying) roll.Play();
        ProceduralAudio.PlayAt(EffectAudio.DoorUnlatch(), transform.position + Vector3.up * 1.1f, 0.55f, Random.Range(0.95f, 1.05f), 1f, 14f);
        PlayerController.Instance.MakeNoise(0.7f);
    }

    void Update()
    {
        if (!moving) return;
        // 무거운 문: 천천히 출발해 미끄러지다 끝에서 탁 멈춘다
        float before = openness;
        openness = Mathf.SmoothDamp(openness, target, ref velocity, seconds * 0.32f, 2.4f / seconds);
        float speed = Mathf.Abs(openness - before) / Mathf.Max(Time.deltaTime, 1e-4f); // 1/초
        if (roll != null)
        {
            roll.volume = Mathf.Clamp01(speed * 1.1f) * 0.55f;
            roll.pitch = 0.85f + Mathf.Clamp01(speed) * 0.3f;
        }
        if (Mathf.Abs(openness - target) < 0.012f)
        {
            openness = target;
            velocity = 0;
            moving = false;
            if (roll != null) roll.Stop();
            var at = transform.position + Vector3.up * 1.0f;
            ProceduralAudio.PlayAt(EffectAudio.DoorStop(), at, 0.85f, target > 0.5f ? 0.95f : 1.05f, 1f, 20f);
            PlayerController.Instance.MakeNoise(0.8f);
            if (PlayerController.Instance != null) PlayerController.Instance.Shake(0.07f);
        }
        Apply();
    }

    void Apply()
    {
        if (leaf != null) leaf.localPosition = closedPos + new Vector3(-slideDistance * openness, 0, 0);
        if (obstacle != null) obstacle.enabled = openness < 0.5f;
    }
}
