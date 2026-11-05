using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 핫바: 1~8 / 마우스 휠로 칸 선택, 사용 키(기본 우클릭) 사용, 버리기 키(기본 G). 선택한 칸의 아이템은 오른손에 든다.
// 칸마다 무엇을 놓을지는 GameState.Hotbar (소지품 창에서 끌어다 놓아 바꾼다)
// 가방·방어구는 우클릭하면 장비 칸에 착용한다 (장비 칸으로 가면 소지품·핫바에서 빠진다)
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
        var s = GameManager.Instance.State;
        s.SyncHotbar();

        if (!PlayerController.Instance.InputBlocked)
        {
            bool changed = false;
            for (int i = 0; i < Slots; i++)
                if (KeyBindings.Pressed(GameAction.Slot1 + i)) SelectedIndex = i;
            if (KeyBindings.Pressed(GameAction.DropItem) && SelectedId != null) { Drop(SelectedId); changed = true; }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f) SelectedIndex = (SelectedIndex + Slots - 1) % Slots;
                else if (scroll < -0.1f) SelectedIndex = (SelectedIndex + 1) % Slots;
            }
            if (KeyBindings.Pressed(GameAction.UseItem) && SelectedId != null) { UseSelected(); changed = true; }
            // 사용·장착·버리기로 소지품이 바뀌었으면 핫바를 다시 맞춘다 (장착한 가방·방어구는 소지품에서 빠진다)
            if (changed) s.SyncHotbar();
        }

        SelectedIndex = Mathf.Clamp(SelectedIndex, 0, Slots - 1);
        SelectedId = s.Hotbar[SelectedIndex];
    }

    void UseSelected()
    {
        var gm = GameManager.Instance;
        var def = ItemDatabase.Get(SelectedId);
        if (def.IsConsumable) gm.UseItem(def.Id, gm.State.Carried);
        else if (def.Id == "flashlight") { if (PlayerFlashlight.Instance != null) PlayerFlashlight.Instance.ToggleOut(); } // 우클릭으로도 꺼내고 넣는다
        else if (def.Id == ItemDatabase.LighterId) gm.UI.ShowMessage($"라이터를 들고 {KeyBindings.Label(GameAction.Attack)}하면 적에게 불을 붙인다. 방공호에서는 화로에 불을 붙인다.");
        else if (def.IsWeapon) gm.UI.ShowMessage($"{def.Name}{Eun(def.Name)} 좌클릭으로 " + (def.Weapon == WeaponKind.Sling ? $"돌을 쏜다. 돌 1개로 {ItemDatabase.ShotsPerStone}발." : "공격한다."));
        else if (def.Slot != EquipSlot.None) Equip(def.Id); // 가방·방어구는 우클릭하면 장비 칸에 착용한다
        else if (def.Id == "fuel") gm.UI.ShowMessage("연료통은 방공호 발전기에 넣는다 (F). 트럭 재료로도 쓴다.");
        else if (def.Id == "battery") gm.UI.ShowMessage("배터리는 손전등이 다 닳으면 저절로 갈아 끼운다.");
        else gm.UI.ShowMessage($"{def.Name}: {MaterialUse(def.Id)}다.");
    }

    // 소지품에서 count개를 발 앞에 버린다 (한 덩어리로 떨어진다). 가방·방어구도 소지품에 있으면 똑같이 버린다
    public void Drop(string id, int count = 1)
    {
        var s = GameManager.Instance.State;
        count = Mathf.Min(count, s.Carried.Count(id));
        if (count <= 0 || !s.Carried.Remove(id, count)) return;
        PickupItem.Spawn(id, count, transform.position + transform.forward * 0.8f + Vector3.up * 0.3f);
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.4f);
    }

    // ---------- 장비 칸 (핫바 우클릭과 가방 창이 함께 쓴다) ----------

    // 소지품의 가방·방어구를 장비 칸에 착용한다. 칸에 있던 것은 소지품으로 돌아온다
    public static bool Equip(string id)
    {
        var gm = GameManager.Instance;
        var def = ItemDatabase.Get(id);
        if (!gm.State.Equip(id))
        {
            if (def.Slot == EquipSlot.Backpack) gm.UI.ShowMessage("이 가방으로 바꾸면 너무 무겁다.");
            return false;
        }
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.5f);
        gm.UI.ShowMessage(def.Slot == EquipSlot.Backpack
            ? $"{UIManager.ColoredName(id)}{Eul(def.Name)} 멨다. 들 수 있는 무게 +{def.CarryBonus:0.#} kg"
            : $"{UIManager.ColoredName(id)}{Eul(def.Name)} 입었다. 최대 체력 +{def.HealthBonus}");
        return true;
    }

    // 장비 칸에서 빼서 소지품에 넣는다. 뺄 수 없으면 이유를 알린다
    public static bool Unequip(EquipSlot slot)
    {
        var gm = GameManager.Instance;
        string id = gm.State.Equipped(slot);
        if (id == null) return false;
        if (!gm.State.Unequip(slot))
        {
            gm.UI.ShowMessage(UnequipBlockReason(slot));
            return false;
        }
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.5f);
        gm.UI.ShowMessage($"{UIManager.ColoredName(id)}{Eul(ItemDatabase.Name(id))} 벗어 소지품에 넣었다.");
        return true;
    }

    // 장비 칸에서 뺄 수 없는 이유 (뺄 수 있으면 null)
    public static string UnequipBlockReason(EquipSlot slot)
    {
        var s = GameManager.Instance.State;
        if (s.Equipped(slot) == null) return "장착한 것이 없다.";
        if (s.CanUnequip(slot)) return null;
        return "가방을 빼면 너무 무겁다";
    }

    // 핫바 아래·가방 창에 보여 줄 짧은 사용법
    public static string ActionHint(string id)
    {
        var def = ItemDatabase.Get(id);
        if (def.IsConsumable) return "우클릭: 사용";
        if (def.Weapon == WeaponKind.Sling) return $"좌클릭: 쏘기 · 공격력 {def.Damage:0} · {SlingAmmoText()}";
        if (def.Weapon == WeaponKind.Stab) return $"좌클릭: 찌르기 · 공격력 {def.Damage:0}";
        if (def.IsWeapon) return $"좌클릭: 휘두르기 · 공격력 {def.Damage:0}";
        if (def.Id == "flashlight") return $"{KeyBindings.Label(GameAction.Interact)}·우클릭: 꺼내기/넣기 · {KeyBindings.Label(GameAction.Flashlight)}: 켜기/끄기";
        if (def.Slot == EquipSlot.Backpack) return "우클릭: 장비 칸에 메기";
        if (def.Slot == EquipSlot.Armor) return "우클릭: 장비 칸에 입기";
        if (def.Id == "fuel") return "방공호 발전기 연료 · 트럭 재료";
        if (def.Id == ItemDatabase.StonePouchId) return "가지고 있으면 돌 무게 0";
        if (def.Id == ItemDatabase.LighterId) return $"{KeyBindings.Label(GameAction.Attack)}: 불붙이기 ({GameBalance.Data.burnSeconds:0}초 화상) · 화로 불";
        string use = MaterialUse(def.Id);
        return def.Id == ItemDatabase.SlingAmmoId ? "새총 총알 · " + use : use;
    }

    // 새총에 남은 발 수와 가진 돌 (예: "남은 3발 · 돌 4개")
    public static string SlingAmmoText()
    {
        var s = GameManager.Instance.State;
        return $"남은 {s.SlingShots}발 · 돌 {s.Carried.Count(ItemDatabase.SlingAmmoId)}개";
    }

    // 재료를 어디에 쓰는지 (예: "제작대·화로 재료")
    public static string MaterialUse(string id)
    {
        var uses = new List<string>();
        if (UsedIn(ItemDatabase.Recipes, id)) uses.Add("제작대");
        if (UsedIn(ItemDatabase.SmeltRecipes, id)) uses.Add("화로");
        if (ItemDatabase.TruckParts.ContainsKey(id)) uses.Add("트럭");
        return uses.Count > 0 ? string.Join("·", uses) + " 재료" : "재료";
    }

    static bool UsedIn(List<Recipe> recipes, string id)
    {
        foreach (var r in recipes)
            if (r.Ingredients.ContainsKey(id)) return true;
        return false;
    }

    // 받침이 있으면 "을", 없으면 "를"
    public static string Eul(string word) => Josa(word, "을", "를", "을(를)");

    // 받침이 있으면 "은", 없으면 "는"
    public static string Eun(string word) => Josa(word, "은", "는", "은(는)");

    static string Josa(string word, string withFinal, string without, string unknown)
    {
        if (string.IsNullOrEmpty(word)) return unknown;
        char c = word[word.Length - 1];
        if (c < 0xAC00 || c > 0xD7A3) return unknown;
        return (c - 0xAC00) % 28 != 0 ? withFinal : without;
    }
}
