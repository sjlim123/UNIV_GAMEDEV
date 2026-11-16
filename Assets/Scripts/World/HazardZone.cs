using UnityEngine;

// 위험 구역: 불길(Fire)·유리 조각(Glass)·한 번만 뜨는 힌트(Hint).
// 콜라이더 없이 이 물체 로컬 좌표의 상자 안에 플레이어가 있는지만 본다 (트리거를 두면 조준선 상호작용을 가로챈다. CrawlHint와 같은 방식).
// 불꽃·불빛·유리 조각처럼 보이는 것은 씬 빌더가 따로 놓는다. 여기서는 피해·소음·힌트와 불 타는 소리만 맡는다
public class HazardZone : MonoBehaviour
{
    public enum Kind { Fire, Glass, Hint }

    const float PlayerTick = 0.5f;  // 임시 수치: 불길 안에서 플레이어가 데는 간격(초)
    const float PlayerDamage = 6f;  // 임시 수치: 한 번 델 때 깎이는 체력
    const float EnemyTick = 0.5f;   // 임시 수치: 불길 안의 적에게 불이 붙었는지 보는 간격(초)
    const float StepMeters = 1.1f;  // 임시 수치: 유리 위를 서서 이만큼 갈 때마다 밟는 소리가 난다
    const float NoiseLevel = 1.6f;  // 임시 수치: 유리 밟는 소리의 크기 (걷기 1, 달리기 2. 감염자는 8m × 이 값까지 듣는다)

    public Kind kind = Kind.Fire;
    // 원점 = 바닥 가운데. |x| <= halfSize.x, |z| <= halfSize.z, -0.3 <= y <= halfSize.y * 2 이면 안
    public Vector3 halfSize = new Vector3(0.8f, 1.0f, 0.8f);
    public int hintBit;                     // Hint: Tutorial 힌트 비트. Glass: 0이면 Tutorial.HintGlass
    [TextArea] public string hintText = ""; // Hint: 한 번 보여 줄 글. Glass: 비우면 기본 글

    static readonly Collider[] found = new Collider[64];

    float burnTimer, enemyTimer, warnCooldown, walked;
    bool wasInside;

    void Start()
    {
        if (kind != Kind.Fire) return;
        // 타닥 소리는 자식 물체에 둔다 (저음 필터가 붙은 물체에 AudioSource를 더 붙이면 유니티가 오류를 낸다. Burning과 같은 방식)
        var ag = new GameObject("FireAudio");
        ag.transform.SetParent(transform, false);
        ag.transform.localPosition = new Vector3(0, halfSize.y * 0.5f, 0);
        var src = ag.AddComponent<AudioSource>();
        ProceduralAudio.Setup3D(src, 12f, 64, false);
        AudioCategory.Tag(src, SoundCategory.Ambient);
        src.clip = ProceduralAudio.Crackle();
        src.loop = true;
        src.volume = 0.5f;
        src.Play();
    }

    bool Inside(Vector3 world)
    {
        var p = transform.InverseTransformPoint(world);
        return Mathf.Abs(p.x) <= halfSize.x && Mathf.Abs(p.z) <= halfSize.z && p.y >= -0.3f && p.y <= halfSize.y * 2;
    }

    void Update()
    {
        var pc = PlayerController.Instance;
        var gm = pc != null ? GameManager.Instance : null;
        if (gm == null || gm.State == null || gm.IsGameOver || gm.IsTransitioning) return;
        bool inside = Inside(pc.transform.position);
        if (kind == Kind.Fire) TickFire(gm, inside, Time.deltaTime);
        else if (kind == Kind.Glass) TickGlass(pc, inside, Time.deltaTime);
        else TickHint(gm, inside);
        wasInside = inside;
    }

    // 불길: 들어선 순간 바로 한 번 데고 그 뒤로는 PlayerTick마다 덴다 (들락날락해도 이 간격보다 자주 데지는 않는다).
    // 안에 든 적에게는 화상(Burning)이 붙는다. 쫓아오던 놈이 불을 가로지르면 타면서 온다
    void TickFire(GameManager gm, bool inside, float dt)
    {
        warnCooldown = Mathf.Max(0f, warnCooldown - dt);
        burnTimer = Mathf.Max(0f, burnTimer - dt);
        if (inside && burnTimer <= 0f)
        {
            burnTimer = PlayerTick;
            if (warnCooldown <= 0f)
            {
                gm.UI.ShowMessage("뜨겁다! 불길에서 벗어나자.", 2f);
                warnCooldown = 6f;
            }
            gm.Damage(PlayerDamage, "불길을 빠져나오지 못했다.");
        }

        enemyTimer -= dt;
        if (enemyTimer > 0f) return;
        enemyTimer = EnemyTick;
        var b = GameBalance.Data;
        var centre = transform.TransformPoint(new Vector3(0, halfSize.y, 0));
        var half = Vector3.Scale(halfSize, transform.lossyScale);
        var cols = found;
        int n = Physics.OverlapBoxNonAlloc(centre, half, found, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        if (n >= found.Length)
        {
            // 바닥·벽·잔해만으로 칸이 다 찼다: 적을 놓치지 않게 이번에는 전부 받아 온다
            cols = Physics.OverlapBox(centre, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            n = cols.Length;
        }
        for (int i = 0; i < n; i++)
        {
            var go = cols[i].gameObject;
            var target = go.GetComponentInParent<IDamageable>();
            // 이미 타고 있으면 다시 붙이지 않는다: Burning은 붙일 때마다 처음부터 다시 세므로, 계속 붙이면 피해가 들어갈 틈이 없다
            if (target == null || IsDead(target) || Burning.IsBurning(go)) continue;
            Burning.Apply(go, b.burnSeconds, b.burnDamage);
        }
    }

    static bool IsDead(IDamageable t) => (t is Zombie z && z.IsDead) || (t is Animal a && a.IsDead);

    // 유리 조각: 서서 걷거나 뛰면 StepMeters마다 밟는 소리가 나고 그 소리를 주변이 듣는다. 앉아서 가면 조용하다
    void TickGlass(PlayerController pc, bool inside, float dt)
    {
        if (!inside) return;
        if (!wasInside)
        {
            // 들어설 때마다 묻는다 (한 번만 뜨고, 안내를 꺼 두었으면 본 것으로 치지 않는 것은 Tutorial.Hint가 맡는다)
            Tutorial.Hint(hintBit != 0 ? hintBit : Tutorial.HintGlass, !string.IsNullOrEmpty(hintText) ? hintText : "유리 조각이 깔려 있다. 앉아서 지나가면 소리가 나지 않는다.");
            walked = 0; // 들어설 때마다 처음부터 센다 (가장자리를 들락거리며 쌓인 거리로 소리가 나지 않게)
        }
        if (!pc.IsGrounded || pc.IsCrouching || pc.HorizontalSpeed <= 0.5f) return;
        walked += pc.HorizontalSpeed * dt;
        if (walked < StepMeters) return;
        walked -= StepMeters;
        pc.MakeNoise(NoiseLevel);
        ProceduralAudio.PlayAt(ProceduralAudio.GlassBreak(), pc.transform.position, 0.22f, Random.Range(1.3f, 1.6f), 1f, 14f);
    }

    // 힌트: 안에 들어오면 한 번 보여 준다. 이미 본 힌트면 (같은 비트를 쓰는 다른 구역이 먼저 보여 줬어도) 더 볼 일이 없다
    void TickHint(GameManager gm, bool inside)
    {
        if (hintBit == 0 || string.IsNullOrEmpty(hintText) || (gm.State.HintsSeen & hintBit) != 0)
        {
            enabled = false;
            return;
        }
        if (inside) Tutorial.Hint(hintBit, hintText);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = kind == Kind.Fire ? new Color(1f, 0.45f, 0.1f) : kind == Kind.Glass ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.85f, 0.2f);
        Gizmos.DrawWireCube(new Vector3(0, halfSize.y - 0.15f, 0), new Vector3(halfSize.x * 2, halfSize.y * 2 + 0.3f, halfSize.z * 2));
    }
}
