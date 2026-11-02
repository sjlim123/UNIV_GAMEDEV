using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory { Health, Food, Equipment, Material }

// 아이템 등급: 일반 < 희귀 < 고급 < 영웅 < 전설 < 신화
public enum ItemRarity { Common, Rare, Advanced, Heroic, Legendary, Mythic }

public static class ItemRarityInfo
{
    public static string Name(ItemRarity r)
    {
        switch (r)
        {
            case ItemRarity.Rare: return "희귀";
            case ItemRarity.Advanced: return "고급";
            case ItemRarity.Heroic: return "영웅";
            case ItemRarity.Legendary: return "전설";
            case ItemRarity.Mythic: return "신화";
            default: return "일반";
        }
    }

    // 일반 회색 / 희귀 파랑 / 고급 초록 / 영웅 보라 / 전설 노랑 / 신화 빨강
    public static Color Color(ItemRarity r)
    {
        switch (r)
        {
            case ItemRarity.Rare: return new Color32(0x4A, 0x8F, 0xE6, 0xFF);
            case ItemRarity.Advanced: return new Color32(0x5B, 0xBD, 0x55, 0xFF);
            case ItemRarity.Heroic: return new Color32(0xA8, 0x67, 0xE0, 0xFF);
            case ItemRarity.Legendary: return new Color32(0xE8, 0xB8, 0x36, 0xFF);
            case ItemRarity.Mythic: return new Color32(0xE0, 0x4A, 0x42, 0xFF);
            default: return new Color32(0xA3, 0xA3, 0x9C, 0xFF);
        }
    }

    public static string Hex(ItemRarity r) => "#" + ColorUtility.ToHtmlStringRGB(Color(r));

    static readonly Dictionary<ItemRarity, Texture2D> backgrounds = new Dictionary<ItemRarity, Texture2D>();

    // 칸 배경 그림 (SurvivalInventory 패키지의 Resources/SurvivalInventory/Rarity/{등급}.png)
    public static Texture2D Background(ItemRarity r)
    {
        if (!backgrounds.TryGetValue(r, out var tex) || tex == null)
        {
            tex = Resources.Load<Texture2D>("SurvivalInventory/Rarity/" + r.ToString().ToLowerInvariant());
            backgrounds[r] = tex;
        }
        return tex;
    }
}

public class ItemDef
{
    public string Id;
    public string Name;
    public ItemCategory Category;
    public ItemRarity Rarity;
    public float Weight;
    public int Heal, Hunger, Thirst; // 사용했을 때 회복량
    public string Description;
    // 2D 아이콘 그림 이름 (Assets/Resources/ItemIcons/{이름}.png). 없으면 3D 모양을 찍어 쓴다
    public string Icon;

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

    // 도시에 무작위로 놓이는 아이템과 확률 가중치.
    // 일반(회색) 필드 드랍 + 드물게 나오는 높은 등급(배터리·의약품). 가죽·고기는 들개를 잡을 때만 나온다.
    public static readonly Dictionary<string, int> LootWeights = new Dictionary<string, int>
    {
        { "metal", 10 }, { "cloth", 12 }, { "stick", 10 }, { "rope", 7 }, { "flashlight", 2 },
        { "bandage", 6 }, { "copper_wire", 8 }, { "canned_food", 12 }, { "fuel", 6 }, { "water", 14 },
        { "rubber", 7 },
        { "battery", 4 }, { "medkit", 2 },
    };

    static ItemDatabase()
    {
        // 일반 (회색)
        Add("metal", "고철", ItemCategory.Material, ItemRarity.Common, 0.5f, "녹슨 금속 조각. 조합 재료", icon: "metal");
        Add("cloth", "천", ItemCategory.Material, ItemRarity.Common, 0.1f, "낡은 천 조각. 붕대 재료", icon: "cloth");
        Add("stick", "막대기", ItemCategory.Material, ItemRarity.Common, 0.4f, "단단한 나무 막대. 조합 재료");
        Add("rope", "밧줄", ItemCategory.Material, ItemRarity.Common, 0.3f, "질긴 밧줄. 조합 재료");
        Add("copper_wire", "구리선", ItemCategory.Material, ItemRarity.Common, 0.2f, "벗겨낸 구리선. 전기 부품 재료");
        Add("rubber", "고무", ItemCategory.Material, ItemRarity.Common, 0.2f, "타이어에서 잘라낸 고무. 조합 재료");
        Add("leather", "가죽", ItemCategory.Material, ItemRarity.Common, 0.3f, "들개 가죽. 가방 재료");
        Add("fuel", "연료통", ItemCategory.Material, ItemRarity.Common, 1.5f, "발전기에 넣으면 전력 +35%", icon: "fuel");
        Add("flashlight", "손전등", ItemCategory.Equipment, ItemRarity.Common, 0.3f, "Q로 켜고 끈다. 배터리를 쓴다", icon: "flashlight");
        Add("bandage", "붕대", ItemCategory.Health, ItemRarity.Common, 0.1f, "체력 +25", heal: 25, icon: "bandage");
        Add("canned_food", "통조림", ItemCategory.Food, ItemRarity.Common, 0.4f, "배고픔 +30", hunger: 30);
        Add("raw_meat", "고기", ItemCategory.Food, ItemRarity.Common, 0.4f, "배고픔 +10", hunger: 10);
        Add("water", "물", ItemCategory.Food, ItemRarity.Common, 0.5f, "갈증 +30", thirst: 30);

        // 희귀 (파랑) 이상
        Add("battery", "배터리", ItemCategory.Material, ItemRarity.Rare, 0.1f, "손전등 배터리", icon: "battery");
        Add("club", "둔기", ItemCategory.Equipment, ItemRarity.Rare, 1.0f, "핫바에서 고르면 좌클릭 공격이 세진다");
        Add("backpack", "가방", ItemCategory.Equipment, ItemRarity.Rare, 0.8f, "들 수 있는 무게 +5kg", icon: "bag");
        Add("medkit", "의약품", ItemCategory.Health, ItemRarity.Advanced, 0.4f, "체력 +60", heal: 60);

        AddRecipe("bandage", 1, ("cloth", 2));
        AddRecipe("club", 1, ("stick", 1), ("metal", 1));
        AddRecipe("flashlight", 1, ("copper_wire", 1), ("metal", 1), ("rubber", 1));
        AddRecipe("battery", 1, ("copper_wire", 2), ("rubber", 1));
        AddRecipe("backpack", 1, ("leather", 2), ("rope", 1));

        AddTrade("canned_food", 1, ("metal", 2));
        AddTrade("water", 1, ("cloth", 2));
        AddTrade("battery", 1, ("copper_wire", 1), ("stick", 1));
        AddTrade("medkit", 1, ("metal", 3));
        AddTrade("fuel", 1, ("canned_food", 2));
        AddTrade("bandage", 2, ("stick", 2));
    }

    public static ItemDef Get(string id)
    {
        if (id != null && Items.TryGetValue(id, out var def)) return def;
        return new ItemDef { Id = id, Name = id, Weight = 0, Description = "" };
    }

    public static string Name(string id) => Get(id).Name;

    static void Add(string id, string name, ItemCategory cat, ItemRarity rarity, float weight, string desc, int heal = 0, int hunger = 0, int thirst = 0, string icon = null)
    {
        Items[id] = new ItemDef { Id = id, Name = name, Category = cat, Rarity = rarity, Weight = weight, Description = desc, Heal = heal, Hunger = hunger, Thirst = thirst, Icon = icon };
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
        int roll = rng != null ? rng.Next(total) : Random.Range(0, total);
        foreach (var kv in LootWeights)
        {
            if (roll < kv.Value) return kv.Key;
            roll -= kv.Value;
        }
        return "cloth";
    }
}
