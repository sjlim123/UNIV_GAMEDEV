// 씬이 바뀌어도 유지되는 한 판의 진행 상태
public class GameState
{
    public static int MaxTurns => GameBalance.Data.maxTurns;

    public int Turn;
    public float Health;
    public float Hunger;  // 0이 되면 턴마다 체력 감소
    public float Thirst;
    public float Power;   // 방공호 발전기 전력 (%)
    public float FlashlightCharge = 100f;

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
        return Carried.Weight + ItemDatabase.Get(id).Weight * n <= CarryCapacity + 0.001f;
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

    public GameState()
    {
        var b = GameBalance.Data;
        Health = b.startHealth;
        Hunger = b.startHunger;
        Thirst = b.startThirst;
        Power = b.startPower;

        Carried.Add("flashlight");
        Carried.Add("battery");
        Carried.Add("water");

        Storage.Add("water", 3);
        Storage.Add("canned_food", 3);
        Storage.Add("bandage", 2);
        Storage.Add("fuel", 1);
        Storage.Add("cloth", 2);
        Storage.Add("wood", 1);
        Storage.Add("metal", 1);
    }
}
