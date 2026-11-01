using UnityEngine;
using UnityEngine.InputSystem;

// 좌클릭 근접 공격. 핫바에서 둔기를 고르면 둔기로, 아니면 주먹으로 친다.
public class PlayerCombat : MonoBehaviour
{
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
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && cooldown <= 0 && !PlayerController.Instance.InputBlocked)
            Attack(PlayerHotbar.Instance != null && PlayerHotbar.Instance.SelectedId == "club");
    }

    void Attack(bool club)
    {
        var b = GameBalance.Data;
        cooldown = club ? 0.75f : 0.5f;
        if (PlayerHands.Instance != null)
        {
            if (club) PlayerHands.Instance.PlaySwing();
            else PlayerHands.Instance.PlayPunch();
        }
        source.PlayOneShot(ProceduralAudio.Swoosh(), 0.5f);
        PlayerController.Instance.MakeNoise(1.2f);

        float range = club ? 2.3f : 1.6f;
        if (PlayerRaycast.SphereCast(cam.transform.position, 0.25f, cam.transform.forward, range, transform, QueryTriggerInteraction.Ignore, out var hit))
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(club ? b.clubDamage : b.fistDamage, cam.transform.forward);
                source.PlayOneShot(ProceduralAudio.Hit(), 0.9f);
                PlayerController.Instance.Shake(0.15f);
            }
        }
    }
}
