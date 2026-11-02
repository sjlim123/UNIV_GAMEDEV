using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 들개: 떠돌다가 플레이어를 보거나 들으면 으르렁거리며 경고한 뒤 달려들어 문다.
// 수치(피·데미지·속도·공격 간격)는 GameBalance.dog. 죽이면 가죽 1개와 고기 1개를 떨어뜨린다.
[RequireComponent(typeof(NavMeshAgent))]
public class Animal : MonoBehaviour, IDamageable
{
    public static readonly List<Animal> All = new List<Animal>();

    // 다리 관절 (Awake에서 몸을 지을 때 채운다: 앞왼·앞오·뒤왼·뒤오의 위·아래)
    public Transform[] legs;
    Transform[] knees;
    Transform head, jaw, neck, tail, body;
    // 눈이 빛나는 재질의 원본 (빌드에 발광 셰이더가 들어가게 프리팹이 들고 있는다)
    public Material eyeTemplate;
    public float walkSpeed = 1.2f;
    [Tooltip("시야(m)")]
    public float sightRange = 14f;
    [Tooltip("플레이어 소음 1당 들리는 거리(m)")]
    public float hearingRange = 9f;
    public float biteRange = 1.3f;
    [Tooltip("달려들기 전에 으르렁거리며 경고하는 시간(초)")]
    public float warnTime = 1.4f;
    [Tooltip("한 마리가 달려들면 이 거리 안의 들개도 따라온다")]
    public float packRadius = 15f;

    enum State { Roam, Warn, Chase, Dead }

    State state;
    NavMeshAgent agent;
    AudioSource voice;
    EnemyStats stats;
    PlayerFlashlight playerLight;
    HitFlash flash;
    float health, wanderTimer, warnTimer, biteTimer, barkTimer, repathTimer, staggerTimer, lostTimer, animPhase, lunge, hitReact;
    float provokeCooldown; // 경고하다 물러난 뒤 다시 으르렁거리기까지 쉬는 시간

    public bool IsDead => state == State.Dead;
    public bool IsChasing => state == State.Chase;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GameBalance.Data.dog;
        health = stats.health;
        voice = gameObject.AddComponent<AudioSource>();
        ProceduralAudio.Setup3D(voice, 36f, 64);
        // 디자인 시트의 들개 몸을 짓는다 (예전 상자 모양 몸은 지운다)
        if (eyeTemplate != null) ProcTex.GlowTemplate = eyeTemplate;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name != "Model" && c.GetComponent<Renderer>() != null || c.name.StartsWith("Leg")) Destroy(c.gameObject);
        }
        var rig = CharacterModels.Build(transform, FigureKind.Dog);
        legs = rig.Legs;
        knees = rig.Knees;
        head = rig.Head;
        jaw = rig.Jaw;
        neck = rig.Neck;
        tail = rig.Tail;
        body = rig.Body;
        flash = HitFlash.For(gameObject, rig.Renderers);
        animPhase = Random.value * 10;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        if (state == State.Dead) return;
        float dt = Time.deltaTime;
        var player = PlayerController.Instance;
        if (player == null || GameManager.Instance.IsGameOver || !agent.isOnNavMesh) { Animate(dt); return; }

        float danger = CityDirector.Instance != null ? CityDirector.Instance.Danger : 0;
        Vector3 playerPos = player.transform.position;
        float dist = Vector3.Distance(transform.position, playerPos);
        staggerTimer -= dt;
        biteTimer -= dt;
        repathTimer -= dt;
        agent.isStopped = staggerTimer > 0 || state == State.Warn;

        switch (state)
        {
            case State.Roam:
                Roam(dt);
                provokeCooldown -= dt;
                if (dist < 50f && provokeCooldown <= 0 && Notices(player, dist)) Provoke(warnTime);
                break;

            case State.Warn:
                // 머리를 낮추고 으르렁거리며 노려본다. 그 사이 멀리 물러나면 쫓아오지 않는다
                FaceTowards(playerPos);
                warnTimer -= dt;
                if (dist > SightRange(player) * 1.3f) { state = State.Roam; provokeCooldown = 3f; }
                else if (warnTimer <= 0) StartChase();
                break;

            case State.Chase:
                agent.speed = GameBalance.Data.ToMetersPerSecond(stats.speed) * (1 + 0.2f * danger);
                if (repathTimer <= 0) { agent.SetDestination(playerPos); repathTimer = 0.2f; }
                if (dist < biteRange + 0.5f) FaceTowards(playerPos);
                if (dist < biteRange && biteTimer <= 0 && staggerTimer <= 0 && Mathf.Abs(playerPos.y - transform.position.y) < 1.2f) Bite(playerPos, danger);
                // 오래 놓치거나 너무 멀어지면 포기한다
                lostTimer = Sees(player, dist, 360f) ? 0 : lostTimer + dt;
                if (lostTimer > 8f || dist > 40f) { state = State.Roam; wanderTimer = 0; }
                barkTimer -= dt;
                if (barkTimer <= 0) { barkTimer = Random.Range(1.2f, 2.8f); Bark(1f, Random.Range(0.9f, 1.15f)); }
                break;
        }
        Animate(dt);
    }

    void Roam(float dt)
    {
        agent.speed = walkSpeed;
        wanderTimer -= dt;
        if (wanderTimer > 0) return;
        wanderTimer = Random.Range(4f, 9f);
        var r = Random.insideUnitCircle * 8f;
        if (NavMesh.SamplePosition(transform.position + new Vector3(r.x, 0, r.y), out var hit, 3f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    bool Notices(PlayerController p, float dist)
    {
        if (Sees(p, dist, 140f)) return true;
        return p.Noise > 0 && dist < hearingRange * p.Noise;
    }

    // 앉으면 덜 보이고, 손전등을 켜면 멀리서도 보인다
    float SightRange(PlayerController p)
    {
        float range = sightRange;
        if (p.IsCrouching) range *= 0.6f;
        if (playerLight == null) playerLight = p.GetComponent<PlayerFlashlight>();
        if (playerLight != null && playerLight.IsOn) range *= GameBalance.Data.flashlightVisibility;
        return range;
    }

    bool Sees(PlayerController p, float dist, float fov)
    {
        if (dist > SightRange(p)) return false;
        // 눈은 콜라이더 바깥(주둥이 쪽)에서 본다
        Vector3 eye = transform.position + Vector3.up * 0.72f + transform.forward * 0.55f;
        Vector3 target = p.cameraRoot.position;
        if (dist > 2f && Vector3.Angle(transform.forward, target - eye) > fov * 0.5f) return false;
        if (Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == p.transform || hit.transform.IsChildOf(p.transform);
        return true;
    }

    // 으르렁거리며 경고를 시작한다 (delay초 뒤 달려든다)
    void Provoke(float delay)
    {
        if (state != State.Roam) return;
        state = State.Warn;
        warnTimer = delay;
        if (agent.isOnNavMesh && agent.hasPath) agent.ResetPath();
        voice.pitch = Random.Range(0.9f, 1.1f);
        voice.PlayOneShot(ProceduralAudio.Growl(), 1f);
    }

    void StartChase()
    {
        if (state == State.Chase || state == State.Dead) return;
        state = State.Chase;
        lostTimer = 0;
        barkTimer = Random.Range(1f, 2f);
        Bark(1f, Random.Range(0.9f, 1.15f));
        // 무리의 다른 들개도 잠깐 으르렁거린 뒤 따라온다
        foreach (var other in All)
            if (other != this && Vector3.Distance(other.transform.position, transform.position) < packRadius)
                other.Provoke(Random.Range(0.5f, 0.9f));
    }

    void Bite(Vector3 playerPos, float danger)
    {
        biteTimer = stats.attackDelay;
        lunge = 1;
        voice.pitch = Random.Range(0.9f, 1.1f);
        voice.PlayOneShot(ProceduralAudio.Snap(), 1f);
        ProceduralAudio.PlayAt(ProceduralAudio.Hit(), playerPos, 0.6f, 1.1f);
        GameManager.Instance.Damage(stats.damage * (1 + 0.2f * danger), "들개에게 물어뜯겼다.");
    }

    void Bark(float volume, float pitch)
    {
        voice.pitch = pitch;
        voice.PlayOneShot(ProceduralAudio.Bark(), volume);
    }

    void FaceTowards(Vector3 pos)
    {
        var dir = pos - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 400 * Time.deltaTime);
    }

    // direction이 0이면 불에 타는 것처럼 계속 받는 피해: 움찔하거나 밀리지 않는다
    public void TakeDamage(float amount, Vector3 direction)
    {
        if (state == State.Dead) return;
        health -= amount;
        bool dot = direction == Vector3.zero;
        if (!dot)
        {
            staggerTimer = 0.35f;
            hitReact = 1;
            if (agent.isOnNavMesh) agent.Move(new Vector3(direction.x, 0, direction.z).normalized * 0.4f);
            if (flash != null) flash.Flash(HitFlash.HitColor);
        }
        if (!dot || Random.value < 0.4f) Bark(dot ? 0.7f : 1f, Random.Range(1.3f, 1.5f)); // 깨갱
        if (health <= 0)
        {
            StartCoroutine(Die());
            return;
        }
        StartChase(); // 경고 중이든 배회 중이든 맞으면 바로 달려든다
    }

    IEnumerator Die()
    {
        state = State.Dead;
        GameManager.Instance.NoteKill(true);
        agent.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        voice.Stop();
        Bark(0.8f, 1.2f);

        var start = transform.rotation;
        var side = Vector3.Cross(Vector3.up, start * Vector3.forward).normalized * 0.35f;
        var end = start * Quaternion.Euler(0, 0, 85);
        for (float t = 0; t < 1; t += Time.deltaTime / 0.45f)
        {
            transform.rotation = Quaternion.Slerp(start, end, t * t);
            // 다리가 힘없이 접히고 머리가 떨어진다
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] != null) legs[i].localRotation = Quaternion.Euler((i < 2 ? -25 : 30) * t, 0, 0);
                    if (knees != null && knees[i] != null) knees[i].localRotation = Quaternion.Euler((i < 2 ? 40 : -35) * t, 0, 0);
                }
            if (neck != null) neck.localRotation = Quaternion.Euler(30 * t, 0, 0);
            if (jaw != null) jaw.localRotation = Quaternion.Euler(10 + 25 * t, 0, 0);
            yield return null;
        }
        transform.rotation = end;
        // 가죽 1개, 고기 1개
        PickupItem.Spawn("leather", 2, transform.position + Vector3.up * 0.5f + side);
        PickupItem.Spawn("raw_meat", 1, transform.position + Vector3.up * 0.5f - side);
        Destroy(gameObject, 30f);
    }

    // 걷기·달리기(다리 4개를 엇갈려 딛고 무릎을 굽힌다), 꼬리 흔들기, 경고할 땐 머리를 낮추고 이를 드러내며, 물 때 머리를 내밀고 턱을 벌린다
    void Animate(float dt)
    {
        float speed = agent.enabled ? agent.velocity.magnitude : 0;
        bool running = speed > 2.5f;
        animPhase += dt * (2 + speed * (running ? 2.6f : 3.5f));
        float amp = Mathf.Clamp01(speed / 2f) * (running ? 42f : 30f);
        lunge = Mathf.MoveTowards(lunge, 0, dt * 4);
        hitReact = Mathf.MoveTowards(hitReact, 0, dt * 5);
        if (legs != null)
            for (int i = 0; i < legs.Length; i++)
            {
                // 걸을 땐 대각선 다리끼리, 달릴 땐 앞다리·뒷다리끼리 함께 (질주)
                float offset = running ? (i < 2 ? 0 : Mathf.PI) + (i % 2) * 0.4f : (i == 0 || i == 3 ? 0 : Mathf.PI);
                float ph = animPhase + offset;
                if (legs[i] != null) legs[i].localRotation = Quaternion.Euler(Mathf.Sin(ph) * amp, 0, 0);
                // 다리를 앞으로 옮길 때 무릎(앞다리) · 발목(뒷다리)을 굽힌다
                float lift = Mathf.Max(0, -Mathf.Cos(ph)) * Mathf.Clamp01(speed / 1.5f);
                if (knees != null && knees[i] != null) knees[i].localRotation = Quaternion.Euler(i < 2 ? lift * 55f : -lift * 40f, 0, 0);
            }
        // 몸이 걸음마다 오르내리고, 달리면 앞뒤로 출렁인다
        if (body != null)
            body.localRotation = Quaternion.Euler((running ? Mathf.Sin(animPhase * 2) * 4f : 0) - hitReact * 8f, 0, Mathf.Sin(animPhase) * 2f * Mathf.Clamp01(speed));
        bool warn = state == State.Warn;
        if (neck != null)
        {
            // 경고: 머리를 낮추고 앞으로 뺀다. 물기: 앞으로 쭉 내민다. 맞으면 움찔 젖힌다
            float pitch = (warn ? 22f : 0) - lunge * 14f + Mathf.Sin(animPhase * 0.5f) * 2f - hitReact * 18f;
            neck.localRotation = Quaternion.Euler(pitch, Mathf.Sin(animPhase * 0.23f) * (state == State.Roam ? 12f : 3f), 0);
            neck.localPosition = new Vector3(0, 0.05f, 0.3f + lunge * 0.08f);
        }
        if (head != null) head.localRotation = Quaternion.Euler(warn ? -12f : 0, 0, warn ? Mathf.Sin(Time.time * 18f) * 1.5f : 0);
        if (jaw != null)
        {
            float open = 10f + (warn ? 14f + Mathf.Sin(Time.time * 22f) * 2f : 0) + lunge * 32f + (running ? 8f : 0);
            jaw.localRotation = Quaternion.Euler(open, 0, 0);
        }
        if (tail != null)
        {
            // 배회할 땐 느슨하게 흔들고, 경고·추격 땐 뻣뻣하게 세운다
            float up = warn || state == State.Chase ? 35f : 0;
            tail.localRotation = Quaternion.Euler(-118 + up, Mathf.Sin(Time.time * (state == State.Roam ? 4f : 9f)) * (warn ? 6f : 18f), 0);
        }
    }
}
