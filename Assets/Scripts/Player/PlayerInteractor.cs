using UnityEngine;

// 화면 가운데를 보고 상호작용 키(기본 F)를 누르면 상호작용한다
public class PlayerInteractor : MonoBehaviour
{
    public Camera cam;
    public float range = 2.6f;

    void Update()
    {
        var gm = GameManager.Instance;
        Interactable current = null;
        if (!PlayerController.Instance.InputBlocked &&
            PlayerRaycast.Raycast(cam.transform.position, cam.transform.forward, range, transform, QueryTriggerInteraction.Collide, out var hit))
        {
            current = hit.collider.GetComponentInParent<Interactable>();
            if (current != null && !current.CanInteract) current = null;
        }

        gm.UI.SetPrompt(current != null ? $"[{KeyBindings.Label(GameAction.Interact)}] " + current.Prompt : "");
        InteractOutline.Show(current);
        if (current != null && KeyBindings.Pressed(GameAction.Interact)) current.Interact(this);
    }
}
