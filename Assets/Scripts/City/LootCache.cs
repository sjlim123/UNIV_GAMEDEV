using System.Collections;
using UnityEngine;

// 목적지 맨 끝의 보상 상자 (삼일아파트 102동 옥상의 통신 중계기함). 뜯어 열면 Destinations의 보상이 앞에 떨어지고,
// 그 목적지의 기록(clears)이 올라 다음에는 덜 나온다. 한 번 열면 그 탐험에서는 다시 열 수 없다.
// 뜯는 동안 쇳소리가 나서 주변이 듣고, 다 뜯으면 경보가 울려 근처의 감염자·약탈자가 몰려온다
public class LootCache : Interactable
{
    const float PrySeconds = 2.5f;           // 임시 수치: 다 뜯는 데 걸리는 시간(초)
    const float PryRange = 2.5f;             // 임시 수치: 뜯는 동안 이보다 멀어지면 손이 떨어진다 (m)
    const float ClangEvery = 0.6f;           // 임시 수치: 뜯는 동안 쇳소리가 나는 간격(초)
    const float ClangNoise = 2f;             // 임시 수치: 쇳소리의 크기 (걷기 1, 달리기 2. 감염자는 8m × 이 값까지 듣는다)
    static readonly bool AlarmOnOpen = true; // 임시 수치: 열리면 경보가 울리는가
    const float AlarmRadius = 30f;           // 임시 수치: 경보를 듣고 몰려오는 거리 (m)
    const int AlarmPulses = 3;               // 임시 수치: 경보음을 거는 횟수
    const float AlarmGap = 2.2f;             // 임시 수치: 경보음을 거는 간격(초). 경보음 하나가 1.1초 박자로 세 번 삐— 하므로, 그 배수여야 겹쳐 울려도 박자가 맞는다

    public string destinationId = "apt102";
    public string label = "통신 중계기함";
    public Transform[] dropPoints = new Transform[0];   // 물건이 떨어질 자리. 비어 있으면 상자 앞(+z) 0.8m
    public Transform door;                              // (없어도 된다) 열릴 때 로컬 Y로 -100도 도는 문짝
    public Light led;                                   // (없어도 된다) 목표 불빛. 열기 전에는 깜빡이고 열면 꺼진다
    public Renderer ledBulb;                            // (없어도 된다) led와 함께 켜고 끄는 전구 메시

    public bool IsOpen { get; private set; }

    AudioSource hum;
    bool prying, ledOn;
    float pry, clangTimer, reach;

    // 뜯는 동안의 숫자는 20%씩만 오른다. 안내문은 글자가 바뀔 때마다 톡 튀어나오는 움직임(0.16초)을 처음부터 다시 하므로,
    // 1%씩 올리면 뜯는 내내 글자가 흐릿하게 떨린다
    public override string Prompt => prying ? $"{label} - 뜯는 중… {Mathf.FloorToInt(Mathf.Clamp01(pry / PrySeconds) * 5) * 20}%" : $"{label} - 뜯어 연다";
    public override bool CanInteract => !IsOpen;

    void Start()
    {
        // 웅웅 소리는 자식 물체에 둔다 (저음 필터가 붙은 물체에 AudioSource를 더 붙이면 유니티가 오류를 낸다. Burning과 같은 방식)
        var ag = new GameObject("CacheHum");
        ag.transform.SetParent(transform, false);
        hum = ag.AddComponent<AudioSource>();
        ProceduralAudio.Setup3D(hum, 14f, 64, false);
        AudioCategory.Tag(hum, SoundCategory.Ambient);
        hum.clip = ProceduralAudio.Hum();
        hum.loop = true;
        hum.volume = 0.25f;
        hum.Play();
        SetLed(true);
    }

    // 한 번 누르면 뜯기 시작한다. 붙어 서 있는 동안 저절로 진행된다 (키를 누르고 있을 필요는 없다)
    public override void Interact(PlayerInteractor player)
    {
        if (IsOpen || prying || !Application.isPlaying) return;
        var pc = PlayerController.Instance;
        if (pc == null) return;
        prying = true;
        pry = 0;
        clangTimer = 0; // 첫 쇳소리는 바로 난다
        // 조준선이 겨우 닿는 거리에서 시작했어도 바로 손이 떨어지지 않게, 시작한 자리에서 조금은 여유를 둔다
        reach = Mathf.Max(PryRange, Mathf.Min(FlatDistance(pc), 3.5f) + 0.15f);
    }

    void Update()
    {
        if (IsOpen) return;
        bool on = Mathf.Repeat(Time.time, 1f) < 0.5f; // 목표 불빛: 0.5초 켜지고 0.5초 꺼진다
        if (on != ledOn) SetLed(on);
        if (!prying) return;

        var pc = PlayerController.Instance;
        var gm = pc != null ? GameManager.Instance : null;
        if (gm == null || gm.State == null || gm.IsGameOver || gm.IsTransitioning)
        {
            prying = false;
            return;
        }
        if (FlatDistance(pc) > reach)
        {
            prying = false;
            gm.UI.ShowMessage("손이 떨어졌다. 다시 뜯어야 한다.", 2f);
            return;
        }
        float dt = Time.deltaTime;
        clangTimer -= dt;
        if (clangTimer <= 0)
        {
            clangTimer += ClangEvery;
            ProceduralAudio.PlayAt(ProceduralAudio.Clang(), transform.position, 0.8f, Random.Range(0.9f, 1.1f));
            Vfx.SparkBurst(FrontPoint(), transform.forward, 6);
            pc.MakeNoise(ClangNoise);
        }
        pry += dt;
        if (pry >= PrySeconds) Open(gm);
    }

    // 플레이어까지의 수평 거리. 층이 다르면 닿지 않는 것으로 친다 (뜯다가 바로 옆 바닥 구멍으로 떨어진 경우)
    float FlatDistance(PlayerController pc)
    {
        var d = pc.transform.position - transform.position;
        if (Mathf.Abs(d.y) > 2.5f) return float.MaxValue;
        d.y = 0;
        return d.magnitude;
    }

    // 상자 앞면 가운데에서 조금 앞 (쇳소리와 함께 불꽃이 튀는 자리: 앞에 덧댄 문짝에 가리지 않게 8cm 띄운다).
    // 콜라이더 크기로 잡고, 없으면 상자 자리에서 조금 앞
    Vector3 FrontPoint()
    {
        var f = transform.forward;
        var col = GetComponentInChildren<Collider>();
        if (col == null) return transform.position + f * 0.3f;
        var b = col.bounds;
        float depth = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.y) * b.extents.y + Mathf.Abs(f.z) * b.extents.z;
        return b.center + f * (depth + 0.08f);
    }

    void SetLed(bool on)
    {
        ledOn = on;
        if (led != null) led.enabled = on;
        if (ledBulb != null) ledBulb.enabled = on;
    }

    // i번째 물건이 떨어질 자리 (dropPoints를 돌아가며 쓴다. 없으면 상자 앞에 나란히)
    Vector3 DropSpot(int i)
    {
        if (dropPoints != null && dropPoints.Length > 0)
        {
            var t = dropPoints[i % dropPoints.Length];
            if (t != null) return t.position;
        }
        return transform.position + transform.forward * 0.8f + transform.right * ((i % 3 - 1) * 0.35f) + Vector3.up * 0.1f;
    }

    void Open(GameManager gm)
    {
        prying = false;
        IsOpen = true;
        var pos = transform.position;
        var dest = Destinations.Get(destinationId);
        // 표에 없는 목적지면 기록을 만들지 않는다 (저장 파일에 쓸모없는 줄이 남는다)
        var rec = dest != null ? gm.State.Record(dest.Id) : null;
        // 지금까지 연 횟수로 이번에 나올 것이 정해진다 (처음이 가장 좋고, 표의 마지막 줄이 되풀이된다)
        int before = rec != null ? rec.clears : 0;
        int rows = dest != null && dest.Rewards != null ? dest.Rewards.Length : 0;
        int placed = 0;
        foreach (var it in Destinations.RewardFor(dest, before))
        {
            if (it == null || it.count <= 0 || string.IsNullOrEmpty(it.id) || !ItemDatabase.Items.ContainsKey(it.id)) continue;
            PickupItem.Spawn(it.id, it.count, DropSpot(placed));
            placed++;
        }
        if (rec != null) rec.clears = before + 1; // 저장은 방공호에 돌아갔을 때 된다 (도중에 그만두면 연 것으로 치지 않는다)

        if (hum != null) hum.Stop();
        SetLed(false);
        if (door != null) StartCoroutine(SwingDoor());
        ProceduralAudio.PlayAt(ProceduralAudio.Creak(), pos, 0.7f, Random.Range(1.1f, 1.25f));

        if (placed > 0)
        {
            bool last = rows > 1 && before >= rows - 1;
            string text = before <= 0 ? "중계기함을 뜯었다. 쓸 만한 부품이 남아 있다."
                : last ? "중계기함은 거의 비었다. 자투리만 챙겼다."
                : "중계기함을 다시 뜯었다. 부품이 조금 남아 있다.";
            if (AlarmOnOpen) text += " 경보음이 울린다!";
            gm.UI.ShowMessage(text, 4f);
        }
        else Debug.LogWarning($"[LootCache] 목적지 '{destinationId}'에 줄 보상이 없습니다 (Destinations.Rewards).");
        if (AlarmOnOpen) StartCoroutine(Alarm(pos));
    }

    // 문짝이 0.4초에 걸쳐 로컬 Y로 -100도 돈다
    IEnumerator SwingDoor()
    {
        var from = door.localRotation;
        var to = from * Quaternion.Euler(0, -100f, 0);
        for (float t = 0; t < 0.4f; t += Time.deltaTime)
        {
            if (door == null) yield break;
            door.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t / 0.4f));
            yield return null;
        }
        if (door != null) door.localRotation = to;
    }

    // 경보: AlarmGap초 간격으로 AlarmPulses번 건다. 첫 소리에 AlarmRadius 안의 살아 있는 감염자·약탈자가 상자 쪽으로 몰려온다
    IEnumerator Alarm(Vector3 pos)
    {
        for (int i = 0; i < AlarmPulses; i++)
        {
            if (i > 0) yield return new WaitForSeconds(AlarmGap);
            ProceduralAudio.PlayAt(ProceduralAudio.AlertTone(), pos, 1f, 1f, 1f, 60f);
            if (i > 0) continue;
            foreach (var z in FindObjectsByType<Zombie>(FindObjectsSortMode.None))
                if (z != null && !z.IsDead && Vector3.Distance(z.transform.position, pos) <= AlarmRadius) z.Alert(pos);
        }
    }
}
