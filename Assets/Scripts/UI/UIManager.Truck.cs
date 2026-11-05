using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 도시 트럭 정비소 창 (TruckSite가 연다). 제작대 창과 같은 흐린 유리판 위에
// 위쪽은 전체 수리 진행 막대, 가운데는 트럭 재료 카드(아이콘·넣은 개수 / 필요 개수·소지 개수·넣기 버튼),
// 아래는 저장 안내와 '모두 넣기' 버튼 (다 고치면 '트럭 타고 탈출하기'). 넣는 규칙·소리는 TruckSite.cs
public partial class UIManager
{
    const float TruckBlurInterval = 0.1f;

    GameObject truckRoot;
    RawImage truckBlur;
    RectTransform truckGrid, truckBottom, truckProgressFill;
    Image truckProgressImage;
    UnityEngine.UI.Text truckSub, truckWeight, truckProgressText, truckNotice;
    CanvasGroup truckNoticeGroup;
    TruckSite truckSite;
    float truckBlurTimer;
    int truckTickFrame = -1;

    public bool IsTruckOpen => truckRoot != null && truckRoot.activeSelf;

    // HUD 알림이 떠 있는가 (TruckSite가 안내를 다른 알림과 겹치지 않게 띄울 때 본다)
    public bool IsShowingMessage => toastTimer > 0;

    public void OpenTruck(TruckSite site)
    {
        Tutorial.Notify("truck");
        if (truckRoot == null) BuildTruck();
        ClosePanel();
        truckSite = site;
        truckRoot.SetActive(true);
        CaptureTruckBlur();
        truckBlurTimer = TruckBlurInterval;
        RefreshTruck();
    }

    // 뒤 배경을 찍는다. 카메라가 없어 찍지 못하면 블러를 끄고 어두운 막만 보인다 (빈 RawImage는 하얗게 보이므로)
    void CaptureTruckBlur()
    {
        UpdateBlur(truckBlur);
        truckBlur.enabled = Camera.main != null && truckBlur.texture != null;
    }

    public void CloseTruck()
    {
        if (truckRoot != null) truckRoot.SetActive(false);
        truckSite = null;
    }

    // 창이 열려 있는 동안 매 프레임 부른다 (TruckSite.Update). 같은 프레임에 여러 번 불려도 한 번만 한다
    public void UpdateTruck()
    {
        if (!IsTruckOpen || truckTickFrame == Time.frameCount) return;
        truckTickFrame = Time.frameCount;
        // 죽거나 끝 화면 같은 다른 창이 뜨면 닫는다 (창을 열어 둬도 도시의 시간은 흐른다)
        var gm = GameManager.Instance;
        if (gm.IsGameOver || gm.IsTransitioning || IsPanelOpen || PlayerController.Instance == null)
        {
            CloseTruck();
            return;
        }
        // 뒤 배경은 0.1초마다 다시 찍는다 (좀비가 다가오는 것이 흐릿하게 보인다). 일시정지 중에는 화면이 멈춰 있으니 찍지 않는다
        truckBlurTimer -= Time.unscaledDeltaTime;
        if (truckBlurTimer <= 0 && !IsPaused)
        {
            truckBlurTimer = TruckBlurInterval;
            CaptureTruckBlur();
        }
        // 창이 HUD 알림을 가리므로 같은 알림을 창 위쪽에 보여 준다
        truckNotice.text = toastText.text;
        truckNoticeGroup.alpha = toastGroup.alpha;
    }

    void BuildTruck()
    {
        var r = NewRect("Truck", root);
        Stretch(r);
        truckRoot = r.gameObject;
        // 일시정지 메뉴 바로 아래에 둔다 (창을 연 채로 일시정지돼도 메뉴가 위에 보이게)
        r.SetSiblingIndex(pauseRoot.transform.GetSiblingIndex());

        truckBlur = BlurBackdrop(r, new Color(0.02f, 0.025f, 0.03f, 0.34f));

        var glass = PlateWindow(r, "Glass"); // 메인 메뉴와 같은 더러운 강판 (2026-11-05 저녁)
        glass.gameObject.AddComponent<UIPopIn>();
        Place(glass, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 840));

        RedTitleBar(glass, new Vector2(30, -28), 48);
        var title = NewText(glass, "트럭 정비소", 38, TextAnchor.MiddleLeft, Text, FontTitle);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(44, -24), new Vector2(230, 56));
        truckSub = NewText(glass, "", 19, TextAnchor.MiddleLeft, Dim);
        Place(truckSub.rectTransform, new Vector2(0, 1), new Vector2(264, -34), new Vector2(580, 40));
        truckWeight = NewText(glass, "", 20, TextAnchor.MiddleRight, Text, FontSemiBold);
        Place(truckWeight.rectTransform, new Vector2(1, 1), new Vector2(-230, -28), new Vector2(360, 46));
        var close = PlateButton(glass, "닫기", "ESC", CloseTruck);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-36, -28), new Vector2(150, 44));
        var line = NewRect("Line", glass);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, -100);
        line.sizeDelta = new Vector2(-72, 2);
        line.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);

        // 전체 수리 진행 막대
        var head = NewText(glass, "수리 진행", 22, TextAnchor.MiddleLeft, Accent, FontSemiBold);
        Place(head.rectTransform, new Vector2(0, 1), new Vector2(44, -118), new Vector2(300, 34));
        truckProgressText = NewText(glass, "", 22, TextAnchor.MiddleRight, Text, FontSemiBold);
        Place(truckProgressText.rectTransform, new Vector2(1, 1), new Vector2(-44, -118), new Vector2(600, 34));
        var bar = NewRect("ProgressBar", glass);
        bar.anchorMin = new Vector2(0, 1);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot = new Vector2(0.5f, 1);
        bar.anchoredPosition = new Vector2(0, -160);
        bar.sizeDelta = new Vector2(-88, 16);
        var bgImg = bar.gameObject.AddComponent<Image>();
        bgImg.sprite = UIStyle.Fill;
        bgImg.type = Image.Type.Sliced;
        bgImg.pixelsPerUnitMultiplier = 2;
        bgImg.color = new Color(1, 1, 1, 0.08f);
        truckProgressFill = NewRect("Fill", bar);
        truckProgressFill.anchorMin = Vector2.zero;
        truckProgressFill.anchorMax = Vector2.one;
        truckProgressFill.offsetMin = truckProgressFill.offsetMax = Vector2.zero;
        truckProgressImage = truckProgressFill.gameObject.AddComponent<Image>();
        truckProgressImage.sprite = UIStyle.Fill;
        truckProgressImage.type = Image.Type.Sliced;
        truckProgressImage.pixelsPerUnitMultiplier = 2;
        truckProgressImage.color = Accent;

        // 트럭 재료 카드 (한 줄에 3장)
        truckGrid = NewRect("Parts", glass);
        truckGrid.anchorMin = new Vector2(0, 1);
        truckGrid.anchorMax = new Vector2(1, 1);
        truckGrid.pivot = new Vector2(0.5f, 1);
        truckGrid.anchoredPosition = new Vector2(0, -200);
        truckGrid.sizeDelta = new Vector2(-72, 452);
        var gl = truckGrid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(440, 216);
        gl.spacing = new Vector2(24, 20);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 3;

        // 아래: 저장 안내 + 버튼 (RefreshTruck에서 채운다)
        truckBottom = NewRect("Bottom", glass);
        truckBottom.anchorMin = new Vector2(0, 0);
        truckBottom.anchorMax = new Vector2(1, 0);
        truckBottom.pivot = new Vector2(0.5f, 0);
        truckBottom.anchoredPosition = new Vector2(0, 32);
        truckBottom.sizeDelta = new Vector2(-88, 96);

        // HUD 알림을 대신 보여 주는 칸: 알림이 떠 있는 동안 제목 옆 안내 문구 자리를 덮는다
        var notice = Box(glass, "Notice", new Color(0.1f, 0.09f, 0.07f, 0.96f), new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        Place(notice, new Vector2(0, 1), new Vector2(256, -27), new Vector2(592, 48));
        truckNoticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        truckNoticeGroup.alpha = 0;
        truckNoticeGroup.blocksRaycasts = false;
        truckNotice = NewText(notice, "", 20, TextAnchor.MiddleLeft, Text);
        Stretch(truckNotice.rectTransform, 16, 2);

        truckRoot.SetActive(false);
    }

    // 진행 막대·카드·아래 버튼을 지금 상태로 다시 만든다 (넣은 뒤에도 부른다)
    void RefreshTruck()
    {
        if (!IsTruckOpen) return;
        var s = GameManager.Instance.State;
        bool complete = s.TruckComplete;
        TruckSite.Progress(s, out int have, out int need);
        float frac = need > 0 ? Mathf.Clamp01(have / (float)need) : 1f;

        truckSub.text = complete ? "트럭을 다 고쳤다. 이제 시동을 걸 수 있다." : "부품을 모아 트럭을 고치면 도시를 빠져나갈 수 있다.";
        truckSub.color = complete ? AddedColor : Dim;
        truckWeight.text = $"소지품  {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg";
        truckProgressFill.anchorMax = new Vector2(frac, 1);
        truckProgressImage.enabled = have > 0;
        truckProgressImage.color = complete ? AddedColor : Accent;
        truckProgressText.text = $"부품 {have} / {need}   <color=#a8a294>·</color>   {Mathf.FloorToInt(frac * 100 + 0.001f)}%";
        truckProgressText.color = complete ? AddedColor : Text;

        ClearChildren(truckGrid);
        foreach (var kv in ItemDatabase.TruckParts) TruckCard(kv.Key, kv.Value, s);

        // 아래 왼쪽: 저장 안내
        ClearChildren(truckBottom);
        var note = NewText(truckBottom, "넣은 재료는 방공호로 돌아가면 저장된다\n<size=17><color=#a8a294>돌아가기 전에 게임을 끄면 이번 탐험에서 넣은 재료는 사라진다   ·   창을 열어 둬도 시간은 흐른다</color></size>", 22, TextAnchor.MiddleLeft, Text, FontSemiBold);
        note.lineSpacing = 1.2f;
        Place(note.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(860, 90));

        // 아래 오른쪽: 다 고쳤으면 탈출 버튼, 아니면 가진 재료 모두 넣기
        bool any = false;
        foreach (var kv in ItemDatabase.TruckParts)
            if (s.TruckNeed(kv.Key) > 0 && s.Carried.Count(kv.Key) > 0) any = true;
        Button btn;
        if (complete)
        {
            btn = MakeButton(truckBottom, "트럭 타고 탈출하기", EscapeByTruck, true, 32);
            Place((RectTransform)btn.transform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(440, 88));
        }
        else
        {
            btn = MakeButton(truckBottom, "가진 재료 모두 넣기", DeliverAllTruckParts, any, 26);
            Place((RectTransform)btn.transform, new Vector2(1, 0.5f), Vector2.zero, new Vector2(340, 76));
        }
        if (complete || any)
        {
            var colors = btn.colors;
            colors.normalColor = complete ? new Color(0.22f, 0.45f, 0.24f, 0.95f) : new Color(0.5f, 0.37f, 0.14f, 0.95f);
            colors.highlightedColor = complete ? new Color(0.3f, 0.6f, 0.32f, 1f) : new Color(0.64f, 0.48f, 0.2f, 1f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;
        }
    }

    // 재료 카드: 아이콘, 이름(등급 색), 얻는 곳, 넣은 개수 / 필요 개수 (다 넣으면 초록), 작은 진행 막대, 소지 개수, 넣기 버튼
    void TruckCard(string id, int need, GameState s)
    {
        var def = ItemDatabase.Get(id);
        var rc = ItemRarityInfo.Color(def.Rarity);
        int have = Mathf.Min(s.Truck.Count(id), need);
        int carried = s.Carried.Count(id);
        bool done = have >= need;
        bool can = !done && carried > 0;
        var frame = done ? new Color(AddedColor.r, AddedColor.g, AddedColor.b, 0.6f)
                  : can ? new Color(Accent.r, Accent.g, Accent.b, 0.55f) : new Color(1, 1, 1, 0.12f);
        var card = Box(truckGrid, "Part", new Color(1, 1, 1, done ? 0.07f : 0.045f), frame);

        var slot = NewItemSlot(card, "Slot", 12);
        Place(slot.Rect, new Vector2(0, 1), new Vector2(18, -18), new Vector2(110, 110));
        SetItemSlot(slot, id);

        var name = NewText(card, def.Name, 26, TextAnchor.MiddleLeft, rc, FontSemiBold);
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(146, -14), new Vector2(200, 36));
        var tag = NewText(card, ItemRarityInfo.Name(def.Rarity), 15, TextAnchor.MiddleRight, rc, FontSemiBold);
        Place(tag.rectTransform, new Vector2(1, 1), new Vector2(-18, -20), new Vector2(80, 24));
        var source = NewText(card, TruckPartSource(id), 16, TextAnchor.MiddleLeft, Dim);
        Place(source.rectTransform, new Vector2(0, 1), new Vector2(146, -50), new Vector2(276, 24));

        var count = NewText(card, $"{have} / {need}", 38, TextAnchor.MiddleLeft, done ? AddedColor : Text, FontBold);
        Place(count.rectTransform, new Vector2(0, 1), new Vector2(146, -76), new Vector2(276, 46));

        // 작은 진행 막대
        var bar = NewRect("Bar", card);
        Place(bar, new Vector2(0, 1), new Vector2(146, -128), new Vector2(276, 8));
        var bg = bar.gameObject.AddComponent<Image>();
        bg.sprite = UIStyle.Fill;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 3;
        bg.color = new Color(1, 1, 1, 0.08f);
        if (have > 0)
        {
            var fill = NewRect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(need > 0 ? have / (float)need : 1f, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var img = fill.gameObject.AddComponent<Image>();
            img.sprite = UIStyle.Fill;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3;
            img.color = done ? AddedColor : Accent;
        }

        // 아래 줄: 소지 개수 + 넣기 버튼 (다 넣었으면 '완료')
        string carriedText = done ? "필요한 만큼 다 넣었다" : carried > 0 ? $"소지 <color=#f9c252>{carried}개</color>" : "소지 0개";
        var ct = NewText(card, carriedText, 19, TextAnchor.MiddleLeft, done || carried == 0 ? Dim : Text);
        Place(ct.rectTransform, new Vector2(0, 0), new Vector2(20, 14), new Vector2(240, 50));
        if (done)
        {
            var chip = Box(card, "Done", new Color(AddedColor.r, AddedColor.g, AddedColor.b, 0.16f), new Color(AddedColor.r, AddedColor.g, AddedColor.b, 0.7f));
            Place(chip, new Vector2(1, 0), new Vector2(-16, 18), new Vector2(110, 42));
            var chipText = NewText(chip, "완료", 20, TextAnchor.MiddleCenter, AddedColor, FontBold);
            Stretch(chipText.rectTransform);
        }
        else
        {
            int put = Mathf.Min(carried, need - have);
            var btn = MakeButton(card, can ? $"넣기  x{put}" : "넣기", () => DeliverTruckPart(id), can, 22);
            Place((RectTransform)btn.transform, new Vector2(1, 0), new Vector2(-16, 14), new Vector2(150, 50));
            if (can)
            {
                var colors = btn.colors;
                colors.normalColor = new Color(0.5f, 0.37f, 0.14f, 0.95f);
                colors.highlightedColor = new Color(0.64f, 0.48f, 0.2f, 1f);
                colors.selectedColor = colors.normalColor;
                btn.colors = colors;
            }
        }
    }

    // 얻는 곳 안내 (예: 얻는 곳  화로 · 도시). 조합법·제련·전리품·거래 목록에서 찾는다
    static string TruckPartSource(string id)
    {
        var where = new List<string>();
        if (ItemDatabase.SmeltRecipes.Exists(r => r.ResultId == id)) where.Add("화로");
        if (ItemDatabase.Recipes.Exists(r => r.ResultId == id)) where.Add("제작대");
        if (ItemDatabase.LootWeights.TryGetValue(id, out int w)) where.Add(w <= 2 ? "도시(드묾)" : "도시");
        if (ItemDatabase.TradeOffers.Exists(t => t.GetId == id)) where.Add("잡상인");
        return where.Count > 0 ? "얻는 곳  " + string.Join(" · ", where) : "";
    }

    void DeliverTruckPart(string id)
    {
        if (truckSite != null) truckSite.Deliver(id);
        else GameManager.Instance.State.DeliverToTruck(id);
        RefreshTruck();
    }

    void DeliverAllTruckParts()
    {
        if (truckSite != null) truckSite.DeliverAll();
        else foreach (var kv in ItemDatabase.TruckParts) GameManager.Instance.State.DeliverToTruck(kv.Key);
        RefreshTruck();
    }

    void EscapeByTruck()
    {
        var gm = GameManager.Instance;
        gm.TruckEscape();
        if (gm.IsGameOver) CloseTruck();
    }
}
