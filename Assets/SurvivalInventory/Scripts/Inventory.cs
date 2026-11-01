using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SurvivalInventory
{
    [Serializable]
    public class ItemStack
    {
        public ItemData item;
        public int count;

        public bool IsEmpty => item == null || count <= 0;

        public void Clear()
        {
            item = null;
            count = 0;
        }
    }

    [Serializable]
    public struct StartingItem
    {
        public ItemData item;
        [Min(1)] public int count;
    }

    /// <summary>
    /// UI와 분리된 인벤토리 데이터. 게임 로직은 이 컴포넌트의 이벤트만 구독하면 된다.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [SerializeField, Min(1)] int slotCount = 24;
        [SerializeField, Min(0f)] float maxWeight = 30f;
        [SerializeField] List<StartingItem> startingItems = new();

        ItemStack[] slots;

        /// <summary>내용이 바뀔 때마다 호출 (UI 갱신용)</summary>
        public event Action Changed;
        /// <summary>아이템을 사용했을 때. 붕대→체력 회복, 손전등→켜기 등은 여기서 연결</summary>
        public event Action<ItemData> ItemUsed;
        /// <summary>아이템을 버렸을 때 (아이템, 개수). 월드에 떨구는 처리는 여기서 연결</summary>
        public event Action<ItemData, int> ItemDropped;

        public int SlotCount { get { EnsureInit(); return slots.Length; } }

        public float MaxWeight
        {
            get => maxWeight;
            set { maxWeight = Mathf.Max(0f, value); Changed?.Invoke(); }
        }

        public float CurrentWeight
        {
            get
            {
                EnsureInit();
                float w = 0f;
                foreach (var s in slots)
                    if (!s.IsEmpty) w += s.item.weight * s.count;
                return w;
            }
        }

        void Awake() => EnsureInit();

        void EnsureInit()
        {
            if (slots != null) return;

            slots = new ItemStack[slotCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = new ItemStack();

            foreach (var start in startingItems)
            {
                if (start.item == null)
                {
                    Debug.LogWarning("[Inventory] 시작 아이템 칸이 비었거나 아이템 에셋이 사라졌습니다 (Missing). Play를 멈추고 메뉴 Tools → Survival Inventory → 3번을 누르면 정리됩니다 (열린 씬만).");
                    continue;
                }
                int added = AddInternal(start.item, start.count);
                if (added < start.count)
                    Debug.LogWarning($"[Inventory] 시작 아이템 '{start.item.displayName}' {start.count}개 중 {added}개만 들어갔습니다 (칸 또는 무게 부족).");
            }
        }

        public ItemStack GetSlot(int index)
        {
            EnsureInit();
            return slots[index];
        }

        /// <summary>아이템을 넣고, 실제로 들어간 개수를 돌려준다.</summary>
        public int Add(ItemData item, int amount = 1)
        {
            EnsureInit();
            int added = AddInternal(item, amount);
            if (added > 0) Changed?.Invoke();
            return added;
        }

        int AddInternal(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return 0;

            int fitByWeight = item.weight > 0f
                ? Mathf.FloorToInt((maxWeight - CurrentWeight) / item.weight + 0.001f)
                : amount;
            int remaining = Mathf.Min(amount, Mathf.Max(0, fitByWeight));
            int toAdd = remaining;

            // 1) 같은 아이템이 있는 칸부터 채우기
            foreach (var s in slots)
            {
                if (remaining == 0) break;
                if (s.item != item || s.count >= item.maxStack) continue;
                int n = Mathf.Min(remaining, item.maxStack - s.count);
                s.count += n;
                remaining -= n;
            }

            // 2) 빈 칸에 넣기
            foreach (var s in slots)
            {
                if (remaining == 0) break;
                if (!s.IsEmpty) continue;
                int n = Mathf.Min(remaining, item.maxStack);
                s.item = item;
                s.count = n;
                remaining -= n;
            }

            return toAdd - remaining;
        }

        public int CountOf(ItemData item)
        {
            EnsureInit();
            return slots.Where(s => s.item == item).Sum(s => s.count);
        }

        /// <summary>제작 재료 소모 등에 사용. 개수가 모자라면 아무것도 빼지 않고 false.</summary>
        public bool Remove(ItemData item, int amount)
        {
            if (CountOf(item) < amount) return false;

            for (int i = slots.Length - 1; i >= 0 && amount > 0; i--)
            {
                var s = slots[i];
                if (s.item != item) continue;
                int n = Mathf.Min(amount, s.count);
                s.count -= n;
                amount -= n;
                if (s.count <= 0) s.Clear();
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>칸 이동. 같은 아이템이면 합치고, 아니면 자리를 바꾼다.</summary>
        public void Move(int from, int to)
        {
            EnsureInit();
            if (from == to) return;

            var a = slots[from];
            var b = slots[to];
            if (a.IsEmpty) return;

            if (!b.IsEmpty && a.item == b.item && b.count < b.item.maxStack)
            {
                int n = Mathf.Min(a.count, b.item.maxStack - b.count);
                b.count += n;
                a.count -= n;
                if (a.count <= 0) a.Clear();
            }
            else
            {
                slots[from] = b;
                slots[to] = a;
            }

            Changed?.Invoke();
        }

        public void UseAt(int index)
        {
            EnsureInit();
            var s = slots[index];
            if (s.IsEmpty || !s.item.usable) return;

            var item = s.item;
            if (item.consumeOnUse)
            {
                s.count--;
                if (s.count <= 0) s.Clear();
            }

            Debug.Log($"[Inventory] 사용: {item.displayName}");
            ItemUsed?.Invoke(item);
            Changed?.Invoke();
        }

        public void DropAt(int index, int amount)
        {
            EnsureInit();
            var s = slots[index];
            if (s.IsEmpty || amount <= 0) return;

            var item = s.item;
            int n = Mathf.Min(amount, s.count);
            s.count -= n;
            if (s.count <= 0) s.Clear();

            Debug.Log($"[Inventory] 버림: {item.displayName} x{n}");
            ItemDropped?.Invoke(item, n);
            Changed?.Invoke();
        }

        /// <summary>분류 → 이름 순으로 정렬하고, 나뉜 묶음을 합친다.</summary>
        public void Sort()
        {
            EnsureInit();

            var totals = new Dictionary<ItemData, int>();
            foreach (var s in slots)
            {
                if (s.IsEmpty) continue;
                totals.TryGetValue(s.item, out int c);
                totals[s.item] = c + s.count;
            }

            var order = totals.Keys
                .OrderBy(i => i.category)
                .ThenBy(i => i.displayName, StringComparer.Ordinal)
                .ToList();

            foreach (var s in slots) s.Clear();

            int idx = 0;
            foreach (var item in order)
            {
                int left = totals[item];
                while (left > 0 && idx < slots.Length)
                {
                    int n = Mathf.Min(left, item.maxStack);
                    slots[idx].item = item;
                    slots[idx].count = n;
                    left -= n;
                    idx++;
                }
            }

            Changed?.Invoke();
        }
    }
}
