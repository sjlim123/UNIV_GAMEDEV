using System.Collections;
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
            else if (weapon != null && weapon.Weapon == WeaponKind.Stab) PlayerHands.Instance.PlaySlash(); // 단검: 대각선 베기를 번갈아
            else PlayerHands.Instance.PlayPunch(); // 주먹·라이터 들이대기
        }
        // 휘두르는 소리는 무기마다 다르다. 방망이는 뒤로 젖혔다가 내려칠 때 바람 소리가 나도록 조금 늦게 낸다
        var swing = EffectAudio.MeleeSwing(weapon);
        if (weapon != null && weapon.Weapon == WeaponKind.Swing) StartCoroutine(OneShotLater(swing, 0.4f, 0.1f));
        else source.PlayOneShot(swing, 0.4f);
        PlayerController.Instance.MakeNoise(1.2f);
        GameManager.Instance.UI.KickCrosshair();

        // 맨손 사거리는 보통 좀비의 공격 사거리(1.6m)보다 2% 길다 (먼저 닿을 수 있게)
        float range = weapon == null ? 1.6f * 1.02f : weapon.Weapon == WeaponKind.Stab ? 1.9f : 2.3f;
        bool struck = PlayerRaycast.SphereCast(cam.transform.position, 0.25f, cam.transform.forward, range, transform, QueryTriggerInteraction.Ignore, out var hit);
        var target = struck ? hit.collider.GetComponentInParent<IDamageable>() : null;
        if (target != null)
        {
            bool armored = target is Zombie armoredZombie && armoredZombie.ArmorLeft; // 맞기 전에 본다 (이번 타격에 조각이 떨어질 수 있다)
            target.TakeDamage(weapon != null ? weapon.Damage : b.playerDamage, cam.transform.forward);
            bool killed = IsDead(target);
            // 무기마다 다른 타격음. 쓰러뜨리면 더 무거운 마무리 소리, 가죽 갑옷을 치면 가죽 소리에 살 소리를 작게 겹친다
            var armorClip = armored && !killed ? EffectAudio.ArmorHit(weapon) : null;
            if (armorClip != null)
            {
                source.PlayOneShot(armorClip, 0.9f);
                source.PlayOneShot(EffectAudio.MeleeHit(weapon, false), 0.4f);
            }
            else source.PlayOneShot(EffectAudio.MeleeHit(weapon, killed), 1f);
            PlayerController.Instance.Shake(killed ? 0.55f : 0.18f); // 처치 때는 크게 흔들린다
            HitEffects.PlayerHit((Component)target, hit.point, cam.transform.forward, killed);
            if (lighter && !killed)
            {
                var go = ((Component)target).gameObject;
                bool wasBurning = Burning.IsBurning(go);
                Burning.Apply(go, b.burnSeconds, b.burnDamage);
                // 처음 불을 붙일 때만, 게임을 켠 뒤 두 번까지만 알려 준다 (타고 있는 적을 다시 때릴 때마다 뜨지 않게. 불꽃이 보이면 충분하다)
                if (!wasBurning && burnNotices < 2)
                {
                    burnNotices++;
                    GameManager.Instance.UI.ShowMessage($"불이 붙었다! ({b.burnSeconds:0}초 동안 화상)", 1.6f);
                }
            }
        }
        // 벽·물건·바닥을 쳤다. 벽에 몸을 붙이고 휘두르면 구가 처음부터 벽에 닿아 있어 구 판정이 벽을 건너뛰므로, 그때는 조준선 한 줄로 다시 찾는다
        else if (struck || PlayerRaycast.Raycast(cam.transform.position, cam.transform.forward, range, transform, QueryTriggerInteraction.Ignore, out hit))
        {
            if (hit.collider.GetComponentInParent<IDamageable>() == null) StartCoroutine(SurfaceHit(hit, weapon, range));
        }
    }

    // 무기가 눈에 보이게 닿는 순간 (휘두르기 동작에서 잰 값): 주먹·라이터는 내지른 끝, 단검은 베는 중간, 방망이는 내려치는 호의 가운데.
    // 적은 누른 순간 맞지만, 벽은 이때 맞아야 방망이가 닿기 전에 먼지가 튀지 않는다
    static float ContactDelay(ItemDef weapon) => weapon == null ? 0.09f : weapon.Weapon == WeaponKind.Stab ? 0.12f : 0.3f;

    // 벽·물건·바닥 타격: 재질에 맞는 먼지·조각·불꽃·자국(Vfx.Impact)과 소리(EffectAudio.Impact), 손에 오는 작은 반동
    IEnumerator SurfaceHit(RaycastHit hit, ItemDef weapon, float range)
    {
        var col = hit.collider;
        var origin = cam.transform.position;
        var forward = cam.transform.forward;
        // 구 판정의 법선은 모서리에서 기울어 있다. 조준선이 그 물체에 닿으면 그 점·면을, 아니면 구가 닿은 점으로 한 줄 쏴서 면을 다시 얻는다
        Vector3 point = hit.point, normal = hit.normal;
        int triangle = hit.triangleIndex;
        bool onCrosshair = col.Raycast(new Ray(origin, forward), out var face, range + 0.3f);
        if (onCrosshair || col.Raycast(new Ray(origin, (hit.point - origin).normalized), out face, range + 0.5f))
        {
            point = face.point;
            normal = face.normal;
            triangle = face.triangleIndex;
        }
        var kind = ImpactSurface.Classify(col, point, normal, triangle);
        bool fist = weapon == null;
        string id = weapon != null ? weapon.Id : null;
        // 세기: 맨손·라이터 0.35, 단검 0.75, 방망이 1, 못 방망이 1.1, 쇠 방망이 1.15
        float strength = fist ? 0.35f : weapon.Weapon == WeaponKind.Stab ? 0.75f : id == "alloy_bat" ? 1.15f : id == "spiked_bat" ? 1.1f : 1f;
        bool hardTool = id == "dagger" || id == "alloy_bat" || id == "spiked_bat";
        // 휘두른 방향 (긁힌 자국·홈이 이쪽으로 길다): 방망이는 오른쪽 위에서 왼쪽 아래로, 단검은 비스듬히, 주먹은 상관없다
        var along = weapon != null && weapon.Weapon == WeaponKind.Swing ? (-cam.transform.right * 0.55f - cam.transform.up * 0.85f) : (-cam.transform.right - cam.transform.up * 0.45f);
        // 렌더러 없는 투명 충돌 상자(울타리 밖 벽 등)면 먼지·조각·자국 없이 소리만 낸다 (허공에 먼지가 생기지 않게)
        bool visible = ImpactSurface.Visible(col, point);
        // 자국은 움직이지 않는 면에만, 조준선이 직접 닿은 자리에만 남긴다 (문·상자·던진 물건은 자국이 허공에 남고, 구가 스친 모서리는 자국이 삐져나간다)
        bool mark = visible && onCrosshair && hit.rigidbody == null && col.GetComponentInParent<Interactable>() == null;

        yield return new WaitForSeconds(ContactDelay(weapon));
        if (col == null || !col.gameObject.activeInHierarchy) yield break;

        if (visible) Vfx.Impact(kind, point, normal, forward, along, strength, hardTool, mark, col);
        var clip = EffectAudio.Impact(kind, hardTool, fist);
        if (clip == null) clip = kind == Vfx.ImpactKind.Water ? ProceduralAudio.Splash() : ProceduralAudio.FleshHit();
        float volume = (fist ? 0.6f : 0.85f) * (kind == Vfx.ImpactKind.Flesh ? 0.7f : 1f);
        ProceduralAudio.PlayAt(clip, point, volume, Random.Range(0.94f, 1.06f), 0.6f, 25f, SoundCategory.Effects);
        if (PlayerController.Instance != null) PlayerController.Instance.Shake(fist ? 0.04f : weapon.Weapon == WeaponKind.Stab ? 0.06f : 0.1f);
        // 굴러다니는 물건(던진 아이템·떨어진 갑옷 조각)은 맞은 쪽으로 밀린다
        if (hit.rigidbody != null && !hit.rigidbody.isKinematic) hit.rigidbody.AddForceAtPosition(forward * 2.5f * strength, point, ForceMode.Impulse);
    }

    static int burnNotices;

    IEnumerator OneShotLater(AudioClip clip, float volume, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (clip != null) source.PlayOneShot(clip, volume);
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
