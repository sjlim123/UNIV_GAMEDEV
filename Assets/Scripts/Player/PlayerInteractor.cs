using UnityEngine;

// 화면 가운데를 보고 상호작용 키(기본 F)를 누르면 조준선의 문·장치·아이템과 상호작용한다. 손전등은 손전등 키가 따로 맡는다 (PlayerFlashlight)
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
        if (blocked || current == null || !KeyBindings.Pressed(GameAction.Interact)) return;
        current.Interact(this);
    }
}
