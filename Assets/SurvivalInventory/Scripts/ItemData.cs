using System.Collections.Generic;
using UnityEngine;

namespace SurvivalInventory
{
    public enum ItemCategory
    {
        Material,   // 재료: 천, 고철
        Medical,    // 의료: 붕대
        Resource,   // 자원: 연료, 배터리
        Tool,       // 도구: 손전등
        Equipment   // 장비: 가방
    }

    /// <summary>낮은 등급 → 높은 등급: 일반 < 희귀 < 고급 < 영웅 < 전설 < 신화</summary>
    public enum ItemRarity
    {
        Common,     // 일반 - 회색
        Rare,       // 희귀 - 파랑
        Advanced,   // 고급 - 초록
        Heroic,     // 영웅 - 보라
        Legendary,  // 전설 - 노랑
        Mythic      // 신화 - 빨강
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "Survival Inventory/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public ItemCategory category;
        public ItemRarity rarity;

        [Header("아이콘")]
        public Sprite icon;
        [Tooltip("아이콘이 없을 때 칸에 표시할 짧은 글자")]
        public string shortLabel;

        [Header("수치")]
        [Min(1)] public int maxStack = 1;
        [Min(0f)] public float weight = 0.1f;

        [Header("사용")]
        public bool usable;
        [Tooltip("사용하면 1개 줄어드는지 (붕대 O, 손전등 X)")]
        public bool consumeOnUse;
        public string useLabel = "사용";
    }

    public static class ItemRarityInfo
    {
        public static string DisplayName(ItemRarity r) => r switch
        {
            ItemRarity.Common => "일반",
            ItemRarity.Rare => "희귀",
            ItemRarity.Advanced => "고급",
            ItemRarity.Heroic => "영웅",
            ItemRarity.Legendary => "전설",
            ItemRarity.Mythic => "신화",
            _ => r.ToString()
        };

        public static Color GetColor(ItemRarity r) => r switch
        {
            ItemRarity.Common => new Color32(0xA3, 0xA3, 0x9C, 0xFF),
            ItemRarity.Rare => new Color32(0x4A, 0x8F, 0xE6, 0xFF),
            ItemRarity.Advanced => new Color32(0x5B, 0xBD, 0x55, 0xFF),
            ItemRarity.Heroic => new Color32(0xA8, 0x67, 0xE0, 0xFF),
            ItemRarity.Legendary => new Color32(0xE8, 0xB8, 0x36, 0xFF),
            ItemRarity.Mythic => new Color32(0xE0, 0x4A, 0x42, 0xFF),
            _ => Color.gray
        };

        static readonly Dictionary<ItemRarity, Texture2D> backgrounds = new();

        /// <summary>칸 배경 그림. Resources/SurvivalInventory/Rarity/{등급}.png</summary>
        public static Texture2D GetBackground(ItemRarity r)
        {
            if (!backgrounds.TryGetValue(r, out var tex) || tex == null)
            {
                tex = Resources.Load<Texture2D>($"SurvivalInventory/Rarity/{r.ToString().ToLowerInvariant()}");
                backgrounds[r] = tex;
            }
            return tex;
        }
    }

    public static class ItemCategoryInfo
    {
        public static string DisplayName(ItemCategory c) => c switch
        {
            ItemCategory.Material => "재료",
            ItemCategory.Medical => "의료",
            ItemCategory.Resource => "자원",
            ItemCategory.Tool => "도구",
            ItemCategory.Equipment => "장비",
            _ => c.ToString()
        };

        // 칸 위쪽 테이프 색. 보급 상자에 붙인 분류 테이프처럼 보이게 한다.
        public static Color GetColor(ItemCategory c) => c switch
        {
            ItemCategory.Material => new Color32(0xA8, 0x97, 0x7A, 0xFF),  // 삼베색
            ItemCategory.Medical => new Color32(0xC9, 0x4F, 0x45, 0xFF),   // 구급 적색
            ItemCategory.Resource => new Color32(0xD9, 0xA4, 0x3A, 0xFF),  // 연료통 황색
            ItemCategory.Tool => new Color32(0x5E, 0x9C, 0xAE, 0xFF),      // 공구 청록
            ItemCategory.Equipment => new Color32(0x86, 0x9E, 0x5C, 0xFF), // 군용 올리브
            _ => Color.gray
        };
    }
}
