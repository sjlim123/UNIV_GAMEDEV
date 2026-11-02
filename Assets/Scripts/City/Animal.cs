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

    public Transform[] legs;
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
    Transform[] headParts;   // 머리·주둥이·귀 (경고할 때 낮추고, 물 때 내민다)
    Vector3[] headBase;
    float health, wanderTimer, warnTimer, biteTimer, barkTimer, repathTimer, staggerTimer, lostTimer, animPhase, lunge;
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
        var names = new[] { "Head", "Snout", "EarL", "EarR" };
        headParts = new Transform[names.Length];
        headBase = new Vector3[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            headParts[i] = transform.Find(names[i]);
            if (headParts[i] != null) headBase[i] = headParts[i].localPosition;
        }
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

    public void TakeDamage(float amount, Vector3 direction)
    {
        if (state == State.Dead) return;
        health -= amount;
        staggerTimer = 0.35f;
        if (agent.isOnNavMesh) agent.Move(new Vector3(direction.x, 0, direction.z).normalized * 0.4f);
        Bark(1f, Random.Range(1.3f, 1.5f)); // 깨갱
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
        agent.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        voice.Stop();
        Bark(0.8f, 1.2f);

        var start = transform.rotation;
        var side = Vector3.Cross(Vector3.up, start * Vector3.forward).normalized * 0.35f;
        var end = start * Quaternion.Euler(0, 0, 85);
        for (float t = 0; t < 1; t += Time.deltaTime / 0.4f)
        {
            transform.rotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }
        transform.rotation = end;
        // 가죽 1개, 고기 1개
        PickupItem.Spawn("leather", 1, transform.position + Vector3.up * 0.5f + side);
        PickupItem.Spawn("raw_meat", 1, transform.position + Vector3.up * 0.5f - side);
        Destroy(gameObject, 30f);
    }

    void Animate(float dt)
    {
        float speed = agent.enabled ? agent.velocity.magnitude : 0;
        animPhase += dt * (2 + speed * 3.5f);
        float amp = Mathf.Clamp01(speed / 2f) * 35f;
        if (legs != null)
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null)
                    legs[i].localRotation = Quaternion.Euler(Mathf.Sin(animPhase + (i % 2 == 0 ? 0 : Mathf.PI) + (i >= 2 ? Mathf.PI * 0.5f : 0)) * amp, 0, 0);

        // 경고할 땐 머리를 낮추고, 물 때는 앞으로 내민다
        lunge = Mathf.MoveTowards(lunge, 0, dt * 4);
        var offset = new Vector3(0, state == State.Warn ? -0.08f : 0, lunge * 0.15f);
        for (int i = 0; i < headParts.Length; i++)
            if (headParts[i] != null) headParts[i].localPosition = headBase[i] + offset;
    }
}
