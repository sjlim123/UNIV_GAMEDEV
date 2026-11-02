using System.Collections.Generic;
using System.Linq;

public class Inventory
{
    readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    public int Count(string id) => counts.TryGetValue(id, out var n) ? n : 0;

    public bool IsEmpty => counts.Count == 0;

    public float Weight
    {
        get
        {
            float w = 0;
            foreach (var kv in counts) w += UnitWeight(kv.Key) * kv.Value;
            return w;
        }
    }

    // 이 안에서 한 개의 무게. 돌주머니가 함께 있으면 돌은 무게가 없다
    public float UnitWeight(string id)
    {
        if (id == ItemDatabase.SlingAmmoId && Count(ItemDatabase.StonePouchId) > 0) return 0f;
        return ItemDatabase.Get(id).Weight;
    }

    public void Add(string id, int n = 1)
    {
        if (n <= 0) return;
        counts[id] = Count(id) + n;
    }

    public bool Remove(string id, int n = 1)
    {
        int have = Count(id);
        if (n <= 0 || have < n) return false;
        if (have == n) counts.Remove(id);
        else counts[id] = have - n;
        return true;
    }

    public bool Has(Dictionary<string, int> required)
    {
        foreach (var kv in required)
            if (Count(kv.Key) < kv.Value) return false;
        return true;
    }

    public Dictionary<string, int> Snapshot() => new Dictionary<string, int>(counts);

    // 체력 → 식량 → 장비 → 재료 순서로 정렬
    public List<KeyValuePair<string, int>> Sorted()
    {
        return counts
            .OrderBy(kv => (int)ItemDatabase.Get(kv.Key).Category)
            .ThenBy(kv => ItemDatabase.Name(kv.Key))
            .ToList();
    }
}
