using UnityEngine;
using UnityEngine.InputSystem;

// 좌클릭 공격. 핫바에서 무기를 고르면 그 무기로(공격력·공격 간격은 ItemDatabase), 아니면 주먹으로 친다.
// 새총은 돌을 쏜다: 메겨 둔 발이 없으면 소지품의 돌 1개를 메겨 ShotsPerStone발을 쏠 수 있다.
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
        source.spatialBlend = 0;
    }

    void Update()
    {
        cooldown -= Time.deltaTime;
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || cooldown > 0 || PlayerController.Instance.InputBlocked) return;

        string id = PlayerHotbar.Instance != null ? PlayerHotbar.Instance.SelectedId : null;
        var weapon = id != null ? ItemDatabase.Get(id) : null;
        if (weapon != null && weapon.Weapon == WeaponKind.Sling) Shoot(weapon);
        else Melee(weapon != null && weapon.IsWeapon ? weapon : null);
    }

    // weapon이 null이면 주먹
    void Melee(ItemDef weapon)
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

        float range = weapon == null ? 1.6f : weapon.Weapon == WeaponKind.Stab ? 1.9f : 2.3f;
        if (PlayerRaycast.SphereCast(cam.transform.position, 0.25f, cam.transform.forward, range, transform, QueryTriggerInteraction.Ignore, out var hit))
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(weapon != null ? weapon.Damage : b.playerDamage, cam.transform.forward);
                source.PlayOneShot(ProceduralAudio.Hit(), 0.9f);
                PlayerController.Instance.Shake(0.15f);
            }
        }
    }

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
