using UnityEngine;
using UnityEngine.InputSystem;

// 핫바: 1~8 / 마우스 휠로 아이템 선택, 우클릭 사용, G 버리기. 선택한 아이템은 오른손에 든다.
public class PlayerHotbar : MonoBehaviour
{
    public static PlayerHotbar Instance { get; private set; }
    public const int Slots = 8;

    public int SelectedIndex { get; private set; }
    public string SelectedId { get; private set; }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var items = GameManager.Instance.State.Carried.Sorted();

        // 아이템 순서가 바뀌어도 고른 아이템을 계속 들고 있게 한다
        int keep = SelectedId != null ? items.FindIndex(kv => kv.Key == SelectedId) : -1;
        if (keep >= 0) SelectedIndex = keep;

        if (!PlayerController.Instance.InputBlocked)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                for (int i = 0; i < Slots; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) SelectedIndex = i;
                if (kb.gKey.wasPressedThisFrame && SelectedId != null) Drop(SelectedId);
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f) SelectedIndex = (SelectedIndex + Slots - 1) % Slots;
                else if (scroll < -0.1f) SelectedIndex = (SelectedIndex + 1) % Slots;
                if (mouse.rightButton.wasPressedThisFrame && SelectedId != null) UseSelected();
            }
        }

        SelectedId = SelectedIndex < items.Count && SelectedIndex < Slots ? items[SelectedIndex].Key : null;
    }

    void UseSelected()
    {
        var gm = GameManager.Instance;
        var def = ItemDatabase.Get(SelectedId);
        if (def.IsConsumable) gm.UseItem(def.Id, gm.State.Carried);
        else if (def.Id == "flashlight") gm.UI.ShowMessage("손전등은 Q로 켜고 끈다.");
        else if (def.Id == "club") gm.UI.ShowMessage("둔기는 좌클릭으로 휘두른다.");
        else if (def.Id == "backpack") gm.UI.ShowMessage("가방을 메고 있으면 더 많이 들 수 있다.");
        else gm.UI.ShowMessage($"{def.Name}: 방공호 제작대에서 쓰는 재료다.");
    }

    public void Drop(string id)
    {
        var s = GameManager.Instance.State;
        if (!s.Carried.Remove(id)) return;
        PickupItem.Spawn(id, 1, transform.position + transform.forward * 0.8f + Vector3.up * 0.3f);
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.4f);
    }
}
