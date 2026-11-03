using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 제작대·화로 창 (아이콘 카드형): 뒤 화면을 흐리게 깐 유리판 위에
// 왼쪽은 등급별로 묶은 조합법 카드 목록(휠로 스크롤), 오른쪽은 고른 조합법의 결과물·재료·제작 버튼을 보여 준다.
// 한 창을 두 가지로 쓴다: 제작대(조합, 전력 필요) / 화로(제련, 부싯돌과 라이터로 불을 붙여 Furnace.SmeltSeconds초 동안 녹인다)
// 조합 규칙(재료 확인·소비·완성품 위치)은 Workbench.cs, 녹이기 진행은 Furnace.cs
public partial class UIManager
{
    const float WorkbenchBlurInterval = 0.1f;
    // 오른쪽 상세 칸의 너비 (유리판 1440 - 왼쪽 512 - 오른쪽 여백 44)
    const float CraftDetailWidth = 884;

    static readonly Color SmeltColor = new Color(0.95f, 0.47f, 0.16f);

    GameObject workbenchRoot;
    RawImage workbenchBlur;
    RectTransform workbenchList, workbenchDetail;
    ScrollRect workbenchScroll;
    UnityEngine.UI.Text workbenchTitle, workbenchSub, workbenchWeight, workbenchNotice;
    CanvasGroup workbenchNoticeGroup;
    Transform workbenchSource;
    Recipe workbenchPick, furnacePick;
    float workbenchBlurTimer;

    // 화로 모드: 이 창이 지금 화로 창인가, 어느 화로인가, 녹이는 중 버튼의 진행 막대
    bool furnaceMode;
    Furnace furnace;
    RectTransform smeltFill;
    bool furnaceWasBusy;

    // 제작대 창이나 화로 창이 열려 있는가 (둘은 같은 창이다)
    public bool IsWorkbenchOpen => workbenchRoot != null && workbenchRoot.activeSelf;
    public bool IsFurnaceOpen => IsWorkbenchOpen && furnaceMode;

    // source: 제작 소리를 낼 위치 (제작대). 없으면 2D 소리
    public void OpenWorkbench(Transform source = null)
    {
        Tutorial.Notify("workbench");
        Tutorial.Hint(Tutorial.HintWorkbench, "재료가 모자란 조합법은 흐리게 보인다. '제작 가능만 보기'를 켜면 지금 만들 수 있는 것만 남는다.");
        OpenCrafting(false, source);
    }

    // source: 화로. 없으면 방공호의 화로(Furnace.Instance)를 쓴다
    public void OpenFurnace(Transform source = null) => OpenCrafting(true, source);

    void OpenCrafting(bool smelt, Transform source)
    {
        if (workbenchRoot == null) BuildWorkbench();
        ClosePanel(); // 제작대 창도 닫으므로 아래에서 다시 채운다
        furnaceMode = smelt;
        workbenchSource = source;
        furnace = null;
        if (smelt)
        {
            if (source != null) furnace = source.GetComponentInParent<Furnace>();
            if (furnace == null) furnace = Furnace.Instance;
        }
        furnaceWasBusy = WatchedFurnaceBusy;
        workbenchTitle.text = smelt ? "화로" : "제작대";
        workbenchRoot.SetActive(true);
        UpdateBlur(workbenchBlur);
        workbenchBlurTimer = WorkbenchBlurInterval;
        RefreshWorkbench();
        Canvas.ForceUpdateCanvases();
        workbenchScroll.verticalNormalizedPosition = 1;
    }

    // 녹이는 도중에 닫아도 화로는 계속 녹이고, 다 되면 알림으로 알려 준다
    public void CloseWorkbench()
    {
        if (workbenchRoot != null) workbenchRoot.SetActive(false);
        workbenchSource = null;
        furnace = null;
        smeltFill = null;
    }

    // UIManager.Update에서 제작대(화로) 창이 열려 있을 때 매 프레임 부른다
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

        // 화로: 진행 막대를 채우고, 다 녹으면 목록·재료 수·버튼을 다시 만든다
        // (제작대 창이어도 녹이기가 끝나면 철주괴 등 재료 수가 바뀌므로 다시 만든다)
        bool busy = WatchedFurnaceBusy;
        if (busy != furnaceWasBusy)
        {
            furnaceWasBusy = busy;
            RefreshWorkbench();
        }
        if (FurnaceBusy && smeltFill != null) smeltFill.anchorMax = new Vector2(furnace.Progress, 1);
    }

    // 지금 창에서 보여 줄 조합법 (제작대: Recipes / 화로: SmeltRecipes). 완성품 등급 순서로,
    // 같은 등급 안에서는 ItemDatabase에 넣은 순서대로 둔다 (목록을 등급별로 묶어 보여 준다)
    List<Recipe> CraftRecipes => (furnaceMode ? ItemDatabase.SmeltRecipes : ItemDatabase.Recipes)
        .OrderBy(r => (int)ItemDatabase.Get(r.ResultId).Rarity).ToList();

    // '제작 가능만 보기' (재료가 갖춰진 조합법만 목록에 남긴다). 창을 닫아도 기억한다
    bool craftableOnly;

    // 제작대와 화로는 고른 조합법을 따로 기억한다
    Recipe CraftSelection
    {
        get => furnaceMode ? furnacePick : workbenchPick;
        set { if (furnaceMode) furnacePick = value; else workbenchPick = value; }
    }

    bool FurnaceBusy => furnaceMode && furnace != null && furnace.IsSmelting;

    // 창이 지켜보는 화로가 녹이는 중인가 (화로 창: 연 화로 / 제작대 창: 방공호의 화로)
    bool WatchedFurnaceBusy
    {
        get
        {
            var f = furnaceMode ? furnace : Furnace.Instance;
            return f != null && f.IsSmelting;
        }
    }

    // 쓸 수 있는 상태인가: 제작대는 전력이 있어야 하고, 화로는 전력 없이 부싯돌과 라이터로 불을 붙인다
    bool StationReady(GameState s) => furnaceMode ? furnace != null && Furnace.HasLighter(s) : Workbench.IsPowered(s);

    // 쓸 수 없을 때 짧은 이유 (목록 카드) / 긴 이유 (상세)
    string StationProblem(bool longForm)
    {
        if (!furnaceMode) return longForm ? "전력이 없어 제작할 수 없다" : "전력 없음";
        if (furnace == null) return longForm ? "화로가 없어 녹일 수 없다" : "화로 없음";
        return longForm ? "부싯돌과 라이터가 없어 불을 붙일 수 없다" : "라이터 없음";
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
        Grime(glass);
        glass.gameObject.AddComponent<UIPopIn>();
        Place(glass, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 840));

        workbenchTitle = NewText(glass, "제작대", 38, TextAnchor.MiddleLeft, Text, FontBold);
        Place(workbenchTitle.rectTransform, new Vector2(0, 1), new Vector2(44, -24), new Vector2(170, 56));
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

    // 고른 조합법을 그대로 두고 목록과 상세를 다시 만든다 (제작한 뒤, 녹이기가 시작·끝난 뒤에도 부른다)
    void RefreshWorkbench()
    {
        if (!IsWorkbenchOpen) return;
        var s = GameManager.Instance.State;
        var recipes = CraftRecipes;
        if (craftableOnly) recipes = recipes.Where(r => Workbench.HasIngredients(s, r) && !Workbench.AlreadyInstalled(s, r)).ToList();
        bool ready = StationReady(s);
        if (furnaceMode)
        {
            workbenchSub.text = ready ? "부싯돌과 라이터로 불을 붙여 고철을 녹이고 고기를 굽는다. 전기는 필요 없다."
                : furnace == null ? "화로를 찾을 수 없다." : "부싯돌과 라이터가 있어야 불을 붙일 수 있다. 제작대에서 만들자.";
            workbenchSub.color = ready ? Dim : Danger;
        }
        else
        {
            workbenchSub.text = ready ? "재료는 창고와 소지품에서 함께 쓴다." : "전력이 없어 작업등과 공구를 쓸 수 없다. 발전기에 연료를 넣자.";
            workbenchSub.color = ready ? Dim : Danger;
        }
        // 간단히 보기: 문제가 있을 때(전력 없음·라이터 없음)만 안내하고, 무게는 오른쪽 상세 칸에만 보인다
        if (CompactItemCards && ready) workbenchSub.text = "";
        workbenchWeight.text = CompactItemCards ? "" : $"소지품  {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg";

        ClearChildren(workbenchList);
        ClearChildren(workbenchDetail);
        smeltFill = null;
        // 목록 맨 위의 토글: 켜면 노란 버튼
        var filterRow = Row(workbenchList, false);
        var filterButton = RowButton(filterRow, "제작 가능만 보기", () => { craftableOnly = !craftableOnly; RefreshWorkbench(); }, true, 190);
        if (craftableOnly) AccentButton(filterButton);
        if (recipes.Count == 0)
        {
            Line(workbenchList, craftableOnly ? "지금 재료로 만들 수 있는 것이 없다." : "조합법이 없습니다.", 22, Dim);
            return;
        }

        var selected = CraftSelection;
        if (selected == null || !recipes.Contains(selected)) selected = recipes[0];
        CraftSelection = selected;
        // 조합법은 등급 순서로 들어 있다. 등급이 바뀔 때마다 머리글을 단다
        for (int i = 0; i < recipes.Count; i++)
        {
            var r = recipes[i];
            var rarity = ItemDatabase.Get(r.ResultId).Rarity;
            if (i == 0 || ItemDatabase.Get(recipes[i - 1].ResultId).Rarity != rarity) RecipeGroupHeader(recipes, rarity, s, ready);
            RecipeCard(r, s, ready, r == selected, () => { CraftSelection = r; RefreshWorkbench(); });
        }
        RecipeDetail(selected, s, ready);
    }

    // 등급 머리글: 등급 이름(등급 색) + 그 등급에서 지금 만들 수 있는 수 / 전체 수
    void RecipeGroupHeader(List<Recipe> recipes, ItemRarity rarity, GameState s, bool ready)
    {
        int total = 0, can = 0;
        foreach (var r in recipes)
        {
            if (ItemDatabase.Get(r.ResultId).Rarity != rarity) continue;
            total++;
            if (ready && Workbench.HasIngredients(s, r) && !Workbench.AlreadyInstalled(s, r)) can++;
        }
        var rc = ItemRarityInfo.Color(rarity);
        var rt = NewRect("Group", workbenchList);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 40;
        var bar = NewRect("Bar", rt);
        Place(bar, new Vector2(0, 0), new Vector2(2, 6), new Vector2(4, 22));
        bar.gameObject.AddComponent<Image>().color = rc;
        var name = NewText(rt, ItemRarityInfo.Name(rarity), 20, TextAnchor.MiddleLeft, rc, FontBold);
        Place(name.rectTransform, new Vector2(0, 0), new Vector2(16, 2), new Vector2(160, 30));
        if (CompactItemCards) return;
        string canText = $"<color=#{ColorUtility.ToHtmlStringRGB(AddedColor)}>만들 수 있음 {can}</color>  ·  {total}개";
        var count = NewText(rt, can > 0 ? canText : $"{total}개", 16, TextAnchor.MiddleRight, Dim);
        Place(count.rectTransform, new Vector2(1, 0), new Vector2(-8, 2), new Vector2(240, 30));
    }

    // 왼쪽 목록의 조합법 카드: 등급 테두리 아이콘 + 이름(등급 색) + 제작 가능 여부. 만들 수 없으면 흐리게
    void RecipeCard(Recipe recipe, GameState s, bool ready, bool selected, Action onClick)
    {
        var def = ItemDatabase.Get(recipe.ResultId);
        var rc = ItemRarityInfo.Color(def.Rarity);
        bool enough = Workbench.HasIngredients(s, recipe);
        bool busy = FurnaceBusy;
        bool active = busy && furnace.CurrentRecipe == recipe;
        bool installed = Workbench.AlreadyInstalled(s, recipe);
        bool craftable = enough && ready && !busy && !installed;

        var rt = Box(workbenchList, "Recipe", Color.white, selected ? new Color(Accent.r, Accent.g, Accent.b, 0.75f) : new Color(1, 1, 1, 0.08f));
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
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 92;

        var body = NewRect("Body", rt);
        Stretch(body);
        var group = body.gameObject.AddComponent<CanvasGroup>();
        group.alpha = craftable || active ? 1f : 0.5f;
        group.blocksRaycasts = false;

        var slot = NewItemSlot(body, "Card", 7);
        Place(slot.Rect, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(68, 68));
        SetItemSlot(slot, def.Id);
        if (recipe.ResultCount > 1) SlotBadge(slot, "x" + recipe.ResultCount, 16);

        var name = NewText(body, def.Name, 24, TextAnchor.MiddleLeft, rc, FontSemiBold);
        if (CompactItemCards)
        {
            // 그림과 이름만. 만들 수 없는 것은 흐리게 (등급·재료·상태는 오른쪽 상세 칸에)
            Place(name.rectTransform, new Vector2(0, 0.5f), new Vector2(96, 0), new Vector2(330, 40));
            return;
        }
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(96, -14), new Vector2(210, 34));
        var tag = NewText(body, ItemRarityInfo.Name(def.Rarity), 15, TextAnchor.MiddleRight, rc, FontSemiBold);
        Place(tag.rectTransform, new Vector2(1, 1), new Vector2(-16, -18), new Vector2(90, 24));

        string state;
        Color stateColor;
        if (active) { state = "녹이는 중…"; stateColor = SmeltColor; }
        else if (installed) { state = "설치함"; stateColor = AddedColor; }
        else if (!ready) { state = StationProblem(false); stateColor = Danger; }
        else if (!enough) { state = "재료 부족"; stateColor = Dim; }
        else if (busy) { state = "화로 사용 중"; stateColor = Dim; }
        else { state = furnaceMode ? "녹일 수 있음" : "제작 가능"; stateColor = AddedColor; }
        var st = NewText(body, state, 17, TextAnchor.MiddleLeft, stateColor);
        Place(st.rectTransform, new Vector2(0, 1), new Vector2(96, -52), new Vector2(260, 26));
    }

    // 오른쪽 상세: 큰 결과물 카드, 이름·등급·설명, 재료 카드, 무게, 제작(녹이기) 버튼
    void RecipeDetail(Recipe recipe, GameState s, bool ready)
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
        string amount = Workbench.IsInstall(recipe) ? "방공호 거실 벽 앞에 설치한다 (한 번만)"
            : $"완성 수량  x{recipe.ResultCount}" + (furnaceMode ? $"   ·   녹이는 시간 {Furnace.SmeltSeconds:0}초" : "");
        var output = NewText(d, amount, 19, TextAnchor.MiddleLeft, Dim);
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
        var note = NewText(d, furnaceMode ? "부싯돌과 라이터로 불을 붙인다 · 창고 재료를 먼저 쓴다" : "창고에 있는 재료를 먼저 쓴다", 17, TextAnchor.MiddleRight, Dim);
        Place(note.rectTransform, new Vector2(1, 1), new Vector2(0, -240), new Vector2(420, 34));
        var grid = NewRect("Ingredients", d);
        grid.anchorMin = new Vector2(0, 1);
        grid.anchorMax = new Vector2(1, 1);
        grid.pivot = new Vector2(0.5f, 1);
        grid.anchoredPosition = new Vector2(0, -288);
        grid.sizeDelta = new Vector2(0, 214);
        // 재료가 4가지까지는 한 줄에 4장, 5가지면 카드를 좁혀 한 줄에 5장
        int columns = Mathf.Max(4, recipe.Ingredients.Count);
        float gap = columns > 4 ? 12 : 20;
        float cellW = (CraftDetailWidth - gap * (columns - 1)) / columns;
        var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(cellW, 214);
        gl.spacing = new Vector2(gap, 16);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = columns;
        foreach (var kv in recipe.Ingredients) IngredientCard(grid, kv.Key, kv.Value, s, cellW);

        // 아래: 완성품이 들어갈 곳과 무게 + 제작(녹이기) 버튼
        bool enough = Workbench.HasIngredients(s, recipe);
        bool busy = FurnaceBusy;
        bool active = busy && furnace.CurrentRecipe == recipe;
        bool install = Workbench.IsInstall(recipe);
        bool installed = Workbench.AlreadyInstalled(s, recipe);
        bool can = enough && ready && !busy && !installed;
        string status;
        Color statusColor;
        if (active) { status = "녹이는 중… 창을 닫아도 계속 녹는다"; statusColor = SmeltColor; }
        else if (busy) { status = "화로에서 다른 재료를 녹이는 중이다"; statusColor = Accent; }
        else if (installed) { status = "이미 방공호에 설치했다"; statusColor = AddedColor; }
        else if (!ready) { status = StationProblem(true); statusColor = Danger; }
        else if (!enough) { status = "재료 부족"; statusColor = Danger; }
        else if (install) { status = "만들면 방공호에 바로 설치한다"; statusColor = Text; }
        else if (Workbench.ResultFitsInHand(s, recipe)) { status = "완성품은 소지품에 넣는다"; statusColor = Text; }
        else { status = "무거워서 완성품은 창고에 넣는다"; statusColor = Accent; }
        float resultKg = install ? 0 : s.Carried.UnitWeight(def.Id) * recipe.ResultCount;
        var st = NewText(d, $"{status}\n<size=18><color=#a8a294>완성품 {resultKg:0.0} kg   ·   소지품 {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg</color></size>", 22, TextAnchor.MiddleLeft, statusColor, FontSemiBold);
        st.lineSpacing = 1.15f;
        Place(st.rectTransform, new Vector2(0, 0), new Vector2(0, 2), new Vector2(540, 72));

        if (busy)
        {
            SmeltingButton(d);
            return;
        }
        var btn = MakeButton(d, furnaceMode ? "녹이기" : install ? (installed ? "설치함" : "설치") : "제작", CraftSelected, can, 30);
        Place((RectTransform)btn.transform, new Vector2(1, 0), Vector2.zero, new Vector2(320, 76));
        if (can)
        {
            var colors = btn.colors;
            if (furnaceMode)
            {
                colors.normalColor = new Color(0.56f, 0.24f, 0.08f, 0.95f);
                colors.highlightedColor = new Color(0.72f, 0.33f, 0.12f, 1f);
                colors.pressedColor = new Color(0.85f, 0.42f, 0.16f, 1f);
            }
            else
            {
                colors.normalColor = new Color(0.5f, 0.37f, 0.14f, 0.95f);
                colors.highlightedColor = new Color(0.64f, 0.48f, 0.2f, 1f);
            }
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;
        }
    }

    // 녹이는 동안의 버튼: 눌리지 않고, 진행 막대가 차오른다 (UpdateWorkbench가 채운다)
    void SmeltingButton(RectTransform parent)
    {
        var btn = MakeButton(parent, "녹이는 중…", () => { }, false, 28);
        var rt = (RectTransform)btn.transform;
        Place(rt, new Vector2(1, 0), Vector2.zero, new Vector2(320, 76));
        var frame = rt.Find("Frame");
        if (frame != null) frame.GetComponent<Image>().color = new Color(SmeltColor.r, SmeltColor.g, SmeltColor.b, 0.7f);
        var label = rt.GetComponentInChildren<UnityEngine.UI.Text>();
        if (label != null) label.color = Text;

        // 바탕 위, 테두리·글씨 아래에 막대를 깐다
        var track = NewRect("Progress", rt);
        track.SetSiblingIndex(0);
        Stretch(track, 4, 4);
        smeltFill = NewRect("Fill", track);
        smeltFill.anchorMin = Vector2.zero;
        smeltFill.anchorMax = new Vector2(furnace != null ? furnace.Progress : 0f, 1);
        smeltFill.offsetMin = smeltFill.offsetMax = Vector2.zero;
        var img = smeltFill.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Fill;
        img.type = Image.Type.Sliced;
        img.color = new Color(SmeltColor.r, SmeltColor.g, SmeltColor.b, 0.62f);
        img.raycastTarget = false;
    }

    // 재료 카드: 아이콘, 이름(등급 색), 보유 / 필요 (모자라면 빨강), 창고·소지 개수. width: 카드 너비
    void IngredientCard(RectTransform parent, string id, int need, GameState s, float width)
    {
        var def = ItemDatabase.Get(id);
        int have = s.Total(id);
        bool lack = have < need;
        float textW = width - 14;
        // 장비 칸에 착용한 것은 재료로 세지 않는다 (벗어야 쓸 수 있다)
        bool worn = lack && (s.EquippedBackpack == id || s.EquippedArmor == id);
        var card = Box(parent, "Ingredient", new Color(1, 1, 1, 0.045f), lack ? new Color(Danger.r, Danger.g, Danger.b, 0.6f) : new Color(1, 1, 1, 0.12f));

        var slot = NewItemSlot(card, "Slot", 10);
        Place(slot.Rect, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(96, 96));
        SetItemSlot(slot, id);

        var name = NewText(card, def.Name, def.Name.Length > 7 ? 17 : 20, TextAnchor.MiddleCenter, ItemRarityInfo.Color(def.Rarity), FontSemiBold);
        Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(textW, 28));
        var count = NewText(card, $"{have} / {need}", 28, TextAnchor.MiddleCenter, lack ? Danger : Text, FontBold);
        Place(count.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(textW, 36));
        var split = NewText(card, worn ? "착용 중 · 벗어야 쓴다" : $"창고 {s.Storage.Count(id)}  ·  소지 {s.Carried.Count(id)}", 15, TextAnchor.MiddleCenter, worn ? Danger : Dim);
        Place(split.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(textW, 22));
    }

    // 제작 / 녹이기 버튼
    void CraftSelected()
    {
        var selected = CraftSelection;
        if (selected == null || !CraftRecipes.Contains(selected)) return;
        if (furnaceMode)
        {
            // 녹이는 동안에는 버튼이 막혀 있고, StartSmelt도 다시 시작하지 않는다
            if (furnace != null && furnace.StartSmelt(selected))
            {
                furnaceWasBusy = true;
                RefreshWorkbench();
            }
            return;
        }
        Vector3? at = workbenchSource != null ? workbenchSource.position : (Vector3?)null;
        if (Workbench.Craft(selected, at)) RefreshWorkbench();
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
