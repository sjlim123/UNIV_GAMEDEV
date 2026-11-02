using System;
using UnityEngine;
using UnityEngine.UI;

// 제작대 창 (아이콘 카드형): 뒤 화면을 흐리게 깐 유리판 위에
// 왼쪽은 조합법 카드 목록(휠로 스크롤), 오른쪽은 고른 조합법의 결과물·재료·제작 버튼을 보여 준다.
// 조합 규칙(재료 확인·소비·완성품 위치)은 Workbench.cs
public partial class UIManager
{
    const float WorkbenchBlurInterval = 0.1f;

    GameObject workbenchRoot;
    RawImage workbenchBlur;
    RectTransform workbenchList, workbenchDetail;
    ScrollRect workbenchScroll;
    UnityEngine.UI.Text workbenchSub, workbenchWeight, workbenchNotice;
    CanvasGroup workbenchNoticeGroup;
    Transform workbenchSource;
    int workbenchSelected;
    float workbenchBlurTimer;

    public bool IsWorkbenchOpen => workbenchRoot != null && workbenchRoot.activeSelf;

    // source: 제작 소리를 낼 위치 (제작대). 없으면 2D 소리
    public void OpenWorkbench(Transform source = null)
    {
        if (workbenchRoot == null) BuildWorkbench();
        ClosePanel();
        workbenchSource = source;
        workbenchRoot.SetActive(true);
        UpdateBlur(workbenchBlur);
        workbenchBlurTimer = WorkbenchBlurInterval;
        RefreshWorkbench();
        Canvas.ForceUpdateCanvases();
        workbenchScroll.verticalNormalizedPosition = 1;
    }

    public void CloseWorkbench()
    {
        if (workbenchRoot != null) workbenchRoot.SetActive(false);
        workbenchSource = null;
    }

    // UIManager.Update에서 제작대 창이 열려 있을 때 매 프레임 부른다
    void UpdateWorkbench()
    {
        // 뒤 배경은 0.1초마다 다시 찍는다 (매 프레임 찍으면 화면을 두 번 그리게 된다)
        workbenchBlurTimer -= Time.unscaledDeltaTime;
        if (workbenchBlurTimer <= 0)
        {
            workbenchBlurTimer = WorkbenchBlurInterval;
            UpdateBlur(workbenchBlur);
        }
        // 창이 HUD 알림을 가리므로 같은 알림을 창 위쪽에 보여 준다
        workbenchNotice.text = toastText.text;
        workbenchNoticeGroup.alpha = toastGroup.alpha;
    }

    void BuildWorkbench()
    {
        var r = NewRect("Workbench", root);
        Stretch(r);
        workbenchRoot = r.gameObject;
        // 일시정지 메뉴 바로 아래에 둔다 (창을 연 채로 일시정지돼도 메뉴가 위에 보이게)
        r.SetSiblingIndex(pauseRoot.transform.GetSiblingIndex());

        workbenchBlur = BlurBackdrop(r, new Color(0.02f, 0.025f, 0.03f, 0.34f));

        var glass = Box(r, "Glass", GlassFill, GlassFrame);
        Place(glass, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 840));

        var title = NewText(glass, "제작대", 38, TextAnchor.MiddleLeft, Text, FontBold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(44, -24), new Vector2(170, 56));
        workbenchSub = NewText(glass, "", 19, TextAnchor.MiddleLeft, Dim);
        Place(workbenchSub.rectTransform, new Vector2(0, 1), new Vector2(184, -34), new Vector2(660, 40));
        workbenchWeight = NewText(glass, "", 20, TextAnchor.MiddleRight, Text, FontSemiBold);
        Place(workbenchWeight.rectTransform, new Vector2(1, 1), new Vector2(-230, -28), new Vector2(360, 46));
        var close = MakeButton(glass, "닫기 (Esc)", CloseWorkbench, true);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-36, -28), new Vector2(170, 46));
        var line = NewRect("Line", glass);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, -100);
        line.sizeDelta = new Vector2(-72, 2);
        line.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);

        // 왼쪽: 조합법 카드 목록 (휠로 스크롤)
        var view = NewRect("ListView", glass);
        view.anchorMin = new Vector2(0, 0);
        view.anchorMax = new Vector2(0, 1);
        view.pivot = new Vector2(0, 1);
        view.offsetMin = new Vector2(36, 36);
        view.offsetMax = new Vector2(456, -122);
        view.gameObject.AddComponent<RectMask2D>();
        view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // 휠 스크롤을 받는 영역
        workbenchList = NewRect("Content", view);
        workbenchList.anchorMin = new Vector2(0, 1);
        workbenchList.anchorMax = new Vector2(1, 1);
        workbenchList.pivot = new Vector2(0.5f, 1);
        workbenchList.sizeDelta = Vector2.zero;
        var vl = workbenchList.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 8;
        vl.padding = new RectOffset(0, 0, 2, 2);
        vl.childControlHeight = true;
        vl.childControlWidth = true;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;
        workbenchList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        workbenchScroll = view.gameObject.AddComponent<ScrollRect>();
        workbenchScroll.content = workbenchList;
        workbenchScroll.viewport = view;
        workbenchScroll.horizontal = false;
        workbenchScroll.movementType = ScrollRect.MovementType.Clamped;
        workbenchScroll.scrollSensitivity = 40;

        var vline = NewRect("VLine", glass);
        vline.anchorMin = new Vector2(0, 0);
        vline.anchorMax = new Vector2(0, 1);
        vline.offsetMin = new Vector2(480, 36);
        vline.offsetMax = new Vector2(482, -122);
        vline.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);

        // 오른쪽: 고른 조합법 상세 (RefreshWorkbench에서 채운다)
        workbenchDetail = NewRect("Detail", glass);
        workbenchDetail.anchorMin = Vector2.zero;
        workbenchDetail.anchorMax = Vector2.one;
        workbenchDetail.offsetMin = new Vector2(512, 36);
        workbenchDetail.offsetMax = new Vector2(-44, -124);

        // HUD 알림을 대신 보여 주는 칸: 알림이 떠 있는 동안 제목 옆 안내 문구 자리를 덮는다
        var notice = Box(glass, "Notice", new Color(0.1f, 0.09f, 0.07f, 0.96f), new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        Place(notice, new Vector2(0, 1), new Vector2(176, -27), new Vector2(668, 48));
        workbenchNoticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        workbenchNoticeGroup.alpha = 0;
        workbenchNoticeGroup.blocksRaycasts = false;
        workbenchNotice = NewText(notice, "", 20, TextAnchor.MiddleLeft, Text);
        Stretch(workbenchNotice.rectTransform, 16, 2);

        workbenchRoot.SetActive(false);
    }

    // 고른 조합법을 그대로 두고 목록과 상세를 다시 만든다 (제작한 뒤에도 부른다)
    void RefreshWorkbench()
    {
        if (!IsWorkbenchOpen) return;
        var s = GameManager.Instance.State;
        var recipes = ItemDatabase.Recipes;
        bool powered = Workbench.IsPowered(s);
        workbenchSub.text = powered ? "재료는 창고와 소지품에서 함께 쓴다." : "전력이 없어 작업등과 공구를 쓸 수 없다. 발전기에 연료를 넣자.";
        workbenchSub.color = powered ? Dim : Danger;
        workbenchWeight.text = $"소지품  {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg";

        ClearChildren(workbenchList);
        ClearChildren(workbenchDetail);
        if (recipes.Count == 0)
        {
            Line(workbenchList, "조합법이 없습니다.", 22, Dim);
            return;
        }

        workbenchSelected = Mathf.Clamp(workbenchSelected, 0, recipes.Count - 1);
        for (int i = 0; i < recipes.Count; i++)
        {
            int k = i;
            RecipeCard(recipes[i], s, powered, i == workbenchSelected, () => { workbenchSelected = k; RefreshWorkbench(); });
        }
        RecipeDetail(recipes[workbenchSelected], s, powered);
    }

    // 왼쪽 목록의 조합법 카드: 등급 테두리 아이콘 + 이름(등급 색) + 제작 가능 여부. 만들 수 없으면 흐리게
    void RecipeCard(Recipe recipe, GameState s, bool powered, bool selected, Action onClick)
    {
        var def = ItemDatabase.Get(recipe.ResultId);
        var rc = ItemRarityInfo.Color(def.Rarity);
        bool enough = Workbench.HasIngredients(s, recipe);
        bool craftable = enough && powered;

        var rt = Box(workbenchList, "Recipe", Color.white, selected ? new Color(Accent.r, Accent.g, Accent.b, 0.75f) : new Color(1, 1, 1, 0.08f));
        var b = rt.gameObject.AddComponent<Button>();
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
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 92;

        var body = NewRect("Body", rt);
        Stretch(body);
        var group = body.gameObject.AddComponent<CanvasGroup>();
        group.alpha = craftable ? 1f : 0.5f;
        group.blocksRaycasts = false;

        var slot = NewItemSlot(body, "Card", 7);
        Place(slot.Rect, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(68, 68));
        SetItemSlot(slot, def.Id);
        if (recipe.ResultCount > 1) SlotBadge(slot, "x" + recipe.ResultCount, 16);

        var name = NewText(body, def.Name, 24, TextAnchor.MiddleLeft, rc, FontSemiBold);
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(96, -14), new Vector2(210, 34));
        var tag = NewText(body, ItemRarityInfo.Name(def.Rarity), 15, TextAnchor.MiddleRight, rc, FontSemiBold);
        Place(tag.rectTransform, new Vector2(1, 1), new Vector2(-16, -18), new Vector2(90, 24));

        string state = craftable ? "제작 가능" : !powered ? "전력 없음" : "재료 부족";
        var st = NewText(body, state, 17, TextAnchor.MiddleLeft, craftable ? AddedColor : !powered ? Danger : Dim);
        Place(st.rectTransform, new Vector2(0, 1), new Vector2(96, -52), new Vector2(260, 26));
    }

    // 오른쪽 상세: 큰 결과물 카드, 이름·등급·설명, 재료 카드, 무게, 제작 버튼
    void RecipeDetail(Recipe recipe, GameState s, bool powered)
    {
        var d = workbenchDetail;
        var def = ItemDatabase.Get(recipe.ResultId);
        var rc = ItemRarityInfo.Color(def.Rarity);

        var big = NewItemSlot(d, "Result", 24);
        Place(big.Rect, new Vector2(0, 1), Vector2.zero, new Vector2(196, 196));
        SetItemSlot(big, def.Id);
        if (recipe.ResultCount > 1) SlotBadge(big, "x" + recipe.ResultCount, 30);

        var name = NewText(d, def.Name, 42, TextAnchor.MiddleLeft, rc, FontBold);
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(226, -2), new Vector2(640, 56));
        var chip = Box(d, "Rarity", new Color(rc.r, rc.g, rc.b, 0.16f), new Color(rc.r, rc.g, rc.b, 0.7f));
        Place(chip, new Vector2(0, 1), new Vector2(228, -66), new Vector2(84, 32));
        var chipText = NewText(chip, ItemRarityInfo.Name(def.Rarity), 17, TextAnchor.MiddleCenter, rc, FontBold);
        Stretch(chipText.rectTransform);
        var meta = NewText(d, $"{CategoryName(def.Category)}   ·   개당 {def.Weight:0.0} kg", 18, TextAnchor.MiddleLeft, Dim);
        Place(meta.rectTransform, new Vector2(0, 1), new Vector2(328, -66), new Vector2(520, 32));
        var desc = NewText(d, def.Description, 22, TextAnchor.UpperLeft, Text);
        desc.lineSpacing = 1.2f;
        Place(desc.rectTransform, new Vector2(0, 1), new Vector2(228, -116), new Vector2(640, 60));
        var output = NewText(d, $"완성 수량  x{recipe.ResultCount}", 19, TextAnchor.MiddleLeft, Dim);
        Place(output.rectTransform, new Vector2(0, 1), new Vector2(228, -164), new Vector2(640, 30));

        var line = NewRect("Line", d);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, -222);
        line.sizeDelta = new Vector2(0, 2);
        line.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);

        // 필요 재료 카드 (한 줄에 4장)
        var head = NewText(d, "필요 재료   <size=17><color=#a8a294>보유 / 필요</color></size>", 22, TextAnchor.MiddleLeft, Accent, FontSemiBold);
        Place(head.rectTransform, new Vector2(0, 1), new Vector2(0, -240), new Vector2(440, 34));
        var note = NewText(d, "창고에 있는 재료를 먼저 쓴다", 17, TextAnchor.MiddleRight, Dim);
        Place(note.rectTransform, new Vector2(1, 1), new Vector2(0, -240), new Vector2(420, 34));
        var grid = NewRect("Ingredients", d);
        grid.anchorMin = new Vector2(0, 1);
        grid.anchorMax = new Vector2(1, 1);
        grid.pivot = new Vector2(0.5f, 1);
        grid.anchoredPosition = new Vector2(0, -288);
        grid.sizeDelta = new Vector2(0, 214);
        var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(206, 214);
        gl.spacing = new Vector2(20, 16);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 4;
        foreach (var kv in recipe.Ingredients) IngredientCard(grid, kv.Key, kv.Value, s);

        // 아래: 완성품이 들어갈 곳과 무게 + 제작 버튼
        bool enough = Workbench.HasIngredients(s, recipe);
        bool can = enough && powered;
        string status;
        Color statusColor;
        if (!powered) { status = "전력이 없어 제작할 수 없다"; statusColor = Danger; }
        else if (!enough) { status = "재료가 모자란다"; statusColor = Danger; }
        else if (Workbench.ResultFitsInHand(s, recipe)) { status = "완성품은 소지품에 넣는다"; statusColor = Text; }
        else { status = "무거워서 완성품은 창고에 넣는다"; statusColor = Accent; }
        float resultKg = def.Weight * recipe.ResultCount;
        var st = NewText(d, $"{status}\n<size=18><color=#a8a294>완성품 {resultKg:0.0} kg   ·   소지품 {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg</color></size>", 22, TextAnchor.MiddleLeft, statusColor, FontSemiBold);
        st.lineSpacing = 1.15f;
        Place(st.rectTransform, new Vector2(0, 0), new Vector2(0, 2), new Vector2(540, 72));

        var btn = MakeButton(d, "제작", CraftSelected, can, 30);
        Place((RectTransform)btn.transform, new Vector2(1, 0), Vector2.zero, new Vector2(320, 76));
        if (can)
        {
            var colors = btn.colors;
            colors.normalColor = new Color(0.5f, 0.37f, 0.14f, 0.95f);
            colors.highlightedColor = new Color(0.64f, 0.48f, 0.2f, 1f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;
        }
    }

    // 재료 카드: 아이콘, 이름(등급 색), 보유 / 필요 (모자라면 빨강), 창고·소지 개수
    void IngredientCard(RectTransform parent, string id, int need, GameState s)
    {
        var def = ItemDatabase.Get(id);
        int have = s.Total(id);
        bool lack = have < need;
        var card = Box(parent, "Ingredient", new Color(1, 1, 1, 0.045f), lack ? new Color(Danger.r, Danger.g, Danger.b, 0.6f) : new Color(1, 1, 1, 0.12f));

        var slot = NewItemSlot(card, "Slot", 10);
        Place(slot.Rect, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(96, 96));
        SetItemSlot(slot, id);

        var name = NewText(card, def.Name, 20, TextAnchor.MiddleCenter, ItemRarityInfo.Color(def.Rarity), FontSemiBold);
        Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(190, 28));
        var count = NewText(card, $"{have} / {need}", 28, TextAnchor.MiddleCenter, lack ? Danger : Text, FontBold);
        Place(count.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(190, 36));
        var split = NewText(card, $"창고 {s.Storage.Count(id)}  ·  소지 {s.Carried.Count(id)}", 15, TextAnchor.MiddleCenter, Dim);
        Place(split.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(196, 22));
    }

    void CraftSelected()
    {
        var recipes = ItemDatabase.Recipes;
        if (workbenchSelected < 0 || workbenchSelected >= recipes.Count) return;
        Vector3? at = workbenchSource != null ? workbenchSource.position : (Vector3?)null;
        if (Workbench.Craft(recipes[workbenchSelected], at)) RefreshWorkbench();
    }

    // 칸 오른쪽 아래 개수 표시 (예: x2)
    static UnityEngine.UI.Text SlotBadge(ItemSlotView v, string text, int size)
    {
        var t = NewText(v.Rect, text, size, TextAnchor.LowerRight, Text, FontSemiBold);
        Stretch(t.rectTransform, 6, 3);
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        return t;
    }
}
