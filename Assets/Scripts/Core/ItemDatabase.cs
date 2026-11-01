using System.Collections.Generic;

public enum ItemCategory { Health, Food, Equipment, Material }

public class ItemDef
{
    public string Id;
    public string Name;
    public ItemCategory Category;
    public float Weight;
    public int Heal, Hunger, Thirst; // 사용했을 때 회복량
    public string Description;

    public bool IsConsumable => Heal != 0 || Hunger != 0 || Thirst != 0;
}

public class Recipe
{
    public string ResultId;
    public int ResultCount = 1;
    public Dictionary<string, int> Ingredients;
}

public class TradeOffer
{
    public Dictionary<string, int> Give; // 플레이어가 내는 것
    public string GetId;                 // 받는 것
    public int GetCount = 1;
}

// 아이템·조합법·잡상인 거래 목록. 숫자를 바꾸면 밸런스를 조정할 수 있다.
public static class ItemDatabase
{
    public static readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
    public static readonly List<Recipe> Recipes = new List<Recipe>();
    public static readonly List<TradeOffer> TradeOffers = new List<TradeOffer>();
    // 도시에 무작위로 놓이는 아이템과 확률 가중치
    public static readonly Dictionary<string, int> LootWeights = new Dictionary<string, int>
    {
        { "water", 14 }, { "canned_food", 12 }, { "bandage", 6 }, { "medkit", 2 },
        { "cloth", 12 }, { "wood", 10 }, { "metal", 10 }, { "wire", 8 }, { "battery", 7 }, { "fuel", 6 },
    };

    static ItemDatabase()
    {
        Add("bandage", "붕대", ItemCategory.Health, 0.1f, "체력 +25", heal: 25);
        Add("medkit", "의약품", ItemCategory.Health, 0.4f, "체력 +60", heal: 60);
        Add("water", "물", ItemCategory.Food, 1.0f, "갈증 +40", thirst: 40);
        Add("canned_food", "통조림", ItemCategory.Food, 0.5f, "배고픔 +40", hunger: 40);
        Add("raw_meat", "생고기", ItemCategory.Food, 0.6f, "배고픔 +30, 체력 -5", hunger: 30, heal: -5);
        Add("flashlight", "손전등", ItemCategory.Equipment, 0.4f, "Q로 켜고 끈다. 배터리를 쓴다");
        Add("club", "둔기", ItemCategory.Equipment, 1.5f, "좌클릭 공격력이 크게 오른다");
        Add("backpack", "가방", ItemCategory.Equipment, 0.8f, "들 수 있는 무게 +8kg");
        Add("metal", "고철", ItemCategory.Material, 1.0f, "녹슨 금속 조각. 조합 재료");
        Add("cloth", "천", ItemCategory.Material, 0.2f, "조합 재료");
        Add("wood", "목재", ItemCategory.Material, 0.8f, "조합 재료");
        Add("wire", "전선", ItemCategory.Material, 0.3f, "조합 재료");
        Add("battery", "배터리", ItemCategory.Material, 0.2f, "손전등 배터리, 조합 재료");
        Add("fuel", "연료통", ItemCategory.Material, 2.0f, "발전기에 넣으면 전력 +35%");

        AddRecipe("bandage", 1, ("cloth", 2));
        AddRecipe("club", 1, ("wood", 1), ("metal", 1));
        AddRecipe("flashlight", 1, ("wire", 1), ("battery", 1));
        AddRecipe("backpack", 1, ("cloth", 3), ("wire", 1));

        AddTrade("canned_food", 1, ("metal", 2));
        AddTrade("water", 1, ("cloth", 2));
        AddTrade("battery", 1, ("wire", 1), ("wood", 1));
        AddTrade("medkit", 1, ("metal", 3));
        AddTrade("fuel", 1, ("canned_food", 2));
        AddTrade("bandage", 2, ("wood", 2));
    }

    public static ItemDef Get(string id)
    {
        if (id != null && Items.TryGetValue(id, out var def)) return def;
        return new ItemDef { Id = id, Name = id, Weight = 0, Description = "" };
    }

    public static string Name(string id) => Get(id).Name;

    static void Add(string id, string name, ItemCategory cat, float weight, string desc, int heal = 0, int hunger = 0, int thirst = 0)
    {
        Items[id] = new ItemDef { Id = id, Name = name, Category = cat, Weight = weight, Description = desc, Heal = heal, Hunger = hunger, Thirst = thirst };
    }

    static void AddRecipe(string result, int count, params (string id, int n)[] ingredients)
    {
        var r = new Recipe { ResultId = result, ResultCount = count, Ingredients = new Dictionary<string, int>() };
        foreach (var (id, n) in ingredients) r.Ingredients[id] = n;
        Recipes.Add(r);
    }

    static void AddTrade(string getId, int getCount, params (string id, int n)[] give)
    {
        var t = new TradeOffer { GetId = getId, GetCount = getCount, Give = new Dictionary<string, int>() };
        foreach (var (id, n) in give) t.Give[id] = n;
        TradeOffers.Add(t);
    }

    public static string RollLoot(System.Random rng = null)
    {
        int total = 0;
        foreach (var w in LootWeights.Values) total += w;
        int roll = rng != null ? rng.Next(total) : UnityEngine.Random.Range(0, total);
        foreach (var kv in LootWeights)
        {
            if (roll < kv.Value) return kv.Key;
            roll -= kv.Value;
        }
        return "cloth";
    }
}
