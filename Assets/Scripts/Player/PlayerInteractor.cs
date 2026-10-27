using UnityEngine;
using UnityEngine.InputSystem;

// 화면 가운데를 보고 F를 누르면 상호작용한다
public class PlayerInteractor : MonoBehaviour
{
    public Camera cam;
    public float range = 2.6f;

    void Update()
    {
        var gm = GameManager.Instance;
        Interactable current = null;
        if (!PlayerController.Instance.InputBlocked &&
            Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, range, ~0, QueryTriggerInteraction.Collide))
        {
            current = hit.collider.GetComponentInParent<Interactable>();
            if (current != null && !current.CanInteract) current = null;
        }

        gm.UI.SetPrompt(current != null ? "[F] " + current.Prompt : "");
        var kb = Keyboard.current;
        if (current != null && kb != null && kb.fKey.wasPressedThisFrame) current.Interact(this);
    }
}
