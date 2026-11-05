using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory { Health, Food, Equipment, Material }

// 장비 칸: 가방·방어구는 소지품이 아니라 이 칸에 메거나 입는다
public enum EquipSlot { None, Backpack, Armor }

// 아이템 등급: 일반 < 희귀 < 고급 < 영웅 < 전설 < 신화
public enum ItemRarity { Common, Rare, Advanced, Heroic, Legendary, Mythic }

// 무기 종류: 휘두르기(방망이) / 찌르기(단검) / 쏘기(새총, 돌을 총알로 쓴다)
public enum WeaponKind { None, Swing, Stab, Sling }

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

    // 일반 회색 / 희귀 파랑 / 고급 초록 / 영웅 보라 / 전설 빨강 / 신화 노랑
    public static Color Color(ItemRarity r)
    {
        switch (r)
        {
            case ItemRarity.Rare: return new Color32(0x4A, 0x8F, 0xE6, 0xFF);
            case ItemRarity.Advanced: return new Color32(0x5B, 0xBD, 0x55, 0xFF);
            case ItemRarity.Heroic: return new Color32(0xA8, 0x67, 0xE0, 0xFF);
            case ItemRarity.Legendary: return new Color32(0xE0, 0x4A, 0x42, 0xFF);
            case ItemRarity.Mythic: return new Color32(0xE8, 0xB8, 0x36, 0xFF);
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
    public EquipSlot Slot; // 장비 칸 (없으면 None)
    public float Weight;
    public int Heal, Hunger, Thirst; // 사용했을 때 회복량
    public string Description;
    // 2D 아이콘 그림 이름 (Assets/Resources/ItemIcons/{이름}.png). 없으면 3D 모양을 찍어 쓴다
    public string Icon;
    public float CarryBonus;  // 가방: 장비 칸에 메면 늘어나는 무게(kg)
    public int HealthBonus;   // 방어구: 장비 칸에 입으면 늘어나는 최대 체력
    public WeaponKind Weapon; // 무기: 핫바에서 고르면 좌클릭으로 공격한다
    public float Damage, AttackDelay;

    public bool IsConsumable => Heal != 0 || Hunger != 0 || Thirst != 0;
    public bool IsWeapon => Weapon != WeaponKind.None;
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
    // 제작대 조합법 (제작대 창은 완성품 등급별로 묶어 보여 준다. 같은 등급 안에서는 넣은 순서대로)
    public static readonly List<Recipe> Recipes = new List<Recipe>();
    // 화로에서 굽거나 녹여 만드는 것 (부싯돌과 라이터가 있어야 불을 붙인다)
    public static readonly List<Recipe> SmeltRecipes = new List<Recipe>();
    public static readonly List<TradeOffer> TradeOffers = new List<TradeOffer>();

    // 화로(설치): 제작대에서 만들면 아이템이 아니라 방공호에 화로가 생긴다
    public const string FurnaceId = "furnace";
    // 새총 총알: 돌 1개를 메기면 이만큼 쏠 수 있다
    public const string SlingAmmoId = "stone";
    public const int ShotsPerStone = 5;
    // 돌주머니: 가지고 있으면 돌의 무게가 0이 된다 (주머니 무게는 그대로)
    public const string StonePouchId = "stone_pouch";
    // 부싯돌과 라이터: 창고나 소지품에 있어야 화로에 불을 붙인다 (닳지 않는다)
    public const string LighterId = "flint_lighter";

    // 탈출용 트럭을 고치는 데 필요한 재료 (도시의 트럭 정비소에 조금씩 넣으면 저장된다)
    public static readonly Dictionary<string, int> TruckParts = new Dictionary<string, int>
    {
        { "wheel", 4 }, { "engine", 1 }, { "motor", 1 }, { "navigation", 1 }, { "fuel", 4 },
    };

    // 도시에 무작위로 놓이는 아이템과 확률 가중치.
    // 일반(회색) 필드 드랍 + 드물게 나오는 배터리. 가죽·고기는 들개를 잡을 때만 나온다.
    public static readonly Dictionary<string, int> LootWeights = new Dictionary<string, int>
    {
        { "metal", 10 }, { "cloth", 12 }, { "stick", 10 }, { "rope", 7 }, { "flashlight", 2 },
        { "bandage", 6 }, { "copper_wire", 8 }, { "canned_food", 12 }, { "fuel", 6 }, { "water", 14 },
        { "rubber", 7 }, { "stone", 9 },
        { "battery", 4 },
    };

    // 예전 저장(saveFormat 2 이하)의 아이템 id → 지금 아이템 (null이면 없앤다)
    public static readonly Dictionary<string, string> LegacyIds = new Dictionary<string, string>
    {
        { "club", "baseball_bat" }, { "backpack", "old_backpack" }, { "armor", "leather_armor" },
        { "iron_plate", "iron_ingot" }, { "tire", null }, { "engine_part", null },
    };

    static ItemDatabase()
    {
        // 일반 (회색): 필드 드랍
        Add("metal", "고철", ItemCategory.Material, ItemRarity.Common, 0.3f, "녹슨 금속 조각. 조합 재료", icon: "metal");
        Add("cloth", "천", ItemCategory.Material, ItemRarity.Common, 0.1f, "낡은 천 조각. 가방·주머니 재료", icon: "cloth");
        Add("stick", "막대기", ItemCategory.Material, ItemRarity.Common, 0.2f, "단단한 나무 막대. 조합 재료");
        Add("rope", "밧줄", ItemCategory.Material, ItemRarity.Common, 0.3f, "질긴 밧줄. 조합 재료");
        Add("copper_wire", "구리선", ItemCategory.Material, ItemRarity.Common, 0.2f, "벗겨낸 구리선. 전기 부품 재료");
        Add("rubber", "고무", ItemCategory.Material, ItemRarity.Common, 0.2f, "타이어에서 잘라낸 고무. 조합 재료");
        Add("leather", "가죽", ItemCategory.Material, ItemRarity.Common, 0.3f, "좀비개 가죽. 가방·방어구 재료");
        Add("stone", "돌", ItemCategory.Material, ItemRarity.Common, 0.3f, "주먹만 한 돌. 새총 총알(1개 = 5발) · 화로 재료");
        Add("fuel", "연료통", ItemCategory.Material, ItemRarity.Common, 1.5f, "발전기에 넣으면 전력 +35%", icon: "fuel");
        Add("flashlight", "손전등", ItemCategory.Equipment, ItemRarity.Common, 0.3f, "Q로 켜고 끈다. 배터리를 쓴다", icon: "flashlight");
        Add("bandage", "붕대", ItemCategory.Health, ItemRarity.Common, 0.1f, "체력 +10", heal: 10, icon: "bandage");
        Add("canned_food", "통조림", ItemCategory.Food, ItemRarity.Common, 0.2f, "배고픔 +20 · 체력 +5", heal: 5, hunger: 20);
        Add("raw_meat", "고기", ItemCategory.Food, ItemRarity.Common, 0.4f, "배고픔 +10. 화로에서 구우면 +40", hunger: 10);
        Add("water", "물", ItemCategory.Food, ItemRarity.Common, 0.5f, "갈증 +30", thirst: 30);

        // 희귀 (파랑)
        Add("battery", "배터리", ItemCategory.Material, ItemRarity.Rare, 0.1f, "손전등 배터리 · 전자 부품 재료", icon: "battery");
        AddBackpack("old_backpack", "낡은 가방", ItemRarity.Rare, 2);
        Add(LighterId, "부싯돌과 라이터", ItemCategory.Equipment, ItemRarity.Rare, 0.2f, "들고 공격하면 적에게 불을 붙인다 (5초 화상). 창고나 소지품에 있으면 화로에 불을 붙일 수 있다 (닳지 않는다)");
        Add("iron_ingot", "철주괴", ItemCategory.Material, ItemRarity.Rare, 0.6f, "화로에서 고철을 녹여 만든 쇳덩이. 여러 조합 재료");
        AddWeapon("baseball_bat", "야구 배트", ItemRarity.Rare, 1.0f, WeaponKind.Swing, 12, 1f, "공격력 12. 핫바에서 고르고 좌클릭으로 휘두른다");
        Add(FurnaceId, "화로", ItemCategory.Equipment, ItemRarity.Rare, 0f, "방공호 거실에 설치한다. 고철을 녹이고 고기를 굽는다");
        Add("screw", "나사", ItemCategory.Material, ItemRarity.Rare, 0.02f, "고철을 깎아 만든 나사. 무기·부품 재료");
        AddWeapon("slingshot", "새총", ItemRarity.Rare, 0.3f, WeaponKind.Sling, 10, 1f, "공격력 10. 좌클릭으로 돌을 쏜다 (돌 1개 = 5발)");
        AddArmor("leather_armor", "가죽 방어구", ItemRarity.Rare, 20);
        Add("circuit_board", "전자기판", ItemCategory.Material, ItemRarity.Rare, 0.2f, "전자기판. 반도체·모터·엔진 재료");
        Add("cooked_meat", "구운 고기", ItemCategory.Food, ItemRarity.Rare, 0.2f, "배고픔 +40", hunger: 40);
        Add("medkit", "구급상자", ItemCategory.Health, ItemRarity.Rare, 0.1f, "체력 +20", heal: 20);
        Add(StonePouchId, "돌주머니", ItemCategory.Equipment, ItemRarity.Rare, 0.3f, "가지고 있으면 돌을 얼마든지 담고, 돌 무게가 0이 된다");

        // 고급 (초록)
        AddWeapon("spiked_bat", "나사 두른 야구 배트", ItemRarity.Advanced, 1.3f, WeaponKind.Swing, 18, 1f, "공격력 18. 나사를 박아 넣은 야구 배트");
        AddWeapon("dagger", "단검", ItemRarity.Advanced, 0.4f, WeaponKind.Stab, 13, 0.5f, "공격력 13 · 공격 속도 2배 (0.5초마다)");
        AddBackpack("backpack", "일반 가방", ItemRarity.Advanced, 4);
        AddArmor("iron_armor", "철 방어구", ItemRarity.Advanced, 40);
        Add("hammer", "망치", ItemCategory.Material, ItemRarity.Advanced, 1.0f, "단단한 쇠망치. 합금 갑옷 재료");
        Add("semiconductor", "반도체", ItemCategory.Material, ItemRarity.Advanced, 0.1f, "반도체. 네비게이션 재료");

        // 영웅 (보라)
        Add("wheel", "바퀴", ItemCategory.Material, ItemRarity.Heroic, 2.5f, "트럭 바퀴. 트럭 재료");
        Add("alloy_plate", "합금판", ItemCategory.Material, ItemRarity.Heroic, 0.8f, "단단한 합금판. 합금 갑옷·합금 방망이 재료");
        AddBackpack("large_backpack", "고급 가방", ItemRarity.Heroic, 8);
        AddArmor("alloy_armor", "합금 갑옷", ItemRarity.Heroic, 60);
        Add("motor", "모터", ItemCategory.Material, ItemRarity.Heroic, 2.0f, "트럭 시동 모터. 트럭 재료");
        AddWeapon("alloy_bat", "합금 방망이", ItemRarity.Heroic, 1.6f, WeaponKind.Swing, 20, 1f, "공격력 20. 합금판을 덧댄 방망이");

        // 전설 (빨강)
        Add("engine", "엔진", ItemCategory.Material, ItemRarity.Legendary, 3.5f, "트럭 엔진. 트럭 재료");

        // 신화 (노랑)
        Add("navigation", "네비게이션", ItemCategory.Material, ItemRarity.Mythic, 0.5f, "도시를 빠져나갈 길을 찾아 준다. 트럭 재료");

        // 제작대 조합법 (등급 순)
        AddRecipe("battery", 1, ("copper_wire", 2));
        AddRecipe("old_backpack", 1, ("cloth", 5), ("rope", 2), ("leather", 2));
        AddRecipe("flashlight", 1, ("battery", 1), ("metal", 4));
        AddRecipe(LighterId, 1, ("stone", 3), ("metal", 2), ("cloth", 2));
        AddRecipe("baseball_bat", 1, ("stick", 8));
        AddRecipe(FurnaceId, 1, ("stone", 8));
        AddRecipe("screw", 5, ("metal", 2));
        AddRecipe("slingshot", 1, ("stick", 2), ("rubber", 2), ("rope", 1));
        AddRecipe("leather_armor", 1, ("leather", 15));
        AddRecipe("circuit_board", 1, ("iron_ingot", 5), ("copper_wire", 10), ("battery", 2));
        AddRecipe("medkit", 1, ("bandage", 3));
        AddRecipe(StonePouchId, 1, ("rope", 3), ("cloth", 2));

        AddRecipe("spiked_bat", 1, ("screw", 15), ("baseball_bat", 1), ("rope", 4));
        AddRecipe("dagger", 1, ("iron_ingot", 2), ("stick", 3), ("rope", 2));
        AddRecipe("backpack", 1, ("old_backpack", 1), ("cloth", 5), ("rope", 3), ("iron_ingot", 2));
        AddRecipe("iron_armor", 1, ("leather_armor", 1), ("iron_ingot", 10));
        AddRecipe("hammer", 1, ("iron_ingot", 5), ("stick", 5), ("rope", 3));
        AddRecipe("semiconductor", 1, ("circuit_board", 3), ("iron_ingot", 3), ("copper_wire", 10), ("screw", 4), ("battery", 2));

        AddRecipe("wheel", 1, ("rubber", 10), ("iron_ingot", 8));
        AddRecipe("alloy_plate", 1, ("iron_ingot", 5), ("copper_wire", 5));
        AddRecipe("large_backpack", 1, ("backpack", 1), ("iron_ingot", 10), ("rope", 10));
        AddRecipe("alloy_armor", 1, ("iron_armor", 1), ("alloy_plate", 5), ("hammer", 1));
        AddRecipe("motor", 1, ("circuit_board", 5), ("copper_wire", 10), ("screw", 8), ("iron_ingot", 4), ("battery", 3));
        AddRecipe("alloy_bat", 1, ("baseball_bat", 1), ("alloy_plate", 10), ("screw", 10));

        AddRecipe("engine", 1, ("circuit_board", 1), ("copper_wire", 10), ("screw", 20), ("iron_ingot", 15));

        AddRecipe("navigation", 1, ("semiconductor", 5), ("copper_wire", 10), ("screw", 4), ("iron_ingot", 10), ("battery", 10));

        // 화로 (부싯돌과 라이터 필요)
        AddSmelt("iron_ingot", 1, ("metal", 3));
        AddSmelt("cooked_meat", 1, ("raw_meat", 1));

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

    static ItemDef Add(string id, string name, ItemCategory cat, ItemRarity rarity, float weight, string desc, int heal = 0, int hunger = 0, int thirst = 0, string icon = null, EquipSlot slot = EquipSlot.None)
    {
        var def = new ItemDef { Id = id, Name = name, Category = cat, Rarity = rarity, Weight = weight, Description = desc, Heal = heal, Hunger = hunger, Thirst = thirst, Icon = icon, Slot = slot };
        Items[id] = def;
        return def;
    }

    // 가방·방어구는 장비 칸에 들어가서 무게가 0이다
    static void AddBackpack(string id, string name, ItemRarity rarity, float bonus) =>
        Add(id, name, ItemCategory.Equipment, rarity, 0f, $"장비 칸에 메면 들 수 있는 무게 +{bonus:0.#}kg", icon: "bag", slot: EquipSlot.Backpack).CarryBonus = bonus;

    static void AddArmor(string id, string name, ItemRarity rarity, int bonus) =>
        Add(id, name, ItemCategory.Equipment, rarity, 0f, $"장비 칸에 입으면 최대 체력 +{bonus}", slot: EquipSlot.Armor).HealthBonus = bonus;

    static void AddWeapon(string id, string name, ItemRarity rarity, float weight, WeaponKind kind, float damage, float delay, string desc)
    {
        var def = Add(id, name, ItemCategory.Equipment, rarity, weight, desc);
        def.Weapon = kind;
        def.Damage = damage;
        def.AttackDelay = delay;
    }

    static Recipe MakeRecipe(string result, int count, (string id, int n)[] ingredients)
    {
        var r = new Recipe { ResultId = result, ResultCount = count, Ingredients = new Dictionary<string, int>() };
        foreach (var (id, n) in ingredients) r.Ingredients[id] = n;
        return r;
    }

    static void AddRecipe(string result, int count, params (string id, int n)[] ingredients) => Recipes.Add(MakeRecipe(result, count, ingredients));

    static void AddSmelt(string result, int count, params (string id, int n)[] ingredients) => SmeltRecipes.Add(MakeRecipe(result, count, ingredients));

    static void AddTrade(string getId, int getCount, params (string id, int n)[] give)
    {
        var t = new TradeOffer { GetId = getId, GetCount = getCount, Give = new Dictionary<string, int>() };
        foreach (var (id, n) in give) t.Give[id] = n;
        TradeOffers.Add(t);
    }

    // 필드 아이템 하나를 뽑는다. 건물 밖 길바닥(outdoor) 자리에는 돌만, 건물 안에는 돌을 뺀 나머지가 나온다
    public static string RollLoot(bool outdoor)
    {
        if (outdoor) return SlingAmmoId;
        int total = 0;
        foreach (var kv in LootWeights)
            if (kv.Key != SlingAmmoId) total += kv.Value;
        int roll = Random.Range(0, total);
        foreach (var kv in LootWeights)
        {
            if (kv.Key == SlingAmmoId) continue;
            if (roll < kv.Value) return kv.Key;
            roll -= kv.Value;
        }
        return "cloth";
    }
}
