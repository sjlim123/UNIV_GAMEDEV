using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// 소지품 창 (Tab): 제작대 창과 같은 흐린 유리판 위에
// 왼쪽은 장비 칸(가방·방어구)과 소지 무게·최대 체력, 가운데는 소지품 카드 격자(휠로 스크롤),
// 오른쪽은 고른 것의 자세한 정보와 사용·장착·해제·버리기 버튼을 보여 준다.
// 장착 규칙은 GameState(Equip/Unequip), 장착 알림·소리는 PlayerHotbar(Equip/Unequip), 버리기는 PlayerHotbar.Drop
public partial class UIManager
{
    const float InventoryBlurInterval = 0.1f;
    const int InvColumns = 4;
    const float InvCellW = 118, InvCellH = 156, InvGap = 14;
    const float InvGridWidth = InvColumns * InvCellW + (InvColumns - 1) * InvGap;
    const float InvDoubleClick = 0.35f;

    GameObject invRoot;
    RawImage invBlur;
    RectTransform invEquip, invGrid, invDetail;
    ScrollRect invScroll;
    UnityEngine.UI.Text invCount, invEmpty, invNotice;
    CanvasGroup invNoticeGroup;
    float invBlurTimer;
    string invSignature;

    // 고른 것: 장비 칸(invSlot)이거나, 소지품 아이템(invItem = 목록의 invIndex번째)
    EquipSlot invSlot;
    string invItem;
    int invIndex;
    // 두 번 누르기 (바로 사용·장착·해제)
    string invClickKey;
    float invClickTime;

    // 소지품 창이 열려 있는가 (IsBlocking·Tab·Esc가 쓴다)
    public bool IsInventoryOpen => invRoot != null && invRoot.activeSelf;

    public void CloseInventory()
    {
        if (invRoot != null) invRoot.SetActive(false);
        invClickKey = null;
    }

    public void OpenInventory()
    {
        if (invRoot == null) BuildInventory();
        ClosePanel();
        // 열 때는 손에 든 아이템을 고른다 (없으면 첫 아이템)
        var items = GameManager.Instance.State.Carried.Sorted();
        var hb = PlayerHotbar.Instance;
        invSlot = EquipSlot.None;
        invItem = hb != null ? hb.SelectedId : null;
        invIndex = invItem != null ? Mathf.Max(0, items.FindIndex(kv => kv.Key == invItem)) : 0;
        invClickKey = null;

        invRoot.SetActive(true);
        CaptureInventoryBlur();
        invBlurTimer = InventoryBlurInterval;
        RefreshInventory();
        invScroll.velocity = Vector2.zero;
        invGrid.anchoredPosition = Vector2.zero;
        ScrollInventoryTo(invIndex);
    }

    // 뒤 배경을 찍는다. 카메라가 없어 찍지 못하면 블러를 끄고 어두운 막만 보인다 (빈 RawImage는 하얗게 보이므로)
    void CaptureInventoryBlur()
    {
        UpdateBlur(invBlur);
        invBlur.enabled = Camera.main != null && invBlur.texture != null;
    }

    // 창이 켜져 있는 동안 매 프레임 (InventoryWindowTicker가 부른다)
    void TickInventory()
    {
        var gm = GameManager.Instance;
        if (gm == null || !IsInventoryOpen) return;
        // 죽거나 끝 화면 같은 다른 창이 뜨면 닫는다 (창을 열어 둬도 시간은 흐른다)
        if (gm.IsGameOver || gm.IsTransitioning || IsPanelOpen || IsWorkbenchOpen || IsTruckOpen || IsCutscenePlaying || PlayerController.Instance == null)
        {
            CloseInventory();
            return;
        }
        // 일시정지 메뉴 등이 화면 크기가 바뀐 뒤 블러 텍스처를 새로 만들었으면 그것을 가리킨다 (지워진 텍스처는 하얗게 보이므로)
        if (invBlur.texture != blurResult)
        {
            invBlur.texture = blurResult;
            invBlur.enabled = blurResult != null && Camera.main != null;
        }
        // 뒤 배경은 0.1초마다 다시 찍는다 (일시정지 중에는 화면이 멈춰 있으니 찍지 않는다).
        // 창 밖에서 소지품이 바뀌었으면 (배터리 교체 등) 다시 그린다
        invBlurTimer -= Time.unscaledDeltaTime;
        if (invBlurTimer <= 0)
        {
            invBlurTimer = InventoryBlurInterval;
            if (!IsPaused) CaptureInventoryBlur();
            var s = gm.State;
            if (InventorySignature(s, s.Carried.Sorted()) != invSignature) RefreshInventory();
        }
        // 창이 HUD 알림을 가리므로 같은 알림을 창 위쪽에 보여 준다
        invNotice.text = toastText.text;
        invNoticeGroup.alpha = toastGroup.alpha;
    }

    void BuildInventory()
    {
        var r = NewRect("Inventory", root);
        Stretch(r);
        invRoot = r.gameObject;
        // 일시정지 메뉴 바로 아래에 둔다 (창을 연 채로 일시정지돼도 메뉴가 위에 보이게)
        r.SetSiblingIndex(pauseRoot.transform.GetSiblingIndex());
        r.gameObject.AddComponent<InventoryWindowTicker>().Tick = TickInventory;

        invBlur = BlurBackdrop(r, new Color(0.02f, 0.025f, 0.03f, 0.34f));

        var glass = Box(r, "Glass", GlassFill, GlassFrame);
        glass.gameObject.AddComponent<UIPopIn>();
        Place(glass, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 840));

        var title = NewText(glass, "소지품", 38, TextAnchor.MiddleLeft, Text, FontBold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(44, -24), new Vector2(170, 56));
        var sub = NewText(glass, "메고 입은 장비는 무게에 들지 않는다. 핫바에서 가방·방어구를 우클릭해도 장착된다.", 19, TextAnchor.MiddleLeft, Dim);
        Place(sub.rectTransform, new Vector2(0, 1), new Vector2(184, -34), new Vector2(900, 40));
        var close = MakeButton(glass, "닫기 (Esc)", CloseInventory, true);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-36, -28), new Vector2(170, 46));
        var line = NewRect("Line", glass);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, -100);
        line.sizeDelta = new Vector2(-72, 2);
        line.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);

        // 왼쪽: 장비 칸·무게·방어 (RefreshInventory에서 채운다)
        invEquip = NewRect("Equipment", glass);
        invEquip.anchorMin = new Vector2(0, 0);
        invEquip.anchorMax = new Vector2(0, 1);
        invEquip.pivot = new Vector2(0, 1);
        invEquip.offsetMin = new Vector2(36, 36);
        invEquip.offsetMax = new Vector2(416, -118);

        var vline = NewRect("VLine", glass);
        vline.anchorMin = new Vector2(0, 0);
        vline.anchorMax = new Vector2(0, 1);
        vline.offsetMin = new Vector2(438, 36);
        vline.offsetMax = new Vector2(440, -122);
        vline.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);

        // 가운데: 소지품 카드 격자 (휠로 스크롤)
        const float gridX = 462;
        var head = NewText(glass, "아이템", 22, TextAnchor.MiddleLeft, Accent, FontSemiBold);
        Place(head.rectTransform, new Vector2(0, 1), new Vector2(gridX, -118), new Vector2(200, 34));
        invCount = NewText(glass, "", 17, TextAnchor.MiddleRight, Dim);
        Place(invCount.rectTransform, new Vector2(0, 1), new Vector2(gridX + InvGridWidth - 300, -118), new Vector2(300, 34));

        var view = NewRect("GridView", glass);
        view.anchorMin = new Vector2(0, 0);
        view.anchorMax = new Vector2(0, 1);
        view.pivot = new Vector2(0, 1);
        view.offsetMin = new Vector2(gridX, 36);
        view.offsetMax = new Vector2(gridX + InvGridWidth, -160);
        view.gameObject.AddComponent<RectMask2D>();
        view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // 휠 스크롤을 받는 영역
        invGrid = NewRect("Content", view);
        invGrid.anchorMin = new Vector2(0, 1);
        invGrid.anchorMax = new Vector2(1, 1);
        invGrid.pivot = new Vector2(0.5f, 1);
        invGrid.sizeDelta = Vector2.zero;
        var gl = invGrid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(InvCellW, InvCellH);
        gl.spacing = new Vector2(InvGap, InvGap);
        gl.padding = new RectOffset(0, 0, 2, 8);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = InvColumns;
        invGrid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        invScroll = view.gameObject.AddComponent<ScrollRect>();
        invScroll.content = invGrid;
        invScroll.viewport = view;
        invScroll.horizontal = false;
        invScroll.movementType = ScrollRect.MovementType.Clamped;
        invScroll.scrollSensitivity = 40;
        invEmpty = NewText(view, "가진 것이 없다.\n<size=17>도시에서 주운 물건이 여기에 들어온다.</size>", 22, TextAnchor.MiddleCenter, Dim);
        invEmpty.lineSpacing = 1.3f;
        Place(invEmpty.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(InvGridWidth, 90));

        // 오른쪽: 고른 것의 자세한 정보 (유리 카드 안)
        var card = Box(glass, "DetailCard", new Color(1, 1, 1, 0.035f), new Color(1, 1, 1, 0.09f));
        card.anchorMin = Vector2.zero;
        card.anchorMax = Vector2.one;
        card.offsetMin = new Vector2(gridX + InvGridWidth + 24, 36);
        card.offsetMax = new Vector2(-36, -118);
        invDetail = NewRect("Detail", card);
        Stretch(invDetail, 22, 22);

        // HUD 알림을 대신 보여 주는 칸: 알림이 떠 있는 동안 제목 옆 안내 문구 자리를 덮는다
        var notice = Box(glass, "Notice", new Color(0.1f, 0.09f, 0.07f, 0.96f), new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        Place(notice, new Vector2(0, 1), new Vector2(176, -27), new Vector2(920, 48));
        invNoticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        invNoticeGroup.alpha = 0;
        invNoticeGroup.blocksRaycasts = false;
        invNotice = NewText(notice, "", 20, TextAnchor.MiddleLeft, Text);
        Stretch(invNotice.rectTransform, 16, 2);

        invRoot.SetActive(false);
    }

    // 고른 것을 그대로 두고 장비 칸·격자·상세를 다시 만든다 (사용·장착·해제·버리기 뒤에 부른다)
    void RefreshInventory()
    {
        if (!IsInventoryOpen) return;
        var s = GameManager.Instance.State;
        var items = s.Carried.Sorted();
        invSignature = InventorySignature(s, items);
        FixInventorySelection(items);

        ClearChildren(invEquip);
        ClearChildren(invGrid);
        ClearChildren(invDetail);

        EquipmentPanel(s);

        int total = 0;
        foreach (var kv in items) total += kv.Value;
        invCount.text = items.Count > 0 ? $"{items.Count}종  ·  {total}개" : "";
        invEmpty.gameObject.SetActive(items.Count == 0);

        var hb = PlayerHotbar.Instance;
        string held = hb != null ? hb.SelectedId : null;
        for (int i = 0; i < items.Count; i++)
            ItemCard(items[i].Key, items[i].Value, i, invSlot == EquipSlot.None && i == invIndex, items[i].Key == held);

        if (invSlot != EquipSlot.None) EquipDetail(s, invSlot);
        else if (invItem != null) ItemDetail(s, invItem, s.Carried.Count(invItem), invIndex, invItem == held);
        else
        {
            var t = NewText(invDetail, "아이템이나 장비 칸을 누르면\n여기에 자세히 보인다.", 20, TextAnchor.MiddleCenter, Dim);
            t.lineSpacing = 1.3f;
            Stretch(t.rectTransform);
        }
    }

    // 고른 아이템이 다 쓰이거나 버려져 없어지면 같은 자리(없으면 마지막)의 아이템을 고른다
    void FixInventorySelection(List<KeyValuePair<string, int>> items)
    {
        if (invSlot != EquipSlot.None) return; // 장비 칸은 비어도 고른 채로 둔다
        int i = invItem != null ? items.FindIndex(kv => kv.Key == invItem) : -1;
        if (i < 0 && items.Count > 0) i = Mathf.Clamp(invIndex, 0, items.Count - 1);
        invItem = i >= 0 ? items[i].Key : null;
        invIndex = Mathf.Max(i, 0);
    }

    // 소지품·장비·손에 든 것이 바뀌었는지 비교하는 글자
    static string InventorySignature(GameState s, List<KeyValuePair<string, int>> items)
    {
        var hb = PlayerHotbar.Instance;
        var sb = new StringBuilder();
        sb.Append(s.EquippedBackpack).Append('|').Append(s.EquippedArmor).Append('|').Append(hb != null ? hb.SelectedId : null).Append('|');
        foreach (var kv in items) sb.Append(kv.Key).Append(':').Append(kv.Value).Append(',');
        return sb.ToString();
    }

    // 고른 줄이 보이도록 격자를 내린다 (창을 열 때)
    void ScrollInventoryTo(int index)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(invGrid);
        float viewH = invScroll.viewport.rect.height;
        float maxY = Mathf.Max(0, invGrid.rect.height - viewH);
        float bottom = 2 + (index / InvColumns) * (InvCellH + InvGap) + InvCellH + 8;
        invGrid.anchoredPosition = new Vector2(invGrid.anchoredPosition.x, Mathf.Clamp(bottom - viewH, 0, maxY));
    }

    // ---------- 왼쪽: 장비 칸·무게·방어 ----------

    void EquipmentPanel(GameState s)
    {
        var p = invEquip;
        var b = GameBalance.Data;

        var head = NewText(p, "장비", 22, TextAnchor.MiddleLeft, Accent, FontSemiBold);
        Place(head.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(120, 34));
        var note = NewText(p, "장비는 무게 0 kg", 16, TextAnchor.MiddleRight, Dim);
        Place(note.rectTransform, new Vector2(1, 1), Vector2.zero, new Vector2(240, 34));

        EquipCard(p, s, EquipSlot.Backpack, -44);
        EquipCard(p, s, EquipSlot.Armor, -206);
        InvLine(p, -376);

        // 소지 무게: 2.4 / 10.0 kg + 막대
        float w = s.Carried.Weight, cap = s.CarryCapacity;
        float ratio = cap > 0 ? Mathf.Clamp01(w / cap) : 1f;
        bool heavy = ratio >= 0.9f;
        var wl = NewText(p, "소지 무게", 20, TextAnchor.MiddleLeft, Accent, FontSemiBold);
        Place(wl.rectTransform, new Vector2(0, 1), new Vector2(0, -392), new Vector2(160, 34));
        var wv = NewText(p, $"{w:0.0} / {cap:0.0} kg", 24, TextAnchor.MiddleRight, heavy ? Danger : Text, FontBold);
        Place(wv.rectTransform, new Vector2(1, 1), new Vector2(0, -392), new Vector2(220, 34));
        var bar = NewRect("WeightBar", p);
        bar.anchorMin = new Vector2(0, 1);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot = new Vector2(0.5f, 1);
        bar.anchoredPosition = new Vector2(0, -432);
        bar.sizeDelta = new Vector2(0, 12);
        var bg = bar.gameObject.AddComponent<Image>();
        bg.sprite = UIStyle.Fill;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 3;
        bg.color = new Color(1, 1, 1, 0.08f);
        bg.raycastTarget = false;
        if (ratio > 0)
        {
            var fill = NewRect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(ratio, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var img = fill.gameObject.AddComponent<Image>();
            img.sprite = UIStyle.Fill;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3;
            img.color = heavy ? Danger : new Color(Accent.r, Accent.g, Accent.b, 0.85f);
            img.raycastTarget = false;
        }
        string capText = s.EquippedBackpack != null
            ? $"기본 {b.carryCapacity:0.#} kg + {ItemDatabase.Name(s.EquippedBackpack)} {ItemDatabase.Get(s.EquippedBackpack).CarryBonus:0.#} kg"
            : $"기본 {b.carryCapacity:0.#} kg  ·  가방을 메면 {InvBonusRange(EquipSlot.Backpack)} kg";
        var wsub = NewText(p, capText, 16, TextAnchor.MiddleLeft, Dim);
        Place(wsub.rectTransform, new Vector2(0, 1), new Vector2(0, -450), new Vector2(380, 26));

        // 최대 체력: 방어구를 입으면 늘어난다
        bool armor = s.HasArmor;
        int bonus = armor ? ItemDatabase.Get(s.EquippedArmor).HealthBonus : 0;
        var al = NewText(p, "최대 체력", 20, TextAnchor.MiddleLeft, Accent, FontSemiBold);
        Place(al.rectTransform, new Vector2(0, 1), new Vector2(0, -494), new Vector2(160, 34));
        var av = NewText(p, armor ? $"{s.MaxHealth:0}  (+{bonus})" : $"{s.MaxHealth:0}", 22, TextAnchor.MiddleRight, armor ? AddedColor : Dim, FontSemiBold);
        Place(av.rectTransform, new Vector2(1, 1), new Vector2(0, -494), new Vector2(240, 34));
        var asub = NewText(p, armor ? $"기본 {b.playerMaxHealth:0} + {ItemDatabase.Name(s.EquippedArmor)} {bonus}" : $"방어구를 입으면 최대 체력 {InvBonusRange(EquipSlot.Armor)}", 16, TextAnchor.MiddleLeft, Dim);
        Place(asub.rectTransform, new Vector2(0, 1), new Vector2(0, -530), new Vector2(380, 26));

        // 아래: 조작 안내
        var hint = NewText(p, "아이템을 두 번 누르면 바로 사용·장착한다\nTab·Esc로 닫는다   ·   휠로 목록을 넘긴다", 16, TextAnchor.LowerLeft, new Color(Dim.r, Dim.g, Dim.b, 0.8f));
        hint.lineSpacing = 1.2f;
        Place(hint.rectTransform, new Vector2(0, 0), Vector2.zero, new Vector2(380, 52));
    }

    // 장비 칸 카드: 큰 아이템 칸 + 칸 이름·장비 이름(등급 색)·효과. 비어 있으면 흐린 윤곽만 보인다
    void EquipCard(RectTransform parent, GameState s, EquipSlot slot, float y)
    {
        string id = s.Equipped(slot);
        bool has = id != null;
        var def = ItemDatabase.Get(id);
        var rc = ItemRarityInfo.Color(def.Rarity);

        var rt = SelectCard(parent, slot + "Card", invSlot == slot, has ? new Color(rc.r, rc.g, rc.b, 0.3f) : new Color(1, 1, 1, 0.08f), () => ClickEquipSlot(slot));
        Place(rt, new Vector2(0, 1), new Vector2(0, y), new Vector2(380, 150));

        var v = NewItemSlot(rt, "Slot", 14);
        Place(v.Rect, new Vector2(0, 0.5f), new Vector2(13, 0), new Vector2(124, 124));
        SetItemSlot(v, id);
        if (!has) EmptyEquipSlot(v, slot, 16, 30);

        var label = NewText(rt, InvSlotName(slot) + " 칸", 16, TextAnchor.MiddleLeft, Dim);
        Place(label.rectTransform, new Vector2(0, 1), new Vector2(154, -14), new Vector2(150, 22));
        if (has)
        {
            var tag = NewText(rt, ItemRarityInfo.Name(def.Rarity), 15, TextAnchor.MiddleRight, rc, FontSemiBold);
            Place(tag.rectTransform, new Vector2(1, 1), new Vector2(-14, -14), new Vector2(90, 22));
        }
        var name = NewText(rt, has ? def.Name : "비어 있음", 26, TextAnchor.MiddleLeft, has ? rc : new Color(Dim.r, Dim.g, Dim.b, 0.75f), has ? FontBold : FontSemiBold);
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(154, -38), new Vector2(214, 36));

        string effect = (has ? "" : slot == EquipSlot.Backpack ? "메면 " : "입으면 ") + InvEquipEffect(slot, id);
        var eff = NewText(rt, effect, 17, TextAnchor.MiddleLeft, has ? AddedColor : Dim, has ? FontSemiBold : null);
        Place(eff.rectTransform, new Vector2(0, 1), new Vector2(154, -78), new Vector2(214, 24));

        // 아래 한 줄: 할 수 있는 일
        string spare = FirstCarriedFor(s, slot);
        string tip;
        Color tipColor = Dim;
        if (has) tip = "두 번 누르면 해제";
        else if (spare != null) { tip = $"소지품에 {ItemDatabase.Name(spare)} 있음 · 두 번 누르면 장착"; tipColor = Accent; }
        else tip = InvObtainHint(slot);
        var t = NewText(rt, tip, 15, TextAnchor.UpperLeft, tipColor);
        Place(t.rectTransform, new Vector2(0, 1), new Vector2(154, -106), new Vector2(214, 40));
    }

    // 빈 장비 칸: 흐린 윤곽선 + 흐린 장비 그림 + 칸 이름
    void EmptyEquipSlot(ItemSlotView v, EquipSlot slot, int labelSize, float pad)
    {
        var outline = NewRect("Outline", v.Rect).gameObject.AddComponent<Image>();
        Stretch(outline.rectTransform, 6, 6);
        outline.sprite = UIStyle.Frame;
        outline.type = Image.Type.Sliced;
        outline.color = new Color(1, 1, 1, 0.14f);
        outline.raycastTarget = false;

        string icon = InvSlotIcon(slot);
        if (icon != null)
        {
            var ghost = NewRect("Ghost", v.Rect).gameObject.AddComponent<Image>();
            ghost.rectTransform.anchorMin = Vector2.zero;
            ghost.rectTransform.anchorMax = Vector2.one;
            ghost.rectTransform.offsetMin = new Vector2(pad, pad + labelSize * 0.6f);
            ghost.rectTransform.offsetMax = new Vector2(-pad, -pad + labelSize * 0.6f);
            ghost.sprite = ItemIcons.Get(icon);
            ghost.preserveAspect = true;
            ghost.color = new Color(1, 1, 1, 0.13f);
            ghost.raycastTarget = false;
        }

        var label = NewText(v.Rect, InvSlotName(slot), labelSize, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.34f), FontSemiBold);
        Stretch(label.rectTransform, 4, 8);
    }

    // ---------- 가운데: 소지품 카드 ----------

    // 아이템 카드: 등급 테두리 아이콘 + 개수 + 핫바 번호 + 이름(등급 색) + 무게
    void ItemCard(string id, int count, int index, bool selected, bool held)
    {
        var def = ItemDatabase.Get(id);
        var rc = ItemRarityInfo.Color(def.Rarity);
        int k = index;
        var rt = SelectCard(invGrid, "Item", selected, new Color(rc.r, rc.g, rc.b, 0.22f), () => ClickInventoryItem(id, k));

        var slot = NewItemSlot(rt, "Slot", 8);
        Place(slot.Rect, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(94, 94));
        SetItemSlot(slot, id);
        if (count > 1) InvBadge(slot.Rect, "x" + count, 17, TextAnchor.LowerRight, Text);
        // 핫바 1~8번 칸에 들어가는 아이템은 번호를 단다 (손에 든 것은 금색)
        if (index < PlayerHotbar.Slots)
            InvBadge(slot.Rect, (index + 1).ToString(), 14, TextAnchor.UpperLeft, held ? Accent : new Color(1, 1, 1, 0.5f));

        var name = NewText(rt, def.Name, 18, TextAnchor.MiddleCenter, rc, FontSemiBold);
        // 긴 이름(부싯돌과 라이터 등)은 한 줄에 들어가게 글자를 줄인다
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 11;
        name.resizeTextMaxSize = 18;
        Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -108), new Vector2(InvCellW - 8, 24));
        var kg = NewText(rt, $"{GameManager.Instance.State.Carried.UnitWeight(id) * count:0.0} kg", 14, TextAnchor.MiddleCenter, Dim);
        Place(kg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(InvCellW - 8, 18));
    }

    // ---------- 오른쪽: 상세 ----------

    // 소지품 아이템 상세: 큰 칸, 이름·등급·분류·무게, 설명, 보유·쓰는 법·핫바, 사용/장착 + 버리기
    void ItemDetail(GameState s, string id, int count, int index, bool held)
    {
        var d = invDetail;
        var def = ItemDatabase.Get(id);
        var rc = ItemRarityInfo.Color(def.Rarity);

        var big = NewItemSlot(d, "Big", 20);
        Place(big.Rect, new Vector2(0.5f, 1), Vector2.zero, new Vector2(150, 150));
        SetItemSlot(big, id);
        if (count > 1) InvBadge(big.Rect, "x" + count, 26, TextAnchor.LowerRight, Text);

        DetailHeader(def.Name, rc, def.Rarity, $"{CategoryName(def.Category)}   ·   개당 {def.Weight:0.0} kg");
        DetailDescription(def.Description);

        float y = -356;
        DetailRow(ref y, "보유", $"{count}개   ·   {GameManager.Instance.State.Carried.UnitWeight(def.Id) * count:0.0} kg");
        DetailRow(ref y, "쓰는 법", PlayerHotbar.ActionHint(id));
        bool onBar = index < PlayerHotbar.Slots;
        DetailRow(ref y, "핫바", onBar ? $"{index + 1}번 칸" + (held ? "   ·   손에 들고 있다" : "") : "칸 밖 (8칸이 넘는다)", onBar ? Text : Dim);

        // 위: 사용 / 장착 (장비 칸에 다른 것이 있으면 바꿔 낀다)
        if (def.IsConsumable)
            DetailButton("사용", () => UseInventoryItem(id), true, true, new Vector2(0, 0), new Vector2(0, 62), new Vector2(360, 60));
        else if (def.Slot != EquipSlot.None)
        {
            string old = s.Equipped(def.Slot);
            DetailButton(old != null ? "바꿔 장착" : "장착", () => EquipInventoryItem(id), true, true, new Vector2(0, 0), new Vector2(0, 62), new Vector2(360, 60));
            if (old != null) DetailNote($"지금 {InvSlotName(def.Slot)} 칸의 {ColoredName(old)}{InvEun(ItemDatabase.Name(old))} 소지품으로 돌아온다", Dim);
        }

        // 아래: 버리기 (여러 개면 모두 버리기도)
        bool canDrop = PlayerHotbar.Instance != null;
        if (count > 1)
        {
            DetailButton("버리기", () => DropInventoryItem(id, 1), canDrop, false, new Vector2(0, 0), Vector2.zero, new Vector2(174, 52));
            DetailButton($"모두 버리기 ({count})", () => DropInventoryItem(id, count), canDrop, false, new Vector2(1, 0), Vector2.zero, new Vector2(174, 52));
        }
        else DetailButton("버리기", () => DropInventoryItem(id, 1), canDrop, false, new Vector2(0, 0), Vector2.zero, new Vector2(360, 52));
    }

    // 장비 칸 상세: 장착한 것(효과·해제·벗어서 버리기) 또는 빈 칸(얻는 법·소지품의 장비 장착)
    void EquipDetail(GameState s, EquipSlot slot)
    {
        var d = invDetail;
        var b = GameBalance.Data;
        string id = s.Equipped(slot);

        var big = NewItemSlot(d, "Big", 20);
        Place(big.Rect, new Vector2(0.5f, 1), Vector2.zero, new Vector2(150, 150));
        SetItemSlot(big, id);

        if (id == null)
        {
            EmptyEquipSlot(big, slot, 18, 30);
            DetailHeader(InvSlotName(slot) + " 칸", Text, null, "비어 있음");
            DetailDescription(slot == EquipSlot.Backpack
                ? $"가방을 메면 들 수 있는 무게가 늘어난다 ({InvSlotItems(slot)}). 멘 가방은 무게에 들지 않는다."
                : $"방어구를 입으면 최대 체력이 늘어난다 ({InvSlotItems(slot)}). 입은 방어구는 무게에 들지 않는다.");
            float ey = -356;
            DetailRow(ref ey, "얻는 법", InvObtainHint(slot));
            string spare = FirstCarriedFor(s, slot);
            if (spare != null)
                DetailButton($"{ItemDatabase.Name(spare)} 장착", () => EquipInventoryItem(spare), true, true, new Vector2(0, 0), new Vector2(0, 62), new Vector2(360, 60));
            else
                DetailNote($"소지품에 {(slot == EquipSlot.Backpack ? "멜" : "입을")} 장비가 없다", Dim, 80);
            return;
        }

        var def = ItemDatabase.Get(id);
        var rc = ItemRarityInfo.Color(def.Rarity);
        DetailHeader(def.Name, rc, def.Rarity, $"{CategoryName(def.Category)}   ·   {InvSlotName(slot)} 칸");
        DetailDescription(def.Description);

        float y = -356;
        DetailRow(ref y, "상태", slot == EquipSlot.Backpack ? "메고 있다" : "입고 있다", AddedColor);
        DetailRow(ref y, "효과", InvEquipEffect(slot, id), AddedColor);

        // 가방은 빼도 소지품을 감당할 수 있을 때만 뺀다
        string reason = PlayerHotbar.UnequipBlockReason(slot);
        bool can = reason == null;
        if (!can)
        {
            DetailRow(ref y, "해제", $"소지품을 {b.carryCapacity:0.#} kg 이하로 (지금 {s.Carried.Weight:0.0} kg)", Danger);
            DetailNote(reason, Danger);
        }
        DetailButton("해제", () => UnequipInventorySlot(slot), can, true, new Vector2(0, 0), new Vector2(0, 62), new Vector2(360, 60));
        DetailButton("벗어서 버리기", () => DropEquipped(slot), can && PlayerHotbar.Instance != null, false, new Vector2(0, 0), Vector2.zero, new Vector2(360, 52));
    }

    // 상세 위쪽: 이름(큰 글씨) + 등급 칩·분류 한 줄 + 구분선
    void DetailHeader(string name, Color nameColor, ItemRarity? rarity, string meta)
    {
        var d = invDetail;
        var n = NewText(d, name, 32, TextAnchor.MiddleCenter, nameColor, FontBold);
        Place(n.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(360, 44));

        var row = NewRect("Meta", d);
        Place(row, new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(360, 30));
        var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 12;
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = true;
        if (rarity.HasValue)
        {
            var rc = ItemRarityInfo.Color(rarity.Value);
            var chip = Box(row, "Rarity", new Color(rc.r, rc.g, rc.b, 0.16f), new Color(rc.r, rc.g, rc.b, 0.7f));
            chip.GetComponent<Image>().raycastTarget = false;
            var cle = chip.gameObject.AddComponent<LayoutElement>();
            cle.minWidth = cle.preferredWidth = 72;
            var ct = NewText(chip, ItemRarityInfo.Name(rarity.Value), 16, TextAnchor.MiddleCenter, rc, FontBold);
            Stretch(ct.rectTransform);
        }
        NewText(row, meta, 17, TextAnchor.MiddleLeft, Dim);

        InvLine(d, -254);
    }

    void DetailDescription(string text)
    {
        var desc = NewText(invDetail, text, 20, TextAnchor.UpperLeft, Text);
        desc.lineSpacing = 1.2f;
        Place(desc.rectTransform, new Vector2(0, 1), new Vector2(0, -270), new Vector2(360, 80));
    }

    // 상세의 한 줄 (이름 / 값)
    void DetailRow(ref float y, string key, string value, Color? color = null)
    {
        var k = NewText(invDetail, key, 17, TextAnchor.MiddleLeft, Dim);
        Place(k.rectTransform, new Vector2(0, 1), new Vector2(0, y), new Vector2(70, 28));
        var v = NewText(invDetail, value, 18, TextAnchor.MiddleLeft, color ?? Text);
        Place(v.rectTransform, new Vector2(0, 1), new Vector2(74, y), new Vector2(286, 28));
        y -= 30;
    }

    // 버튼 바로 위의 안내 한 줄 (예: 해제할 수 없는 이유)
    void DetailNote(string text, Color color, float bottom = 130)
    {
        var t = NewText(invDetail, text, 18, TextAnchor.MiddleCenter, color, FontSemiBold);
        Place(t.rectTransform, new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(360, 28));
    }

    // 상세 아래쪽 버튼. primary면 주요 동작 버튼(금색)으로 칠한다 (누를 수 없으면 그대로)
    Button DetailButton(string label, Action onClick, bool interactable, bool primary, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var btn = MakeButton(invDetail, label, onClick, interactable, primary ? 26 : 20);
        Place((RectTransform)btn.transform, anchor, pos, size);
        if (primary) AccentButton(btn);
        return btn;
    }

    // ---------- 누르기·동작 ----------

    // 첫 누름도 늘 기록한다 (고르지 않은 아이템을 두 번 눌러도 바로 사용·장착되게)
    void ClickInventoryItem(string id, int index)
    {
        bool twice = IsDoubleClick("item:" + id);
        invSlot = EquipSlot.None;
        invItem = id;
        invIndex = index;
        if (twice)
        {
            var def = ItemDatabase.Get(id);
            if (def.IsConsumable) { UseInventoryItem(id); return; }
            if (def.Slot != EquipSlot.None) { EquipInventoryItem(id); return; }
        }
        RefreshInventory();
    }

    void ClickEquipSlot(EquipSlot slot)
    {
        bool twice = IsDoubleClick("slot:" + slot);
        invSlot = slot;
        var s = GameManager.Instance.State;
        if (twice)
        {
            // 두 번 누르면 장착한 것은 해제, 빈 칸이면 소지품의 장비를 장착
            if (s.Equipped(slot) != null) { UnequipInventorySlot(slot); return; }
            string spare = FirstCarriedFor(s, slot);
            if (spare != null) { EquipInventoryItem(spare); return; }
        }
        RefreshInventory();
    }

    // 같은 것을 짧은 사이에 두 번 눌렀는가 (두 번째 누름을 쓰고 나면 다시 처음부터 센다)
    bool IsDoubleClick(string key)
    {
        float now = Time.unscaledTime;
        bool twice = invClickKey == key && now - invClickTime < InvDoubleClick;
        invClickKey = twice ? null : key;
        invClickTime = now;
        return twice;
    }

    void UseInventoryItem(string id)
    {
        var gm = GameManager.Instance;
        gm.UseItem(id, gm.State.Carried);
        RefreshInventory();
    }

    // 장착하면 그 장비 칸을 고른다 (바로 해제할 수 있게)
    void EquipInventoryItem(string id)
    {
        var slot = ItemDatabase.Get(id).Slot;
        if (PlayerHotbar.Equip(id)) invSlot = slot;
        RefreshInventory();
    }

    // 해제하면 소지품으로 돌아온 그 아이템을 고른다
    void UnequipInventorySlot(EquipSlot slot)
    {
        string id = GameManager.Instance.State.Equipped(slot);
        if (PlayerHotbar.Unequip(slot))
        {
            invSlot = EquipSlot.None;
            invItem = id;
        }
        RefreshInventory();
    }

    void DropInventoryItem(string id, int count)
    {
        var hb = PlayerHotbar.Instance;
        if (hb != null) hb.Drop(id, count);
        RefreshInventory();
    }

    // 장비 칸에서 벗어 바로 발 앞에 버린다 (해제할 수 없으면 버릴 수도 없다)
    void DropEquipped(EquipSlot slot)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        var hb = PlayerHotbar.Instance;
        string id = s.Equipped(slot);
        if (id == null || hb == null) return;
        if (!s.Unequip(slot)) ShowMessage(PlayerHotbar.UnequipBlockReason(slot));
        else
        {
            hb.Drop(id);
            ShowMessage($"{ColoredName(id)}{PlayerHotbar.Eul(ItemDatabase.Name(id))} 벗어서 버렸다.");
        }
        RefreshInventory();
    }

    // ---------- 도우미 ----------

    // 누를 수 있는 유리 카드: 고르면 어두운 금색 바탕 + 금색 테두리 (제작대 조합법 카드와 같은 모양)
    RectTransform SelectCard(Transform parent, string name, bool selected, Color frame, Action onClick)
    {
        var rt = Box(parent, name, Color.white, selected ? new Color(Accent.r, Accent.g, Accent.b, 0.8f) : frame);
        var b = rt.gameObject.AddComponent<Button>();
        UIPress.Attach(b, 1.015f);
        b.targetGraphic = rt.GetComponent<Image>();
        var colors = b.colors;
        colors.normalColor = selected ? new Color(0.36f, 0.27f, 0.1f, 0.7f) : new Color(1, 1, 1, 0.04f);
        colors.highlightedColor = selected ? colors.normalColor : new Color(1, 1, 1, 0.1f);
        colors.pressedColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(() => onClick());
        return rt;
    }

    // 칸 위의 작은 글자 (개수·핫바 번호)
    static UnityEngine.UI.Text InvBadge(RectTransform parent, string text, int size, TextAnchor anchor, Color color)
    {
        var t = NewText(parent, text, size, anchor, color, FontSemiBold);
        Stretch(t.rectTransform, 6, 3);
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        return t;
    }

    static void InvLine(RectTransform parent, float y)
    {
        var line = NewRect("Line", parent);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, y);
        line.sizeDelta = new Vector2(0, 2);
        var img = line.gameObject.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.1f);
        img.raycastTarget = false;
    }

    static string InvSlotName(EquipSlot slot) => slot == EquipSlot.Backpack ? "가방" : slot == EquipSlot.Armor ? "방어구" : "";

    // 장비의 효과 한 줄 (예: "들 수 있는 무게 +4 kg", "최대 체력 +40"). id가 없으면 그 칸 장비들의 범위
    static string InvEquipEffect(EquipSlot slot, string id)
    {
        var def = id != null ? ItemDatabase.Get(id) : null;
        if (slot == EquipSlot.Backpack) return def != null ? $"들 수 있는 무게 +{def.CarryBonus:0.#} kg" : $"들 수 있는 무게 {InvBonusRange(slot)} kg";
        return def != null ? $"최대 체력 +{def.HealthBonus}" : $"최대 체력 {InvBonusRange(slot)}";
    }

    static float InvBonus(ItemDef def) => def.Slot == EquipSlot.Backpack ? def.CarryBonus : def.HealthBonus;

    // 이 칸에 들어가는 장비들의 효과 범위 (예: "+2 ~ +8")
    static string InvBonusRange(EquipSlot slot)
    {
        float min = float.MaxValue, max = 0;
        foreach (var def in ItemDatabase.Items.Values)
        {
            if (def.Slot != slot) continue;
            min = Mathf.Min(min, InvBonus(def));
            max = Mathf.Max(max, InvBonus(def));
        }
        if (max <= 0) return "";
        return min >= max ? $"+{max:0.#}" : $"+{min:0.#} ~ +{max:0.#}";
    }

    // 이 칸에 들어가는 장비와 효과 (예: "낡은 가방 +2 · 일반 가방 +4 · 고급 가방 +8 kg")
    static string InvSlotItems(EquipSlot slot)
    {
        var parts = new List<string>();
        foreach (var def in ItemDatabase.Items.Values)
            if (def.Slot == slot) parts.Add($"{def.Name} +{InvBonus(def):0.#}");
        return string.Join(" · ", parts) + (slot == EquipSlot.Backpack ? " kg" : "");
    }

    // 받침이 있으면 "은", 없으면 "는"
    static string InvEun(string word)
    {
        string eul = PlayerHotbar.Eul(word);
        return eul == "을" ? "은" : eul == "를" ? "는" : "은(는)";
    }

    // 빈 칸에 흐리게 그릴 장비 그림 (그 칸에 들어가는 첫 아이템)
    static string InvSlotIcon(EquipSlot slot)
    {
        foreach (var def in ItemDatabase.Items.Values)
            if (def.Slot == slot) return def.Id;
        return null;
    }

    // 소지품에서 이 칸에 낄 수 있는 첫 장비
    static string FirstCarriedFor(GameState s, EquipSlot slot)
    {
        foreach (var kv in s.Carried.Sorted())
            if (ItemDatabase.Get(kv.Key).Slot == slot) return kv.Key;
        return null;
    }

    // 이 칸의 장비를 얻는 법 (예: "제작대: 가죽 2 + 밧줄 1")
    static string InvObtainHint(EquipSlot slot)
    {
        foreach (var r in ItemDatabase.Recipes)
        {
            if (ItemDatabase.Get(r.ResultId).Slot != slot) continue;
            var parts = new List<string>();
            foreach (var kv in r.Ingredients) parts.Add($"{ItemDatabase.Name(kv.Key)} {kv.Value}");
            return "제작대: " + string.Join(" + ", parts);
        }
        return "도시에서 찾아야 한다";
    }
}

// 창 루트에 붙여, 창이 켜져 있는 동안 매 프레임 Tick을 부른다 (코드에서 AddComponent로만 붙인다)
public class InventoryWindowTicker : MonoBehaviour
{
    public Action Tick;

    void Update() => Tick?.Invoke();
}
