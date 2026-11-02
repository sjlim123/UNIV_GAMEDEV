using System.Collections.Generic;
using UnityEngine;

// 씬이 바뀌어도 유지되는 한 판의 진행 상태. 저장 파일(SaveSystem)로 그대로 옮겨진다.
public class GameState
{
    public static int MaxTurns => GameBalance.Data.maxTurns;

    public int Turn;
    public float Health;
    public float Hunger;  // 0이 되면 턴마다 체력 감소
    public float Thirst;
    public float Power;   // 방공호 발전기 전력 (%)
    public float FlashlightCharge = 100f;
    // 탐험에 나가 있는 동안 저장되면 true (이어하기 때 탐험을 포기한 것으로 친다)
    public bool InExpedition;
    // 저장할 때 방공호 안에 살아 있던 침입 좀비 수 (이어하기 때 다시 들어온다)
    public int ShelterInvaders;

    public readonly Inventory Carried = new Inventory(); // 몸에 지닌 것
    public readonly Inventory Storage = new Inventory(); // 방공호 창고
    // 도시 트럭 정비소에 넣은 트럭 재료 (ItemDatabase.TruckParts만큼 모이면 탈출할 수 있다)
    public readonly Inventory Truck = new Inventory();

    // 장비 칸에 멘 가방·입은 방어구의 아이템 id (없으면 null). 장비는 무게에 들어가지 않는다
    public string EquippedBackpack;
    public string EquippedArmor;

    // 방공호에 화로를 설치했는가 (제작대에서 화로를 만들면 true)
    public bool FurnaceBuilt;
    // 새총에 메겨 둔 돌로 더 쏠 수 있는 발 수 (0이면 다음에 쏠 때 돌 1개를 메긴다)
    public int SlingShots;

    // 가방의 무게 보너스는 가방마다 다르다 (낡은 +2 / 일반 +4 / 고급 +8)
    public float CarryCapacity => GameBalance.Data.carryCapacity + (EquippedBackpack != null ? ItemDatabase.Get(EquippedBackpack).CarryBonus : 0f);

    // 방어구를 입으면 최대 체력이 늘어난다 (가죽 +20 / 철 +40 / 합금 +60)
    public float MaxHealth => GameBalance.Data.playerMaxHealth + (EquippedArmor != null ? ItemDatabase.Get(EquippedArmor).HealthBonus : 0);

    public bool CanCarry(string id, int n = 1)
    {
        return Carried.Weight + Carried.UnitWeight(id) * n <= CarryCapacity + 0.001f;
    }

    // ---------- 장비 칸 ----------

    public string Equipped(EquipSlot slot) => slot == EquipSlot.Backpack ? EquippedBackpack : slot == EquipSlot.Armor ? EquippedArmor : null;

    void SetEquipped(EquipSlot slot, string id)
    {
        if (slot == EquipSlot.Backpack) EquippedBackpack = id;
        else if (slot == EquipSlot.Armor) EquippedArmor = id;
    }

    public bool HasArmor => EquippedArmor != null;

    // 소지품에 있는 장비를 장비 칸으로 옮긴다. 칸에 있던 것은 소지품으로 돌아온다.
    // 더 작은 가방으로 바꾸면 무게를 감당할 수 있을 때만 바꾼다
    public bool CanEquip(string id)
    {
        var def = ItemDatabase.Get(id);
        if (def.Slot == EquipSlot.None || Carried.Count(id) == 0) return false;
        if (def.Slot != EquipSlot.Backpack) return true;
        return Carried.Weight <= GameBalance.Data.carryCapacity + def.CarryBonus + 0.001f;
    }

    public bool Equip(string id)
    {
        if (!CanEquip(id)) return false;
        var slot = ItemDatabase.Get(id).Slot;
        Carried.Remove(id);
        var old = Equipped(slot);
        SetEquipped(slot, id);
        if (old != null) Carried.Add(old);
        ClampHealth();
        return true;
    }

    // 장비 칸에서 빼서 소지품으로 옮긴다. 가방을 빼면 들 수 있는 무게가 줄어서, 그래도 감당할 수 있을 때만 뺀다
    public bool CanUnequip(EquipSlot slot)
    {
        if (Equipped(slot) == null) return false;
        if (slot == EquipSlot.Backpack) return Carried.Weight <= GameBalance.Data.carryCapacity + 0.001f;
        return true;
    }

    public bool Unequip(EquipSlot slot)
    {
        if (!CanUnequip(slot)) return false;
        Carried.Add(Equipped(slot));
        SetEquipped(slot, null);
        ClampHealth();
        return true;
    }

    // 방어구를 벗거나 바꿔 최대 체력이 줄면 체력도 그만큼 줄인다
    void ClampHealth() => Health = Mathf.Min(Health, MaxHealth);

    // ---------- 트럭 ----------

    public int TruckNeed(string id) => ItemDatabase.TruckParts.TryGetValue(id, out var n) ? Mathf.Max(0, n - Truck.Count(id)) : 0;

    public bool TruckComplete
    {
        get
        {
            foreach (var kv in ItemDatabase.TruckParts)
                if (Truck.Count(kv.Key) < kv.Value) return false;
            return true;
        }
    }

    // 소지품의 재료를 트럭에 넣는다 (필요한 만큼만). 넣은 개수를 돌려준다
    public int DeliverToTruck(string id)
    {
        int n = Mathf.Min(TruckNeed(id), Carried.Count(id));
        if (n <= 0) return 0;
        Carried.Remove(id, n);
        Truck.Add(id, n);
        return n;
    }

    public int Total(string id) => Carried.Count(id) + Storage.Count(id);

    // 창고에서 먼저 쓰고, 모자라면 소지품에서 쓴다
    public bool ConsumeFromAnywhere(string id, int n)
    {
        if (Total(id) < n) return false;
        int fromStorage = System.Math.Min(n, Storage.Count(id));
        Storage.Remove(id, fromStorage);
        Carried.Remove(id, n - fromStorage);
        return true;
    }

    public GameState() : this(true) { }

    GameState(bool startingKit)
    {
        var b = GameBalance.Data;
        Health = b.startHealth;
        Hunger = b.startHunger;
        Thirst = b.startThirst;
        Power = b.startPower;
        if (!startingKit) return;

        Carried.Add("flashlight");
        Carried.Add("battery");
        Carried.Add("water");

        Storage.Add("water", 3);
        Storage.Add("canned_food", 3);
        Storage.Add("bandage", 2);
        Storage.Add("fuel", 1);
        Storage.Add("cloth", 2);
        Storage.Add("stick", 1);
        Storage.Add("metal", 1);
    }

    public SaveData ToSaveData()
    {
        var d = new SaveData
        {
            turn = Turn,
            health = Health,
            hunger = Hunger,
            thirst = Thirst,
            power = Power,
            flashlightCharge = FlashlightCharge,
            inExpedition = InExpedition,
            shelterInvaders = ShelterInvaders,
            equippedBackpack = EquippedBackpack ?? "",
            equippedArmor = EquippedArmor ?? "",
            furnaceBuilt = FurnaceBuilt,
            slingShots = SlingShots,
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            gameVersion = UnityEngine.Application.version,
        };
        foreach (var kv in Carried.Sorted()) d.carried.Add(new ItemCount { id = kv.Key, count = kv.Value });
        foreach (var kv in Storage.Sorted()) d.storage.Add(new ItemCount { id = kv.Key, count = kv.Value });
        foreach (var kv in Truck.Sorted()) d.truck.Add(new ItemCount { id = kv.Key, count = kv.Value });
        return d;
    }

    public static GameState FromSaveData(SaveData d)
    {
        var s = new GameState(false)
        {
            Turn = d.turn,
            Health = d.health,
            Hunger = d.hunger,
            Thirst = d.thirst,
            Power = d.power,
            FlashlightCharge = d.flashlightCharge,
            InExpedition = d.inExpedition,
            ShelterInvaders = d.shelterInvaders,
            FurnaceBuilt = d.furnaceBuilt,
            SlingShots = d.slingShots,
        };
        // 이름이 바뀐 예전 아이템은 지금 아이템으로 바꾸고, 없어진 아이템은 건너뛴다
        bool legacy = d.saveFormat < 3;
        foreach (var c in d.carried) AddLoaded(s.Carried, c, legacy);
        foreach (var c in d.storage) AddLoaded(s.Storage, c, legacy);
        if (d.truck != null) foreach (var c in d.truck) AddLoaded(s.Truck, c, legacy);
        s.EquippedBackpack = ValidEquip(LoadedId(d.equippedBackpack, legacy), EquipSlot.Backpack);
        s.EquippedArmor = ValidEquip(LoadedId(d.equippedArmor, legacy), EquipSlot.Armor);
        // 예전 저장(장비 칸이 없던 때): 소지품의 가방 하나를 장비 칸으로 옮긴다
        if (d.saveFormat < 2 && s.EquippedBackpack == null && s.Carried.Count("old_backpack") > 0) s.Equip("old_backpack");
        return s;
    }

    static string LoadedId(string id, bool legacy)
    {
        if (legacy && id != null && ItemDatabase.LegacyIds.TryGetValue(id, out var now)) return now;
        return id;
    }

    static void AddLoaded(Inventory inv, ItemCount c, bool legacy)
    {
        string id = LoadedId(c.id, legacy);
        if (id != null && ItemDatabase.Items.ContainsKey(id)) inv.Add(id, c.count);
    }

    static string ValidEquip(string id, EquipSlot slot)
    {
        if (string.IsNullOrEmpty(id) || !ItemDatabase.Items.TryGetValue(id, out var def) || def.Slot != slot) return null;
        return id;
    }
}

[System.Serializable]
public class ItemCount
{
    public string id;
    public int count;
}

[System.Serializable]
public class SaveData
{
    public int saveFormat = 3; // 2: 장비 칸·트럭 추가 / 3: 새 조합법 (아이템 이름 바뀜)·화로 설치·새총
    public string gameVersion;
    public string savedAt;
    public int turn;
    public float health, hunger, thirst, power, flashlightCharge;
    public bool inExpedition;
    public int shelterInvaders;
    public string equippedBackpack, equippedArmor;
    public bool furnaceBuilt;
    public int slingShots;
    public List<ItemCount> truck = new List<ItemCount>();
    public List<ItemCount> carried = new List<ItemCount>();
    public List<ItemCount> storage = new List<ItemCount>();
}
