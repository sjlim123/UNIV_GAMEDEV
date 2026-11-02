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

    public float CarryCapacity
    {
        get
        {
            var b = GameBalance.Data;
            return b.carryCapacity + (Carried.Count("backpack") > 0 ? b.backpackBonus : 0f);
        }
    }

    public bool CanCarry(string id, int n = 1)
    {
        // 첫 가방은 메는 순간 들 수 있는 무게가 늘어난다
        float cap = CarryCapacity;
        if (id == "backpack" && n > 0 && Carried.Count("backpack") == 0) cap += GameBalance.Data.backpackBonus;
        return Carried.Weight + ItemDatabase.Get(id).Weight * n <= cap + 0.001f;
    }

    // 가방을 내려놓거나 버린 뒤에도 무게를 감당할 수 있는가 (마지막 가방일 때만 따진다)
    public bool CanDropBackpack(int n = 1)
    {
        if (Carried.Count("backpack") > n) return true;
        float after = Carried.Weight - ItemDatabase.Get("backpack").Weight * Mathf.Min(n, Carried.Count("backpack"));
        return after <= GameBalance.Data.carryCapacity + 0.001f;
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
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            gameVersion = UnityEngine.Application.version,
        };
        foreach (var kv in Carried.Sorted()) d.carried.Add(new ItemCount { id = kv.Key, count = kv.Value });
        foreach (var kv in Storage.Sorted()) d.storage.Add(new ItemCount { id = kv.Key, count = kv.Value });
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
        };
        // 버전이 바뀌며 없어진 아이템은 건너뛴다
        foreach (var c in d.carried) if (ItemDatabase.Items.ContainsKey(c.id)) s.Carried.Add(c.id, c.count);
        foreach (var c in d.storage) if (ItemDatabase.Items.ContainsKey(c.id)) s.Storage.Add(c.id, c.count);
        return s;
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
    public int saveFormat = 1;
    public string gameVersion;
    public string savedAt;
    public int turn;
    public float health, hunger, thirst, power, flashlightCharge;
    public bool inExpedition;
    public int shelterInvaders;
    public List<ItemCount> carried = new List<ItemCount>();
    public List<ItemCount> storage = new List<ItemCount>();
}
