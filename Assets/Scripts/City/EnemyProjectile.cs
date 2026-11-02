using UnityEngine;

// 새총으로 쏘는 돌멩이 (원거리 좀비와 플레이어가 함께 쓴다). 낮고 빠른 포물선으로 날아가
// 좀비가 쏜 돌은 플레이어에, 플레이어가 쏜 돌은 좀비·들개에 맞으면 피해를 주고,
// 벽·바닥에 맞으면 흙먼지를 일으키며 딱 소리와 함께 튕겨 떨어졌다가 잠시 뒤 사라진다.
public class EnemyProjectile : MonoBehaviour
{
    // 중력 배율 (작을수록 곧게 날아간다)
    public const float GravityScale = 0.45f;
    // 이 시간(초)이 지나면 저절로 사라진다
    public const float Lifetime = 4f;
    public const float Radius = 0.06f;
    // 떨어진 돌이 바닥에 남아 있는 시간(초)
    const float RestTime = 1.5f;
    const int DustCount = 6;
    const float DustTime = 0.5f;

    static readonly RaycastHit[] hits = new RaycastHit[16];
    static Mesh sphereMesh;

    Vector3 velocity, spin;
    float damage, age, dustAge;
    string deathReason;
    Transform owner, stone;
    bool spent, resting; // spent: 한 번 부딪힌 돌 (더는 피해를 주지 않는다)
    bool fromPlayer;     // 플레이어가 쏜 돌 (좀비·들개에게 피해를 준다)
    int bounces;
    Transform[] dust;
    Vector3[] dustVel;
    float[] dustSize;

    // from에서 target을 향해 포물선으로 쏜다. speed = 수평 속도(m/s), mat = 돌 재질 (null이면 회색 돌)
    public static EnemyProjectile Launch(Vector3 from, Vector3 target, float speed, float damage, string deathReason, Transform owner, Material mat)
    {
        var p = Create(from, damage, owner, mat);
        p.deathReason = deathReason;
        p.velocity = BallisticVelocity(from, target, speed);
        return p;
    }

    // 플레이어의 새총: from에서 velocity로 쏜다 (조준한 방향으로 곧게 날아가다 조금씩 떨어진다)
    public static EnemyProjectile LaunchFromPlayer(Vector3 from, Vector3 velocity, float damage, Transform player)
    {
        var p = Create(from, damage, player, null);
        p.fromPlayer = true;
        p.velocity = velocity;
        return p;
    }

    static EnemyProjectile Create(Vector3 from, float damage, Transform owner, Material mat)
    {
        var go = new GameObject("SlingStone");
        go.transform.position = from;
        var col = go.AddComponent<SphereCollider>();
        col.radius = Radius;
        col.isTrigger = true;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        if (mat == null) mat = RuntimeVisuals.Mat(new Color(0.42f, 0.41f, 0.38f), 0.15f);
        // 울퉁불퉁한 돌 (찌그러진 공)
        var stone = Visual("Stone", go.transform, mat, new Vector3(Random.Range(0.085f, 0.1f), Random.Range(0.065f, 0.08f), Random.Range(0.075f, 0.09f)));
        stone.localRotation = Random.rotation;

        var p = go.AddComponent<EnemyProjectile>();
        p.damage = damage;
        p.owner = owner;
        p.stone = stone;
        p.spin = Random.onUnitSphere * Random.Range(500f, 900f); // 날아가며 핑글핑글 돈다
        return p;
    }

    static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float speed)
    {
        float g = -Physics.gravity.y * GravityScale;
        Vector3 d = to - from;
        Vector3 flat = new Vector3(d.x, 0, d.z);
        float t = Mathf.Max(0.15f, flat.magnitude / Mathf.Max(1f, speed));
        Vector3 v = flat / t;
        v.y = d.y / t + 0.5f * g * t;
        return v;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        age += dt;
        UpdateDust(dt);
        if (resting) return;
        if (age > Lifetime) { Destroy(gameObject); return; }

        // 날아가는 동안은 중력을 약하게 (낮고 곧은 궤적), 한 번 부딪힌 뒤에는 그냥 떨어진다
        velocity += Physics.gravity * (spent ? 1f : GravityScale) * dt;
        if (stone != null) stone.Rotate(spin * dt, Space.Self);
        Vector3 step = velocity * dt;
        float len = step.magnitude;
        if (len < 1e-5f) return;

        // 빨리 날아가도 얇은 벽을 뚫지 않도록 이번 프레임에 지나갈 길을 미리 훑는다
        Vector3 dir = step / len;
        int n = Physics.SphereCastNonAlloc(transform.position, Radius, dir, hits, len, ~0, QueryTriggerInteraction.Ignore);
        int best = -1;
        for (int i = 0; i < n; i++)
        {
            if (Ignore(hits[i].collider)) continue;
            if (best < 0 || hits[i].distance < hits[best].distance) best = i;
        }
        if (best >= 0)
        {
            var h = hits[best];
            bool overlapped = h.distance <= 0f; // 처음부터 겹쳐 있던 물체는 point가 비어 있다
            Vector3 normal = overlapped ? -dir : h.normal;
            Vector3 point = overlapped ? transform.position : h.point;
            // 부딪힌 자리에 붙인 뒤 살짝 띄워 다음 프레임에 다시 겹치지 않게 한다
            transform.position += dir * h.distance + normal * 0.01f;
            if (spent) Bounce(normal);
            else Impact(h.collider, point, normal);
            return;
        }
        transform.position += step;
    }

    // 플레이어가 날아오는 돌에 직접 걸어 들어온 경우
    void OnTriggerEnter(Collider other)
    {
        if (spent || Ignore(other)) return;
        Impact(other, transform.position, velocity.sqrMagnitude > 1e-6f ? -velocity.normalized : Vector3.up);
    }

    // 한 번 부딪힌 돌은 플레이어·좀비 몸을 그냥 지나 떨어진다 (몸에 끼어 앞뒤로 튕기다 공중에 멈추지 않게)
    bool Ignore(Collider c) =>
        c == null || c.isTrigger || c.transform == transform || (owner != null && c.transform.IsChildOf(owner))
        || (spent && (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<IDamageable>() != null));

    void Impact(Collider c, Vector3 point, Vector3 normal)
    {
        spent = true;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var target = fromPlayer ? c.GetComponentInParent<IDamageable>() : null;
        if (target != null)
        {
            target.TakeDamage(damage, velocity.sqrMagnitude > 1e-6f ? velocity.normalized : -normal);
            ProceduralAudio.Play2D(ProceduralAudio.Hit(), 0.6f, 1.3f);
            ProceduralAudio.PlayAt3D(ProceduralAudio.StoneHit(), point, 0.6f, 0.8f);
            velocity *= -0.12f;
            return;
        }
        if (!fromPlayer && c.GetComponentInParent<PlayerController>() != null)
        {
            GameManager.Instance.Damage(damage, deathReason);
            ProceduralAudio.Play2D(ProceduralAudio.Hit(), 0.85f, 1.25f);
            ProceduralAudio.Play2D(ProceduralAudio.StoneHit(), 0.35f, 0.7f);
            // 몸에 맞고 툭 떨어진다
            velocity *= -0.12f;
            return;
        }
        ProceduralAudio.PlayAt3D(ProceduralAudio.StoneHit(), point, 1f, Random.Range(0.9f, 1.15f));
        if (c.GetComponentInParent<IDamageable>() == null) Dust(point, normal);
        Bounce(normal);
    }

    // 튕긴다 (탄성 0.3, 마찰 0.5). 바닥에서 느려지면 멈췄다가 잠시 뒤 사라진다
    void Bounce(Vector3 normal)
    {
        bounces++;
        float into = Vector3.Dot(velocity, normal);
        if (into < 0)
        {
            // 두 번째부터 튕길 때는 작게 달각거린다
            if (bounces > 1 && -into > 1.5f)
                ProceduralAudio.PlayAt3D(ProceduralAudio.StoneHit(), transform.position, Mathf.Clamp01(-into / 8f) * 0.6f, Random.Range(1.2f, 1.4f));
            velocity = (velocity - into * normal) * 0.5f - into * normal * 0.3f;
        }
        if (normal.y > 0.6f && velocity.magnitude < 1.2f)
        {
            resting = true;
            if (stone != null) stone.position = transform.position - normal * (Radius - 0.035f); // 바닥에 닿게 내려놓는다
            Destroy(gameObject, RestTime);
        }
        else if (bounces > 5) Destroy(gameObject);
    }

    // 벽·바닥에서 피어오르는 작은 흙먼지
    void Dust(Vector3 point, Vector3 normal)
    {
        var mat = RuntimeVisuals.Mat(new Color(0.46f, 0.43f, 0.38f), 0f);
        dust = new Transform[DustCount];
        dustVel = new Vector3[DustCount];
        dustSize = new float[DustCount];
        for (int i = 0; i < DustCount; i++)
        {
            dustSize[i] = Random.Range(0.04f, 0.08f);
            dust[i] = Visual("Dust", null, mat, Vector3.one * dustSize[i]);
            dust[i].position = point + normal * 0.03f;
            dustVel[i] = (normal * Random.Range(0.5f, 1.2f) + Random.insideUnitSphere * 0.8f) * 1.3f;
            Destroy(dust[i].gameObject, DustTime + 0.2f); // 돌이 먼저 사라져도 남지 않게
        }
        dustAge = 0;
    }

    void UpdateDust(float dt)
    {
        if (dust == null) return;
        dustAge += dt;
        float u = Mathf.Clamp01(dustAge / DustTime);
        for (int i = 0; i < dust.Length; i++)
        {
            if (dust[i] == null) continue;
            dustVel[i] *= 1 - Mathf.Min(1f, 5f * dt); // 공기 저항으로 금방 멈춘다
            dustVel[i].y += 0.4f * dt;                // 살짝 떠오른다
            dust[i].position += dustVel[i] * dt;
            dust[i].localScale = Vector3.one * dustSize[i] * (1 + 1.6f * u) * (1 - u * u); // 퍼지며 옅어진다
        }
        if (u >= 1) dust = null;
    }

    // 콜라이더 없는 공 모양 (돌·먼지). CreatePrimitive는 콜라이더가 프레임 끝까지 남아서 메시만 빌려 쓴다
    static Transform Visual(string name, Transform parent, Material mat, Vector3 scale)
    {
        if (sphereMesh == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tmp.SetActive(false);
            sphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }
}
