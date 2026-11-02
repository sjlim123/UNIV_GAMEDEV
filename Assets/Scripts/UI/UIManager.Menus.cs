using System;
using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴, 일시정지 메뉴, 설정·조작법·제작진 창
// 배치: 왼쪽 위 제목 + 왼쪽 세로 메뉴(리썰 컴퍼니·오버워치), 맨 아래 패치노트, 오른쪽 아래 버전
// 디자인: 메뉴는 왼쪽 세로 유리 기둥 위에 놓는다 (메인 메뉴는 기둥 자리만 뒤 배경을 흐리게, 일시정지는 화면 전체를 흐리게)
public partial class UIManager
{
    const string GameTitle = "벙커랩";
    const string GameSubtitle = "BUNKER  LAB   ·   NUCLEAR  SURVIVAL";
    const float MenuColumnWidth = 640;
    GameObject mainMenuRoot, pauseRoot;
    RawImage menuBlur, pauseBlur;
    UnityEngine.UI.Text pauseInfo;
    Button continueButton;
    int settingsTab;
    // 컨트롤 탭에서 새 키를 기다리는 동작 (없으면 null)
    GameAction? rebinding;
    public bool IsRebinding => rebinding.HasValue;

    public bool IsPaused { get; private set; }

    void BuildMenuLayers()
    {
        mainMenuRoot = BuildMainMenu();
        mainMenuRoot.SetActive(false);
        pauseRoot = BuildPauseMenu();
        pauseRoot.SetActive(false);
    }

    GameObject BuildMainMenu()
    {
        var m = NewRect("MainMenu", root);
        Stretch(m);

        // 오른쪽으로 옅어지는 가림막: 유리 기둥과 배경을 자연스럽게 잇는다
        var shade = NewRect("Shade", m).gameObject.AddComponent<Image>();
        shade.sprite = UIStyle.HorizontalFade;
        shade.color = new Color(0, 0, 0, 0.6f);
        shade.raycastTarget = false;
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(0.62f, 1);
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

        // 왼쪽 유리 기둥: 흐린 화면 중 기둥 자리만 잘라 깐다 (FitMenuGlass)
        var col = MenuColumn(m);
        menuBlur = NewRect("Blur", col).gameObject.AddComponent<RawImage>();
        menuBlur.transform.SetAsFirstSibling();
        Stretch(menuBlur.rectTransform);
        menuBlur.raycastTarget = false;
        menuBlur.enabled = false;

        var title = NewText(m, GameTitle, 88, TextAnchor.LowerLeft, Text, FontBold);
        title.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(104, -96), new Vector2(520, 120));
        var sub = NewText(m, GameSubtitle, 22, TextAnchor.UpperLeft, Accent, FontSemiBold);
        Place(sub.rectTransform, new Vector2(0, 1), new Vector2(110, -222), new Vector2(520, 34));
        var line = NewRect("Line", m);
        Place(line, new Vector2(0, 1), new Vector2(110, -266), new Vector2(140, 3));
        line.gameObject.AddComponent<Image>().color = Accent;

        float y = -330, step = 66;
        MenuButton(m, "새 게임", new Vector2(92, y), ConfirmNewGame);
        continueButton = MenuButton(m, "이어하기", new Vector2(92, y -= step), () => OpenSlots(false), true, "");
        MenuButton(m, "설정", new Vector2(92, y -= step), () => OpenSettings());
        MenuButton(m, "조작법", new Vector2(92, y -= step), OpenControls);
        MenuButton(m, "제작진", new Vector2(92, y -= step), OpenCredits);
        MenuButton(m, "게임 종료", new Vector2(92, y -= step), () => GameManager.Instance.QuitGame());
        // 패치노트는 가는 선으로 나눠 맨 아래에 둔다
        var divider = NewRect("Divider", m);
        Place(divider, new Vector2(0, 1), new Vector2(110, y - 70), new Vector2(420, 1));
        divider.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
        MenuButton(m, "패치노트", new Vector2(92, y -= step + 16), OpenPatchNotes);

        var credit = NewText(m, "Team_31   ·   Made with Unity 6", 16, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.4f));
        Place(credit.rectTransform, new Vector2(0, 0), new Vector2(110, 24), new Vector2(500, 26));
        return m.gameObject;
    }

    GameObject BuildPauseMenu()
    {
        var p = NewRect("PauseMenu", root);
        Stretch(p);
        // 멈춘 게임 화면을 흐리게 깔고(막이 뒤 화면 클릭도 막는다) 왼쪽에 유리 기둥을 세운다
        pauseBlur = BlurBackdrop(p, new Color(0.02f, 0.025f, 0.03f, 0.3f));
        pauseBlur.enabled = false;
        MenuColumn(p);

        var title = NewText(p, "일시정지", 64, TextAnchor.LowerLeft, Text, FontBold);
        title.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(104, -120), new Vector2(500, 90));
        pauseInfo = NewText(p, "", 19, TextAnchor.UpperLeft, Dim);
        Place(pauseInfo.rectTransform, new Vector2(0, 1), new Vector2(110, -214), new Vector2(480, 30));
        var line = NewRect("Line", p);
        Place(line, new Vector2(0, 1), new Vector2(110, -256), new Vector2(110, 3));
        line.gameObject.AddComponent<Image>().color = Accent;

        float y = -310, step = 66;
        MenuButton(p, "계속하기", new Vector2(92, y), () => SetPaused(false));
        MenuButton(p, "설정", new Vector2(92, y -= step), () => OpenSettings());
        MenuButton(p, "조작법", new Vector2(92, y -= step), OpenControls);
        MenuButton(p, "메인 메뉴로", new Vector2(92, y -= step), () => ConfirmLeaveExpedition("메인 메뉴로", () => GameManager.Instance.GoToMainMenu()));
        MenuButton(p, "게임 종료", new Vector2(92, y -= step), () => ConfirmLeaveExpedition("게임 종료", () => GameManager.Instance.QuitGame()));

        var hint = NewText(p, "Esc   계속하기", 17, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.4f));
        Place(hint.rectTransform, new Vector2(0, 0), new Vector2(110, 24), new Vector2(460, 26));
        return p.gameObject;
    }

    // 왼쪽 세로 유리 기둥 (메인·일시정지 메뉴): 어두운 유리 + 오른쪽 가는 금색 선 + 바깥으로 옅어지는 그림자
    static RectTransform MenuColumn(Transform parent)
    {
        var col = NewRect("Column", parent);
        col.anchorMin = Vector2.zero;
        col.anchorMax = new Vector2(0, 1);
        col.pivot = new Vector2(0, 0.5f);
        col.offsetMin = Vector2.zero;
        col.offsetMax = new Vector2(MenuColumnWidth, 0);

        var fill = NewRect("Glass", col).gameObject.AddComponent<Image>();
        Stretch(fill.rectTransform);
        fill.color = new Color(0.035f, 0.04f, 0.045f, 0.52f);
        fill.raycastTarget = false;

        var shadow = NewRect("Shadow", col).gameObject.AddComponent<Image>();
        var srt = shadow.rectTransform;
        srt.anchorMin = new Vector2(1, 0);
        srt.anchorMax = Vector2.one;
        srt.pivot = new Vector2(0, 0.5f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = new Vector2(56, 0);
        shadow.sprite = UIStyle.HorizontalFade;
        shadow.color = new Color(0, 0, 0, 0.28f);
        shadow.raycastTarget = false;

        var edge = NewRect("Edge", col).gameObject.AddComponent<Image>();
        var ert = edge.rectTransform;
        ert.anchorMin = new Vector2(1, 0);
        ert.anchorMax = Vector2.one;
        ert.pivot = new Vector2(1, 0.5f);
        ert.offsetMin = new Vector2(-1, 0);
        ert.offsetMax = Vector2.zero;
        edge.color = new Color(0.9f, 0.8f, 0.55f, 0.26f);
        edge.raycastTarget = false;
        return col;
    }

    // 메인 메뉴 기둥에는 흐린 화면 중 기둥 자리만 잘라 보여 준다
    void FitMenuGlass()
    {
        if (menuBlur == null) return;
        float w = root.rect.width;
        if (w > 0) menuBlur.uvRect = new Rect(0, 0, Mathf.Clamp01(MenuColumnWidth / w), 1);
    }

    // 메뉴 버튼: 평소에는 글자와 왼쪽 금색 막대만 보인다.
    // 마우스를 올리면 왼쪽부터 번지는 금빛이 켜지고, 막대가 길고 굵어지고, 글자가 살짝 오른쪽으로 밀린다
    Button MenuButton(Transform parent, string label, Vector2 pos, Action onClick, bool interactable = true, string tag = null)
    {
        var rt = NewRect("MenuButton", parent);
        Place(rt, new Vector2(0, 1), pos, new Vector2(460, 58));
        var glow = rt.gameObject.AddComponent<Image>(); // 평소에는 투명하지만 클릭은 받는다
        glow.sprite = UIStyle.HorizontalFade;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = glow;
        b.transition = Selectable.Transition.None; // 색은 UIHover가 바꾼다
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.interactable = interactable;
        if (onClick != null) b.onClick.AddListener(() => onClick());
        UIPress.Attach(b, 1f);

        var bar = NewRect("Bar", rt);
        bar.anchorMin = bar.anchorMax = new Vector2(0, 0.5f);
        bar.pivot = new Vector2(0, 0.5f);
        bar.anchoredPosition = new Vector2(2, 0);
        bar.sizeDelta = new Vector2(3, 24);
        var barImg = bar.gameObject.AddComponent<Image>();
        barImg.raycastTarget = false;

        var t = NewText(rt, label, 30, TextAnchor.MiddleLeft, Text, FontSemiBold);
        Stretch(t.rectTransform, 24, 0);
        t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
        if (tag != null)
        {
            var tg = NewText(rt, tag, 15, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.38f));
            Stretch(tg.rectTransform, 16, 0);
        }

        var hover = rt.gameObject.AddComponent<UIHover>();
        hover.Gate = b;
        // 평소 알파를 0이 아닌 아주 작은 값으로 둔다 (완전히 투명한 그림은 잘려서 클릭을 못 받을 수 있다)
        hover.Add(glow, new Color(Accent.r, Accent.g, Accent.b, 0.01f), new Color(Accent.r, Accent.g, Accent.b, 0.2f))
             .Add(barImg, Color.clear, Color.clear)
             .Add(t, Color.clear, Color.clear);
        hover.Move = t.rectTransform;
        hover.MoveFrom = Vector2.zero;
        hover.MoveTo = new Vector2(10, 0);
        hover.Grow = bar;
        hover.GrowFrom = new Vector2(3, 24);
        hover.GrowTo = new Vector2(5, 40);
        SetMenuButtonColors(hover, interactable);
        return b;
    }

    // 메뉴 버튼의 막대·글자 색 (누를 수 있을 때 / 없을 때). UIHover 항목 순서: 0 금빛, 1 막대, 2 글자
    static void SetMenuButtonColors(UIHover hover, bool on)
    {
        var off = new Color(1, 1, 1, 0.35f);
        var barOff = new Color(1, 1, 1, 0.15f);
        hover.SetColors(1, on ? new Color(Accent.r, Accent.g, Accent.b, 0.65f) : barOff, on ? Accent : barOff);
        hover.SetColors(2, on ? Text : off, on ? new Color(1f, 0.95f, 0.84f) : off);
    }

    public void ShowMainMenu()
    {
        mainMenuRoot.SetActive(true);
        RefreshContinueButton();
        ClosePanel();
        RefreshGlass();
    }

    void ConfirmNewGame() => OpenSlots(true);

    // 저장 슬롯 고르기 (유리 카드 3장). 새 게임이면 빈 슬롯은 바로 시작하고 찬 슬롯은 덮어쓸지 묻는다. 이어하기면 불러오거나 지운다
    void OpenSlots(bool newGame)
    {
        OpenPanel(newGame ? "새 게임 · 저장 슬롯" : "이어하기 · 저장 슬롯", c =>
        {
            Line(c, newGame ? "새 게임을 저장할 슬롯을 고르세요." : "불러올 게임을 고르세요.", 22, Dim);
            int latest = SaveSystem.LatestSlot;
            for (int i = 1; i <= SaveSystem.SlotCount; i++)
            {
                int slot = i;
                bool exists = SaveSystem.Exists(slot);
                var data = exists ? SaveSystem.LoadData(slot) : null;
                bool readable = data != null;
                var card = SlotCard(c, slot, exists, data, slot == latest);
                if (newGame)
                {
                    var b = SlotButton(card, exists ? "덮어쓰기" : "시작", () => { if (exists) ConfirmOverwrite(slot); else GameManager.Instance.NewGame(slot); }, true, 150);
                    if (!exists) AccentButton(b);
                }
                else
                {
                    AccentButton(SlotButton(card, "불러오기", () => GameManager.Instance.ContinueGame(slot), readable, 150));
                    if (exists) SlotButton(card, "삭제", () => ConfirmDeleteSlot(slot), true, 110);
                }
            }
        });
    }

    // 저장 슬롯 카드: 왼쪽 큰 번호, 가운데 요약(턴·체력·저장 시각 또는 "비어 있음"), 오른쪽 버튼 (SlotButton으로 붙인다)
    RectTransform SlotCard(RectTransform parent, int slot, bool exists, SaveData data, bool latest)
    {
        var card = Row(parent);
        var h = card.GetComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 18, 12, 12);
        h.spacing = 18;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childForceExpandHeight = false;
        var le = card.GetComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 112;

        var c = exists ? Accent : Dim;
        var badge = Box(card, "Number", new Color(c.r, c.g, c.b, exists ? 0.14f : 0.05f), new Color(c.r, c.g, c.b, exists ? 0.6f : 0.22f));
        badge.GetComponent<Image>().raycastTarget = false;
        var ble = badge.gameObject.AddComponent<LayoutElement>();
        ble.minWidth = ble.preferredWidth = 80;
        ble.minHeight = ble.preferredHeight = 80;
        var num = NewText(badge, slot.ToString(), 40, TextAnchor.MiddleCenter, c, FontBold);
        Stretch(num.rectTransform);

        var info = NewRect("Info", card);
        var vl = info.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 2;
        vl.childAlignment = TextAnchor.MiddleLeft;
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        var ile = info.gameObject.AddComponent<LayoutElement>();
        ile.flexibleWidth = 1;
        ile.minHeight = ile.preferredHeight = 86;

        string head = $"슬롯 {slot}" + (exists && latest ? "   <size=16><color=#f9c252>· 최근 저장</color></size>" : "");
        SlotText(info, head, 26, exists ? Text : Dim, FontSemiBold);
        if (!exists) SlotText(info, "비어 있음", 20, Dim, null);
        else if (data == null) SlotText(info, "저장 파일이 손상됨", 20, Danger, null);
        else
        {
            SlotText(info, $"턴 {data.turn} / {GameState.MaxTurns}   ·   체력 {Mathf.CeilToInt(data.health)}", 20, Text, null);
            SlotText(info, data.savedAt + (string.IsNullOrEmpty(data.gameVersion) ? "" : $"   ·   Ver {data.gameVersion}"), 17, Dim, null);
        }
        return card;
    }

    static void SlotText(RectTransform parent, string text, int size, Color color, Font font)
    {
        var t = NewText(parent, text, size, TextAnchor.MiddleLeft, color, font);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.gameObject.AddComponent<LayoutElement>().minHeight = size + 8;
    }

    Button SlotButton(RectTransform card, string label, Action onClick, bool interactable, float width)
    {
        var b = RowButton(card, label, onClick, interactable, width);
        var le = b.GetComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 54;
        return b;
    }

    void ConfirmOverwrite(int slot)
    {
        OpenPanel("새 게임", c =>
        {
            Line(c, $"슬롯 {slot}의 저장된 게임({SaveSystem.Summary(slot)})이 지워집니다.\n처음부터 새로 시작할까요?", 26);
            Line(c, "");
            var row = Row(c, false);
            DangerButton(RowButton(row, "새로 시작", () => GameManager.Instance.NewGame(slot), true, 260));
            RowButton(row, "취소", () => OpenSlots(true), true, 200);
        });
    }

    void ConfirmDeleteSlot(int slot)
    {
        OpenPanel("저장 삭제", c =>
        {
            Line(c, $"슬롯 {slot}의 저장된 게임({SaveSystem.Summary(slot)})을 지울까요?\n지운 게임은 되돌릴 수 없습니다.", 26);
            Line(c, "");
            var row = Row(c, false);
            DangerButton(RowButton(row, "지우기", () => { SaveSystem.Delete(slot); RefreshContinueButton(); if (SaveSystem.AnyExists) OpenSlots(false); else ClosePanel(); }, true, 220));
            RowButton(row, "취소", () => OpenSlots(false), true, 200);
        });
    }

    // 탐험 도중 나가면 이어하기 때 가져간 물건을 잃는다는 것을 알린다 (방공호에서는 바로 나간다)
    void ConfirmLeaveExpedition(string title, Action leave)
    {
        if (GameManager.Instance.InShelter)
        {
            leave();
            return;
        }
        OpenPanel(title, c =>
        {
            Line(c, "탐험 도중에 나가면 진행 상황은 출발할 때로 돌아가고,\n이어할 때 가져간 물건을 모두 잃습니다.", 24);
            Line(c, "");
            var row = Row(c, false);
            DangerButton(RowButton(row, "나가기", () => { ClosePanel(); leave(); }, true, 220));
            RowButton(row, "취소", ClosePanel, true, 200);
        });
    }

    // 저장 파일이 있을 때만 '이어하기'를 누를 수 있고, 오른쪽에 턴과 저장 시각을 보여 준다
    void RefreshContinueButton()
    {
        if (continueButton == null) return;
        int latest = SaveSystem.LatestSlot;
        bool has = latest > 0;
        continueButton.interactable = has;
        var texts = continueButton.GetComponentsInChildren<Text>(true);
        if (texts.Length > 1) texts[1].text = has ? $"최근: 슬롯 {latest} · {SaveSystem.Summary(latest)}" : "저장된 게임 없음";
        var hover = continueButton.GetComponent<UIHover>();
        if (hover != null) SetMenuButtonColors(hover, has);
    }

    public void HideMainMenu() => mainMenuRoot.SetActive(false);

    public void SetPaused(bool paused)
    {
        if (paused && (PlayerController.Instance == null || GameManager.Instance.IsGameOver)) paused = false;
        if (!paused && IsPanelOpen && IsPaused) ClosePanel();
        IsPaused = paused;
        pauseRoot.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        if (!paused) return;

        // 지금 어디서 몇 턴째인지 제목 아래에 보여 주고, 멈춘 화면을 한 번 찍어 흐리게 깐다
        var gm = GameManager.Instance;
        var s = gm.State;
        pauseInfo.text = s == null ? "" : $"턴 {s.Turn} / {GameState.MaxTurns}   ·   {(gm.InShelter ? "방공호" : "도시 탐험 중")}";
        RefreshGlass();
    }

    // ---------- 설정 ----------

    public void OpenSettings()
    {
        OpenPanel("설정", c =>
        {
            var tabs = Row(c, false);
            // 탭: 0 일반(마우스·조준점) · 1 그래픽 · 2 컨트롤(조작키) · 3 소리
            string[] names = { "일반", "그래픽", "컨트롤", "소리" };
            for (int i = 0; i < names.Length; i++)
            {
                int k = i;
                var b = RowButton(tabs, names[i], () => { settingsTab = k; RefreshPanel(); }, true, 170);
                if (k == settingsTab) AccentButton(b);
            }
            Line(c, "", 8);

            if (settingsTab == 1)
            {
                Selector(c, "그래픽 품질", GameSettings.QualityNames[GameSettings.Quality], d => GameSettings.Quality = Mathf.Clamp(GameSettings.Quality + d, 0, 2));
                Selector(c, "렌더 해상도", $"{GameSettings.RenderScale * 100:0}%", d => GameSettings.RenderScale = Step(GameSettings.RenderScale, d * 0.1f, 0.5f, 1f));
                Selector(c, "밝기", $"{GameSettings.Brightness * 100:0}%", d => GameSettings.Brightness = Step(GameSettings.Brightness, d * 0.1f, 0.5f, 2f));
                Selector(c, "시야각 (FOV)", $"{GameSettings.Fov:0}", d => GameSettings.Fov = Mathf.Clamp(GameSettings.Fov + d * 5, 60, 100));
                int fps = GameSettings.FpsOptions[GameSettings.FpsIndex];
                Selector(c, "최대 FPS", fps == 0 ? "무제한" : fps.ToString(), d => GameSettings.FpsIndex = (GameSettings.FpsIndex + d + GameSettings.FpsOptions.Length) % GameSettings.FpsOptions.Length);
                Selector(c, "수직 동기화", GameSettings.VSync ? "켬" : "끔", d => GameSettings.VSync = !GameSettings.VSync);
                Selector(c, "화면 모드", GameSettings.Fullscreen ? "전체 화면" : "창 모드", d => GameSettings.Fullscreen = !GameSettings.Fullscreen);
                Selector(c, "FPS 표시", GameSettings.ShowFps ? "켬" : "끔", d => GameSettings.ShowFps = !GameSettings.ShowFps);
                Line(c, "저사양 PC: 품질 '낮음' + 렌더 해상도 70%를 권장합니다. (그림자·블룸·필름 효과를 끕니다)", 17, Dim);
            }
            else if (settingsTab == 3)
            {
                Selector(c, "전체 음량", $"{GameSettings.Volume * 100:0}%", d => GameSettings.Volume = Step(GameSettings.Volume, d * 0.1f, 0f, 1f));
                Line(c, "", 4);
                Selector(c, "환경음 (바람·비·기계·라디오)", $"{GameSettings.VolumeAmbient * 100:0}%", d => GameSettings.VolumeAmbient = Step(GameSettings.VolumeAmbient, d * 0.1f, 0f, 1f));
                Selector(c, "효과음 (발소리·타격·아이템·문)", $"{GameSettings.VolumeEffects * 100:0}%", d => GameSettings.VolumeEffects = Step(GameSettings.VolumeEffects, d * 0.1f, 0f, 1f));
                Selector(c, "적·동물 소리 (좀비·들개·약탈자)", $"{GameSettings.VolumeEnemy * 100:0}%", d => GameSettings.VolumeEnemy = Step(GameSettings.VolumeEnemy, d * 0.1f, 0f, 1f));
                Selector(c, "인터페이스 (버튼·창)", $"{GameSettings.VolumeUI * 100:0}%", d => GameSettings.VolumeUI = Step(GameSettings.VolumeUI, d * 0.1f, 0f, 1f));
                Line(c, "전체 음량은 모든 소리에, 나머지는 그 종류에만 곱해집니다.", 17, Dim);
            }
            else if (settingsTab == 2)
            {
                Line(c, "바꿀 동작의 키를 누른 뒤, 새로 쓸 키(또는 마우스 버튼)를 누르세요. Esc는 취소. 다른 동작과 겹치면 서로 바뀝니다.", 17, Dim);
                foreach (var action in KeyBindings.All)
                {
                    var a = action;
                    var keyLine = Row(c);
                    RowLabel(keyLine, KeyBindings.ActionName(a));
                    bool waiting = rebinding == a;
                    var kb = RowButton(keyLine, waiting ? "키를 누르세요..." : KeyBindings.Label(a), () => { rebinding = a; RefreshPanel(); }, true, 240);
                    if (waiting) AccentButton(kb);
                }
                Line(c, "", 6);
                var keyRow = Row(c, false);
                RowButton(keyRow, "조작키만 기본값으로", () => { KeyBindings.ResetAll(); rebinding = null; RefreshPanel(); }, true, 260);
            }
            else
            {
                Selector(c, "마우스 감도", $"{GameSettings.Sensitivity:0.0}", d => GameSettings.Sensitivity = Step(GameSettings.Sensitivity, d * 0.1f, 0.2f, 3f));
                Selector(c, "마우스 Y축 반전", GameSettings.InvertY ? "켬" : "끔", d => GameSettings.InvertY = !GameSettings.InvertY);
                Selector(c, "조준점 모양", GameSettings.CrosshairStyleNames[GameSettings.CrosshairStyle], d => GameSettings.CrosshairStyle = Cycle(GameSettings.CrosshairStyle, d, GameSettings.CrosshairStyleNames.Length));
                Selector(c, "조준점 색", GameSettings.CrosshairColorNames[GameSettings.CrosshairColor], d => GameSettings.CrosshairColor = Cycle(GameSettings.CrosshairColor, d, GameSettings.CrosshairColors.Length));
                Selector(c, "조준점 크기", GameSettings.CrosshairSizeNames[GameSettings.CrosshairSize], d => GameSettings.CrosshairSize = Mathf.Clamp(GameSettings.CrosshairSize + d, 0, GameSettings.CrosshairSizeNames.Length - 1));
            }

            Line(c, "", 8);
            var row = Row(c, false);
            RowButton(row, "기본값으로", () => { GameSettings.ResetToDefault(); rebinding = null; ApplyAllSettings(); RefreshPanel(); }, true, 200);
        }, "settings", true, () => rebinding = null);
    }

    static float Step(float value, float delta, float min, float max) => Mathf.Clamp(Mathf.Round((value + delta) * 10f) / 10f, min, max);

    static int Cycle(int value, int delta, int count) => (value + delta + count) % count;

    void Selector(RectTransform c, string label, string value, Action<int> change)
    {
        var row = Row(c);
        RowLabel(row, label);
        RowButton(row, "<", () => { change(-1); ApplyAllSettings(); RefreshPanel(); }, true, 54);
        var v = NewText(row, value, 22, TextAnchor.MiddleCenter, Accent, FontSemiBold);
        var le = v.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 170;
        RowButton(row, ">", () => { change(1); ApplyAllSettings(); RefreshPanel(); }, true, 54);
    }

    static void ApplyAllSettings()
    {
        GameSettings.Save();
        GameSettings.ApplyGlobal();
        GameSettings.ApplyScene();
        if (GameManager.Instance != null && GameManager.Instance.UI != null) GameManager.Instance.UI.ApplyCrosshair();
    }

    public void OpenControls()
    {
        OpenPanel("조작법", c =>
        {
            string K(GameAction a) => KeyBindings.Label(a);
            string[,] keys =
            {
                { $"{K(GameAction.MoveForward)} {K(GameAction.MoveLeft)} {K(GameAction.MoveBack)} {K(GameAction.MoveRight)}", "이동" }, { "마우스", "시점 회전" },
                { K(GameAction.Run), "달리기 (소리가 커진다 · 짐이 무거우면 스테미너가 빨리 준다)" }, { K(GameAction.Jump), "점프" },
                { K(GameAction.Crouch), "앉기 (조용히 숨기 · 낮은 틈 지나가기)" },
                { K(GameAction.Interact), "줍기 · 문 열기 · 시설 사용" }, { K(GameAction.Flashlight), "손전등 켜기/끄기" }, { "1 ~ 8 / 휠", "핫바 칸 선택" },
                { K(GameAction.UseItem), "선택한 아이템 사용 (먹기·치료)" }, { K(GameAction.DropItem), "선택한 아이템 버리기" },
                { K(GameAction.Attack), "공격 (무기를 들면 더 세다 · 새총은 돌을 쏜다 · 라이터는 불을 붙인다)" },
                { K(GameAction.Inventory), "소지품 열기 (아이템을 핫바로 끌어다 놓기)" }, { "Esc", "일시정지 · 창 닫기" },
            };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                var row = Row(c);
                KeyCap(row, keys[i, 0]);
                RowLabel(row, keys[i, 1]);
            }
        }, "controls");
    }

    // 키 이름을 작은 유리 키캡으로 보여 준다 (조작법 창)
    static void KeyCap(RectTransform row, string key)
    {
        var cap = Box(row, "Key", new Color(1, 1, 1, 0.06f), new Color(0.98f, 0.76f, 0.32f, 0.4f));
        cap.GetComponent<Image>().raycastTarget = false;
        var le = cap.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 190;
        var t = NewText(cap, key, 20, TextAnchor.MiddleCenter, Accent, FontSemiBold);
        Stretch(t.rectTransform, 8, 0);
    }

    public void OpenCredits()
    {
        OpenPanel("제작진", c =>
        {
            Line(c, "Team_31", 30, Accent);
            Line(c, "기획 · 개발 · 테스트: Team_31");
            Line(c, "");
            Line(c, "사용한 무료 에셋", 22, Accent);
            Line(c, "· 텍스처: Poly Haven (CC0) — 콘크리트, 아스팔트, 녹슨 금속, 벽돌, 나무");
            Line(c, "· 효과음: 코드로 합성한 소리 (외부 에셋 없음)");
            Line(c, "· 폰트: Pretendard (SIL Open Font License 1.1) — Kil Hyung-jin");
            Line(c, "· 엔진: Unity 6 (URP)");
        }, "credits");
    }
}
