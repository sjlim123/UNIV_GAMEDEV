using UnityEngine;

// 공격 키(기본 좌클릭). 핫바에서 무기를 고르면 그 무기로(공격력·공격 간격은 ItemDatabase), 아니면 주먹으로 친다.
// 부싯돌과 라이터를 들고 때리면 불이 붙어 5초 동안 모두 10만큼 탄다 (GameBalance.burnSeconds·burnDamage).
// 새총은 돌을 쏜다: 메겨 둔 발이 없으면 소지품의 돌 1개를 메겨 ShotsPerStone발을 쏠 수 있다.
// 맞히면 적이 빨갛게 번쩍이고 피가 튀며, 조준점에 X가 뜨고 화면이 아주 잠깐 멈칫한다 (HitEffects)
public class PlayerCombat : MonoBehaviour
{
    // 새총 돌이 날아가는 빠르기(m/s)
    const float SlingSpeed = 26f;

    public Camera cam;

    float cooldown;
    AudioSource source;

    void Start()
    {
        source = gameObject.AddComponent<AudioSource>();
        AudioCategory.Tag(source, SoundCategory.Effects);
        source.spatialBlend = 0;
    }

    void Update()
    {
        cooldown -= Time.deltaTime;
        if (!KeyBindings.Pressed(GameAction.Attack) || cooldown > 0 || PlayerController.Instance.InputBlocked) return;

        string id = PlayerHotbar.Instance != null ? PlayerHotbar.Instance.SelectedId : null;
        var weapon = id != null ? ItemDatabase.Get(id) : null;
        if (weapon != null && weapon.Weapon == WeaponKind.Sling) Shoot(weapon);
        else Melee(weapon != null && weapon.IsWeapon ? weapon : null, id == ItemDatabase.LighterId);
    }

    // weapon이 null이면 주먹 (lighter면 라이터 불을 들이댄다)
    void Melee(ItemDef weapon, bool lighter)
    {
        var b = GameBalance.Data;
        cooldown = weapon != null ? weapon.AttackDelay : b.playerAttackDelay;
        if (PlayerHands.Instance != null)
        {
            if (weapon != null && weapon.Weapon == WeaponKind.Swing) PlayerHands.Instance.PlaySwing();
            else PlayerHands.Instance.PlayPunch(); // 주먹·단검 찌르기
        }
        source.PlayOneShot(ProceduralAudio.Swoosh(), 0.5f);
        PlayerController.Instance.MakeNoise(1.2f);
        GameManager.Instance.UI.KickCrosshair();

        // 맨손 사거리는 보통 좀비의 공격 사거리(1.6m)보다 2% 길다 (먼저 닿을 수 있게)
        float range = weapon == null ? 1.6f * 1.02f : weapon.Weapon == WeaponKind.Stab ? 1.9f : 2.3f;
        if (PlayerRaycast.SphereCast(cam.transform.position, 0.25f, cam.transform.forward, range, transform, QueryTriggerInteraction.Ignore, out var hit))
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(weapon != null ? weapon.Damage : b.playerDamage, cam.transform.forward);
                bool killed = IsDead(target);
                source.PlayOneShot(killed ? ProceduralAudio.KillHit() : ProceduralAudio.FleshHit(), 1f);
                PlayerController.Instance.Shake(killed ? 0.55f : 0.18f); // 처치 때는 크게 흔들린다
                HitEffects.PlayerHit((Component)target, hit.point, cam.transform.forward, killed);
                if (lighter && !killed)
                {
                    Burning.Apply(((Component)target).gameObject, b.burnSeconds, b.burnDamage);
                    GameManager.Instance.UI.ShowMessage($"불이 붙었다! ({b.burnSeconds:0}초 동안 화상)", 1.6f);
                }
            }
        }
    }

    static bool IsDead(IDamageable t) => (t is Zombie z && z.IsDead) || (t is Animal a && a.IsDead);

    void Shoot(ItemDef sling)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        if (s.SlingShots <= 0)
        {
            if (!s.Carried.Remove(ItemDatabase.SlingAmmoId))
            {
                cooldown = 0.4f;
                gm.UI.ShowMessage("새총에 쓸 돌이 없다. 돌을 주워 오자.");
                source.PlayOneShot(ProceduralAudio.Click(), 0.4f);
                return;
            }
            s.SlingShots = ItemDatabase.ShotsPerStone;
            gm.UI.ShowMessage($"돌을 새총에 메겼다. ({ItemDatabase.ShotsPerStone}발)");
        }
        s.SlingShots--;
        cooldown = sling.AttackDelay;
        if (PlayerHands.Instance != null) PlayerHands.Instance.PlayShoot();
        ProceduralAudio.Play2D(ProceduralAudio.Swoosh(), 0.35f, 1.5f); // 고무줄이 튕기는 짧고 높은 소리
        PlayerController.Instance.MakeNoise(0.6f);

        // 오른손 앞에서 조준한 곳(화면 가운데)을 향해 쏜다
        var c = cam.transform;
        var from = c.position + c.forward * 0.45f + c.right * 0.12f - c.up * 0.08f;
        Vector3 aim = c.position + c.forward * 40f;
        if (PlayerRaycast.SphereCast(c.position, 0.05f, c.forward, 40f, transform, QueryTriggerInteraction.Ignore, out var hit)) aim = hit.point;
        EnemyProjectile.LaunchFromPlayer(from, (aim - from).normalized * SlingSpeed, sling.Damage, transform);
    }
}
