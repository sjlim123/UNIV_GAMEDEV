using UnityEngine;

// 화면 가운데를 보고 상호작용 키(기본 F)를 누르면 상호작용한다.
// 조준선에 아무것도 없으면(문·장치·아이템을 보고 있지 않으면) 같은 키로 손전등을 왼손에 꺼내거나 넣는다 — 문 앞에서는 늘 문 열기가 먼저다
public class PlayerInteractor : MonoBehaviour
{
    public Camera cam;
    public float range = 2.6f;

    void Update()
    {
        var gm = GameManager.Instance;
        bool blocked = PlayerController.Instance.InputBlocked;
        Interactable current = null;
        if (!blocked &&
            PlayerRaycast.Raycast(cam.transform.position, cam.transform.forward, range, transform, QueryTriggerInteraction.Collide, out var hit))
        {
            current = hit.collider.GetComponentInParent<Interactable>();
            if (current != null && !current.CanInteract) current = null;
        }

        gm.UI.SetPrompt(current != null ? $"[{KeyBindings.Label(GameAction.Interact)}] " + current.Prompt : "");
        InteractOutline.Show(current);
        if (blocked || !KeyBindings.Pressed(GameAction.Interact)) return;
        if (current != null) current.Interact(this);
        else if (PlayerFlashlight.Instance != null) PlayerFlashlight.Instance.ToggleOut();
    }
}
