using UnityEngine;
using UnityEngine.AI;

// 복도 옆 옛 통로를 막는 방폭문. 전력이 있는 동안 닫혀 있고, 정전되면 옆으로 밀려 열린다 (ShelterInvasion이 연다).
// 움직이기 시작하면 공압 장치가 치이익 하며 문 위 배출구에서 흰 증기가 문짝을 타고 아래로 흐르고, 끝에 닿으면 밀봉되는 치익과 둔탁한 쿵,
// 닫힐 때는 문틀 틈을 따라 먼지·녹 부스러기·불꽃이 튄다.
// 이 오브젝트(문틀)의 로컬 +Z가 통로 쪽이고, 문짝(leaf)은 로컬 +X로 밀려 벽 속에 들어간다.
// 닫혀 있을 때만 NavMeshObstacle로 내비메시를 끊는다 (씬을 구울 때는 문을 빼고 굽는다).
public class BlastDoor : Interactable
{
    public Transform leaf;
    public NavMeshObstacle obstacle;
    public Light warningLight;
    public AudioSource motor;
    public float width = 1.6f;          // 문 구멍 너비
    public float slideDistance = 1.75f;
    public float moveSeconds = 3.2f;
    [Tooltip("문 바깥 통로 영역(월드 좌표). 이 안의 좀비는 아직 들어오지 않은 것으로 본다")]
    public Bounds outsideArea;

    Vector3 closedPos;
    float amount, target, warnBase, debrisTimer, vaporLeft, vaporTimer, vaporStrength;

    public bool IsOpen => target > 0.5f;            // 열렸거나 열리는 중
    public bool IsMoving => amount != target;
    public bool IsClosed => target <= 0f && amount <= 0f;

    public override string Prompt => "방폭문 - 굳게 잠겨 있다";
    public override bool CanInteract => IsClosed;

    public override void Interact(PlayerInteractor player)
    {
        GameManager.Instance.UI.ShowMessage("발전기 전력으로 잠긴 방폭문이다. 전력이 끊기면… 열려 버린다.", 3.5f);
        ProceduralAudio.PlayAt(ProceduralAudio.Bang(), leaf.position, 0.35f, 1.3f);
    }

    void Awake()
    {
        if (leaf != null) closedPos = leaf.localPosition;
        if (warningLight != null)
        {
            warnBase = warningLight.intensity;
            warningLight.enabled = false;
        }
    }

    void Start()
    {
        if (motor != null && motor.clip == null) motor.clip = RadioAudio.DoorGrind();
        RadioAudio.DoorBoom(); // 처음 움직일 때 끊기지 않게 미리 만든다
        EffectAudio.Pneumatic(true);
        EffectAudio.Pneumatic(false);
    }

    public void Open() => SetTarget(1);
    public void Close() => SetTarget(0);

    void SetTarget(float value)
    {
        if (target == value) return;
        target = value;
        // 잠금장치가 풀리는 철컥 + 압축 공기가 치이익 빠지며 흰 증기가 문 위 배출구에서 아래로 흐른다 + 모터 + 문틀에서 쏟아지는 먼지·녹
        if (leaf != null)
        {
            ProceduralAudio.PlayAt(ProceduralAudio.Clang(), leaf.position, 0.5f, 0.45f, 1f, 40f);
            ProceduralAudio.PlayAt(EffectAudio.Pneumatic(true), leaf.position + Vector3.up * 2.1f, 0.9f, Random.Range(0.95f, 1.05f), 1f, 36f);
        }
        if (motor != null && !motor.isPlaying) motor.Play();
        Debris(1.6f);
        vaporLeft = 1.4f;
        vaporStrength = 1f;
        for (int i = 0; i < 14; i++) Vapor(1.3f); // 압력이 빠지는 순간 확 뿜는다
    }

    // 문 위 틀 모서리(문짝이 지나가는 길)에서 콘크리트 먼지와 녹 조각이 떨어진다
    void Debris(float strength)
    {
        if (leaf == null) return;
        var a = transform.TransformPoint(new Vector3(-width / 2, 2.25f, 0));
        var b = transform.TransformPoint(new Vector3(width / 2 + slideDistance * amount, 2.25f, 0));
        Vfx.DoorDebris(Vfx.DoorKind.Blast, a, b, transform.forward, strength);
    }

    void Update()
    {
        if (leaf == null) return;
        if (amount != target)
        {
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime / moveSeconds);
            if (amount == target) Arrive();
        }

        // 움직이는 동안 조금씩 먼지가 흩날린다
        if (IsMoving)
        {
            debrisTimer -= Time.deltaTime;
            if (debrisTimer <= 0) { debrisTimer = 0.22f; Debris(0.3f); }
        }
        // 흰 증기: 문 위 배출구 넷(양면)에서 문짝을 타고 아래로 흘러내린다
        if (vaporLeft > 0)
        {
            vaporLeft -= Time.deltaTime;
            vaporTimer -= Time.deltaTime;
            if (vaporTimer <= 0)
            {
                vaporTimer = 0.02f;
                Vapor(vaporStrength * Mathf.Clamp01(vaporLeft / 0.5f + 0.3f));
            }
        }
        // 무거운 문이 덜컹거리며 미끄러진다
        float wobble = IsMoving ? (Mathf.PerlinNoise(Time.time * 18f, 0) - 0.5f) * 0.012f : 0;
        leaf.localPosition = closedPos + new Vector3(Mathf.SmoothStep(0, 1, amount) * slideDistance, wobble, 0);
        if (obstacle != null) obstacle.enabled = amount < 0.6f;

        // 경고등: 열려 있거나 움직이는 동안 붉게 깜빡인다
        if (warningLight != null)
        {
            bool on = amount > 0 || target > 0;
            warningLight.enabled = on;
            if (on) warningLight.intensity = warnBase * (0.2f + 0.8f * Mathf.Abs(Mathf.Sin(Time.time * 3f)));
        }
    }

    void Arrive()
    {
        if (motor != null) motor.Stop();
        // 밀봉되는 치익 + 둔탁한 쿵 (예전의 쩌렁쩌렁한 둥은 줄였다)
        ProceduralAudio.PlayAt(EffectAudio.Pneumatic(false), leaf.position + Vector3.up * 2.1f, 0.8f, Random.Range(0.95f, 1.05f), 1f, 36f);
        ProceduralAudio.PlayAt(RadioAudio.DoorBoom(), leaf.position, 0.4f, 0.9f, 1f, 40f);
        vaporLeft = 0.6f;
        vaporStrength = 0.6f;
        var player = PlayerController.Instance;
        if (player != null && Vector3.Distance(player.transform.position, transform.position) < 12f) player.Shake(target <= 0f ? 0.22f : 0.14f);
        // 닫히며 문짝 앞 가장자리가 문틀에 닿는 자리: 틈을 따라 양면으로 먼지·부스러기·불꽃. 열려서 벽 속에 들어갈 때는 벽 틈에서 약하게
        float edgeX = target <= 0f ? -width / 2 - 0.03f : width / 2 + 0.03f;
        float strength = target <= 0f ? 1f : 0.45f;
        foreach (var side in new[] { -1f, 1f })
        {
            var a = transform.TransformPoint(new Vector3(edgeX, 0.1f, side * 0.17f));
            var b = transform.TransformPoint(new Vector3(edgeX, 2.2f, side * 0.17f));
            Vfx.SeamSlam(a, b, transform.forward * side, strength);
        }
    }

    // 문 위 가장자리의 배출 틈(양면, 문 너비 전체)에서 흰 증기가 커튼처럼 문짝을 타고 아래로 흘러내린다
    void Vapor(float strength)
    {
        for (int k = 0; k < 3; k++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = Random.Range(-0.8f, 0.8f);
            var pos = transform.TransformPoint(new Vector3(x, 2.2f, side * 0.13f));
            var vel = Vector3.down * Random.Range(1.2f, 1.9f) + transform.forward * side * Random.Range(0.06f, 0.2f) + Random.insideUnitSphere * 0.06f;
            Vfx.Vapor(pos, vel * (0.6f + 0.4f * strength), Random.Range(0.9f, 1.4f), Random.Range(0.11f, 0.2f), Random.Range(0.5f, 0.8f) * strength);
        }
    }

    // 문간(문짝이 지나가는 자리 근처)에 있는가
    public bool InDoorway(Vector3 worldPos, float margin = 0.45f)
    {
        var p = transform.InverseTransformPoint(worldPos);
        return Mathf.Abs(p.x) < width / 2 + margin && Mathf.Abs(p.z) < 0.5f + margin;
    }

    // 문 바깥(통로 쪽)에 있는가
    public bool IsBeyond(Vector3 worldPos) => outsideArea.Contains(worldPos);
}
