using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SurvivalInventory
{
    [RequireComponent(typeof(UIDocument))]
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] Inventory inventory;

        [Tooltip("한글이 네모(□)로 보이면 한글 폰트(TTF/OTF)를 여기에 넣으세요. 예: Noto Sans KR")]
        [SerializeField] Font koreanFont;

        [SerializeField] bool startOpen = true;

        const float DragThreshold = 6f;
        const float GhostSize = 64f;

        class SlotView
        {
            public VisualElement root, icon;
            public Label abbr, count;
        }

        readonly List<SlotView> slotViews = new();
        readonly List<Button> tabButtons = new();

        VisualElement root, boundRoot, grid, tabs, ghost, weightBox, weightFill;
        VisualElement detailContent, detailIcon, detailArt, ghostArt;
        Label slotCountLabel, weightLabel, toast, ghostAbbr, ghostCount;
        Label detailEmpty, detailAbbr, detailName, detailCategory, detailRarity, detailDesc;
        Label statCount, statWeight, statStack;
        Button useButton, dropButton, dropAllButton;

        ItemCategory? filter;
        int selected = -1;
        bool isOpen;

        // 드래그 상태
        int pressIndex = -1, pressPointerId = -1, hoverIndex = -1;
        Vector2 pressPos;
        bool dragging;

        IVisualElementScheduledItem toastHide;

        public bool IsOpen => isOpen;

        void OnEnable()
        {
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
            if (inventory == null)
            {
                Debug.LogError("[InventoryUI] Inventory 컴포넌트를 찾지 못했습니다.");
                return;
            }

            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement.Q("inv-root");
            if (root == null)
            {
                Debug.LogError("[InventoryUI] UXML에서 'inv-root'를 찾지 못했습니다. UIDocument에 Inventory.uxml을 넣었는지 확인하세요.");
                return;
            }

            if (koreanFont != null)
                doc.rootVisualElement.style.unityFontDefinition =
                    new StyleFontDefinition(FontDefinition.FromFont(koreanFont));

            QueryElements();
            BuildTabs();
            BuildSlots();

            // 컴포넌트를 껐다 켜도 같은 UI에 버튼 동작이 두 번 걸리지 않게 한 번만 연결한다
            // (두 번 걸리면 '치료' 한 번에 붕대가 2개 줄어든다)
            if (boundRoot != root)
            {
                boundRoot = root;
                root.Q<Button>("sort-button").clicked += () => { inventory.Sort(); selected = -1; Refresh(); };
                root.Q<Button>("close-button").clicked += () => SetOpen(false);
                useButton.clicked += () => UseSlot(selected);
                dropButton.clicked += () => DropSlot(selected, 1);
                dropAllButton.clicked += () => DropSlot(selected, int.MaxValue);

                root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                root.RegisterCallback<PointerUpEvent>(OnPointerUp);
                root.RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag());
            }

            inventory.Changed += Refresh;

            SetOpen(startOpen);
            Refresh();
        }

        void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Refresh;
            slotViews.Clear();
            tabButtons.Clear();
            root = null;
        }

        void Update()
        {
            if (root == null) return;
            if (TogglePressed()) SetOpen(!isOpen);
            else if (isOpen && EscapePressed()) SetOpen(false);
        }

        // ───────────── 열기 / 닫기 ─────────────

        public void SetOpen(bool open)
        {
            isOpen = open;
            if (root == null) return;
            if (!open) CancelDrag();
            root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Toggle() => SetOpen(!isOpen);

        static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.iKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab)) return true;
#endif
            return false;
        }

        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        // ───────────── 구성 ─────────────

        void QueryElements()
        {
            grid = root.Q("grid");
            tabs = root.Q("tabs");
            slotCountLabel = root.Q<Label>("slot-count");

            weightBox = root.Q("weight");
            weightFill = root.Q("weight-fill");
            weightLabel = root.Q<Label>("weight-label");
            toast = root.Q<Label>("toast");

            ghost = root.Q("drag-ghost");
            ghostAbbr = root.Q<Label>("ghost-abbr");
            ghostCount = root.Q<Label>("ghost-count");
            ghostArt = root.Q("ghost-art");

            detailEmpty = root.Q<Label>("detail-empty");
            detailContent = root.Q("detail-content");
            detailIcon = root.Q("detail-icon");
            detailAbbr = root.Q<Label>("detail-abbr");
            detailArt = root.Q("detail-art");
            detailRarity = root.Q<Label>("detail-rarity");
            detailName = root.Q<Label>("detail-name");
            detailCategory = root.Q<Label>("detail-category");
            detailDesc = root.Q<Label>("detail-desc");
            statCount = root.Q<Label>("stat-count");
            statWeight = root.Q<Label>("stat-weight");
            statStack = root.Q<Label>("stat-stack");

            useButton = root.Q<Button>("use-button");
            dropButton = root.Q<Button>("drop-button");
            dropAllButton = root.Q<Button>("drop-all-button");
        }

        void BuildTabs()
        {
            tabs.Clear();
            tabButtons.Clear();
            AddTab("전체", null);
            foreach (ItemCategory c in Enum.GetValues(typeof(ItemCategory)))
                AddTab(ItemCategoryInfo.DisplayName(c), c);
        }

        void AddTab(string label, ItemCategory? category)
        {
            var b = new Button(() => { filter = category; Refresh(); })
            {
                text = label,
                userData = category
            };
            b.AddToClassList("tab");
            tabs.Add(b);
            tabButtons.Add(b);
        }

        void BuildSlots()
        {
            grid.Clear();
            slotViews.Clear();

            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = new VisualElement { userData = i };
                slot.AddToClassList("slot");

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("slot__icon");

                var abbr = new Label { pickingMode = PickingMode.Ignore };
                abbr.AddToClassList("slot__abbr");
                icon.Add(abbr);

                var count = new Label { pickingMode = PickingMode.Ignore };
                count.AddToClassList("slot__count");

                slot.Add(icon);
                slot.Add(count);

                int index = i;
                slot.RegisterCallback<PointerDownEvent>(e => OnSlotPointerDown(e, index));

                grid.Add(slot);
                slotViews.Add(new SlotView { root = slot, icon = icon, abbr = abbr, count = count });
            }
        }

        // ───────────── 갱신 ─────────────

        void Refresh()
        {
            if (root == null) return;

            int used = 0;
            for (int i = 0; i < slotViews.Count; i++)
            {
                var s = inventory.GetSlot(i);
                var v = slotViews[i];
                bool empty = s.IsEmpty;
                if (!empty) used++;

                v.root.EnableInClassList("slot--empty", empty);
                v.root.EnableInClassList("slot--has-icon", !empty && s.item.icon != null);
                v.root.EnableInClassList("slot--selected", i == selected && !empty);
                v.root.EnableInClassList("slot--dimmed",
                    filter.HasValue && (empty || s.item.category != filter.Value));

                if (empty)
                {
                    v.icon.style.backgroundImage = StyleKeyword.Null;
                    v.abbr.text = "";
                    v.count.text = "";
                    v.root.style.backgroundImage = StyleKeyword.Null;
                }
                else
                {
                    ApplyIcon(v.icon, v.abbr, s.item);
                    v.count.text = s.item.maxStack > 1 ? s.count.ToString() : "";
                    v.root.style.backgroundImage = RarityBackground(s.item);
                }
            }

            slotCountLabel.text = $"{used} / {slotViews.Count}칸";

            foreach (var b in tabButtons)
                b.EnableInClassList("tab--active", Equals(b.userData, filter));

            RefreshWeight();
            RefreshDetail();
        }

        void RefreshWeight()
        {
            float cur = inventory.CurrentWeight;
            float max = inventory.MaxWeight;
            float ratio = max > 0f ? cur / max : 0f;

            weightFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
            weightLabel.text = $"{cur:0.0} / {max:0.0} kg";
            weightBox.EnableInClassList("weight--warn", ratio >= 0.8f && ratio < 1f);
            weightBox.EnableInClassList("weight--full", ratio >= 1f);
        }

        void RefreshDetail()
        {
            var s = selected >= 0 ? inventory.GetSlot(selected) : null;
            bool has = s != null && !s.IsEmpty;

            detailEmpty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
            detailContent.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            if (!has) return;

            var item = s.item;
            ApplyIcon(detailArt, detailAbbr, item);
            detailIcon.style.backgroundImage = RarityBackground(item);

            var rarityColor = ItemRarityInfo.GetColor(item.rarity);
            detailName.text = item.displayName;
            detailName.style.color = item.rarity == ItemRarity.Common ? new StyleColor(StyleKeyword.Null) : rarityColor;
            detailRarity.text = ItemRarityInfo.DisplayName(item.rarity);
            detailRarity.style.backgroundColor = rarityColor;
            detailCategory.text = ItemCategoryInfo.DisplayName(item.category);
            detailDesc.text = item.description;

            statCount.text = item.maxStack > 1 ? $"{s.count}개" : "1개";
            statWeight.text = s.count > 1
                ? $"개당 {item.weight:0.##} kg · 합계 {item.weight * s.count:0.##} kg"
                : $"{item.weight:0.##} kg";
            statStack.text = item.maxStack > 1 ? $"{item.maxStack}개까지" : "겹치지 않음";

            useButton.style.display = item.usable ? DisplayStyle.Flex : DisplayStyle.None;
            useButton.text = string.IsNullOrEmpty(item.useLabel) ? "사용" : item.useLabel;
            dropButton.text = s.count > 1 ? "1개 버리기" : "버리기";
            dropAllButton.style.display = s.count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static StyleBackground RarityBackground(ItemData item)
        {
            var tex = ItemRarityInfo.GetBackground(item.rarity);
            return tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.Null);
        }

        static void ApplyIcon(VisualElement icon, Label abbr, ItemData item)
        {
            if (item.icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(item.icon);
                abbr.text = "";
            }
            else
            {
                icon.style.backgroundImage = StyleKeyword.Null;
                abbr.text = !string.IsNullOrEmpty(item.shortLabel)
                    ? item.shortLabel
                    : (item.displayName ?? "?").Substring(0, Mathf.Min(2, (item.displayName ?? "?").Length));
            }
        }

        // ───────────── 동작 ─────────────

        void Select(int index)
        {
            selected = index;
            Refresh();
        }

        void UseSlot(int index)
        {
            if (index < 0) return;
            var s = inventory.GetSlot(index);
            if (s.IsEmpty || !s.item.usable) return;

            string msg = $"{s.item.displayName} {(string.IsNullOrEmpty(s.item.useLabel) ? "사용" : s.item.useLabel)}";
            inventory.UseAt(index);
            ShowToast(msg);
        }

        void DropSlot(int index, int amount)
        {
            if (index < 0) return;
            var s = inventory.GetSlot(index);
            if (s.IsEmpty) return;

            int n = Mathf.Min(amount, s.count);
            string name = s.item.displayName;
            inventory.DropAt(index, n);
            ShowToast($"{name} {n}개 버림");
        }

        void ShowToast(string msg)
        {
            toast.text = msg;
            toast.AddToClassList("toast--show");
            toastHide?.Pause();
            toastHide = toast.schedule.Execute(() => toast.RemoveFromClassList("toast--show")).StartingIn(1600);
        }

        // ───────────── 드래그 & 클릭 ─────────────

        void OnSlotPointerDown(PointerDownEvent e, int index)
        {
            if (e.button == 1) // 우클릭: 바로 사용
            {
                Select(index);
                UseSlot(index);
                e.StopPropagation();
                return;
            }
            if (e.button != 0) return;

            pressIndex = index;
            pressPointerId = e.pointerId;
            pressPos = e.position;
            dragging = false;
            root.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (pressIndex < 0 || e.pointerId != pressPointerId) return;

            if (!dragging)
            {
                if (((Vector2)e.position - pressPos).sqrMagnitude < DragThreshold * DragThreshold) return;
                if (inventory.GetSlot(pressIndex).IsEmpty) return;
                BeginGhost(pressIndex);
            }

            MoveGhost(e.position);
            SetHover(FindSlotIndex(root.panel.Pick(e.position)));
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (pressIndex < 0 || e.pointerId != pressPointerId) return;

            int from = pressIndex;
            bool wasDragging = dragging;
            int to = wasDragging ? FindSlotIndex(root.panel.Pick(e.position)) : -1;

            ResetDragState();
            if (root.HasPointerCapture(e.pointerId)) root.ReleasePointer(e.pointerId);

            if (!wasDragging)
            {
                Select(from);
            }
            else if (to >= 0 && to != from)
            {
                inventory.Move(from, to);
                Select(inventory.GetSlot(to).IsEmpty ? -1 : to);
            }
        }

        void CancelDrag()
        {
            if (pressIndex < 0) return;
            if (root != null && pressPointerId >= 0 && root.HasPointerCapture(pressPointerId))
                root.ReleasePointer(pressPointerId);
            ResetDragState();
        }

        void ResetDragState()
        {
            if (pressIndex >= 0 && pressIndex < slotViews.Count)
                slotViews[pressIndex].root.RemoveFromClassList("slot--drag-source");
            pressIndex = -1;
            pressPointerId = -1;
            dragging = false;
            SetHover(-1);
            if (ghost != null) ghost.style.display = DisplayStyle.None;
        }

        void BeginGhost(int index)
        {
            dragging = true;
            var s = inventory.GetSlot(index);
            ApplyIcon(ghostArt, ghostAbbr, s.item);
            ghost.style.backgroundImage = RarityBackground(s.item);
            ghostCount.text = s.item.maxStack > 1 ? s.count.ToString() : "";
            ghost.style.display = DisplayStyle.Flex;
            slotViews[index].root.AddToClassList("slot--drag-source");
        }

        void MoveGhost(Vector2 panelPos)
        {
            var local = root.WorldToLocal(panelPos);
            ghost.style.left = local.x - GhostSize * 0.5f;
            ghost.style.top = local.y - GhostSize * 0.5f;
        }

        void SetHover(int index)
        {
            if (index == pressIndex) index = -1;
            if (index == hoverIndex) return;
            if (hoverIndex >= 0 && hoverIndex < slotViews.Count)
                slotViews[hoverIndex].root.RemoveFromClassList("slot--drop-target");
            hoverIndex = index;
            if (hoverIndex >= 0)
                slotViews[hoverIndex].root.AddToClassList("slot--drop-target");
        }

        static int FindSlotIndex(VisualElement ve)
        {
            while (ve != null)
            {
                if (ve.userData is int i && ve.ClassListContains("slot")) return i;
                ve = ve.parent;
            }
            return -1;
        }
    }
}
