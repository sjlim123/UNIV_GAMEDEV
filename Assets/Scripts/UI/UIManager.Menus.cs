using System;
using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴, 일시정지 메뉴, 설정·조작법·제작진 창
// 배치: 왼쪽 위 제목(옆에 핏빛 세로 막대) + 왼쪽 세로 메뉴, 오른쪽 아래 큰 주요 버튼(메인 메뉴는 그 위에 최근 저장 정보 판), 오른쪽 아래 구석 버전
// 디자인(2026-11-05, 사용자 확정 '더러운 강판'): 3D 배경을 비네트와 왼쪽 그러데이션으로만 누르고, 버튼은 흙먼지·긁힘이 앉은 어두운 강판 위에
// 칠이 군데군데 벗겨진 가는 테두리와 오른쪽 끝 붉은 막대를 둔다. 마우스를 올리면 판이 밝아지고 막대가 빛난다. 일시정지는 멈춘 화면을 흐리게 깐다
public partial class UIManager
{
    const string GameTitle = "벙커랩";
    const string GameSubtitle = "BUNKER  LAB   ·   NUCLEAR  SURVIVAL";
    const float MenuColumnWidth = 640; // (옛 유리 기둥 폭, FitMenuGlass가 쓴다)
    // 메뉴 판: 왼쪽 여백, 판 크기. 포인트 색은 핏빛 붉은색(제목 막대·버튼 끝 막대), 글자 강조는 호박색 그대로
    const float MenuLeft = 96, SlabW = 440, SlabH = 52;
    static readonly Color MenuRed = new Color(0.72f, 0.1f, 0.08f);
    static readonly Color PlateFill = new Color(0.075f, 0.075f, 0.08f, 0.92f);
    static readonly Color PlateHover = new Color(0.17f, 0.165f, 0.16f, 0.95f);
    static readonly Color InfoFill = new Color(0.3f, 0.28f, 0.18f, 0.82f);
    GameObject mainMenuRoot, pauseRoot;
    RawImage menuBlur, pauseBlur;
    UnityEngine.UI.Text pauseInfo;
    Button continueButton;
    // 오른쪽 아래 큰 주요 버튼(메인 메뉴: 저장이 있으면 이어하기, 없으면 새 게임)과 그 위의 흙색 정보 판(최근 저장, 저장이 있을 때만)
    UnityEngine.UI.Text primaryLabel, primaryNote, primaryInfoTitle;
    GameObject primaryInfo;
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
        MenuBackdrop(m, 0.7f);
        TitleBlock(m, GameTitle, GameSubtitle, 92, -96);

        float y = -318, step = SlabH + 10;
        MenuButton(m, "새 게임", "NEW GAME", new Vector2(MenuLeft, y), ConfirmNewGame);
        continueButton = MenuButton(m, "이어하기", "CONTINUE", new Vector2(MenuLeft, y -= step), () => OpenSlots(false), true, "");
        MenuButton(m, "설정", "SETTINGS", new Vector2(MenuLeft, y -= step), () => OpenSettings());
        MenuButton(m, "조작법", "CONTROLS", new Vector2(MenuLeft, y -= step), OpenControls);
        MenuButton(m, "제작진", "CREDITS", new Vector2(MenuLeft, y -= step), OpenCredits);
        MenuButton(m, "패치노트", "PATCH NOTES", new Vector2(MenuLeft, y -= step), OpenPatchNotes);
        MenuButton(m, "게임 종료", "QUIT", new Vector2(MenuLeft, y -= step + 14), () => GameManager.Instance.QuitGame());

        // 오른쪽 아래 큰 주요 버튼: 저장이 있으면 이어하기, 없으면 새 게임 (RefreshContinueButton이 글과 정보 판을 맞춘다)
        PrimaryButton(m, "새 게임", () => { if (SaveSystem.LatestSlot > 0) OpenSlots(false); else ConfirmNewGame(); }, out primaryLabel, out primaryNote, true);

        var credit = NewText(m, "Team_31   ·   Made with Unity 6", 15, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.35f));
        Place(credit.rectTransform, new Vector2(0, 0), new Vector2(MenuLeft + 4, 26), new Vector2(500, 24));
        return m.gameObject;
    }

    GameObject BuildPauseMenu()
    {
        var p = NewRect("PauseMenu", root);
        Stretch(p);
        // 멈춘 게임 화면을 흐리게 깔고(막이 뒤 화면 클릭도 막는다) 어둡게 누른다
        pauseBlur = BlurBackdrop(p, new Color(0.02f, 0.022f, 0.026f, 0.4f));
        pauseBlur.enabled = false;
        MenuBackdrop(p, 0.5f);
        TitleBlock(p, "일시정지", "PAUSED", 64, -120);
        pauseInfo = NewText(p, "", 18, TextAnchor.UpperLeft, Dim);
        Place(pauseInfo.rectTransform, new Vector2(0, 1), new Vector2(MenuLeft + 4, -232), new Vector2(480, 30));

        float y = -300, step = SlabH + 10;
        MenuButton(p, "설정", "SETTINGS", new Vector2(MenuLeft, y), () => OpenSettings());
        MenuButton(p, "조작법", "CONTROLS", new Vector2(MenuLeft, y -= step), OpenControls);
        MenuButton(p, "메인 메뉴로", "MAIN MENU", new Vector2(MenuLeft, y -= step), () => ConfirmLeaveExpedition("메인 메뉴로", () => GameManager.Instance.GoToMainMenu()));
        MenuButton(p, "게임 종료", "QUIT", new Vector2(MenuLeft, y -= step + 14), () => ConfirmLeaveExpedition("게임 종료", () => GameManager.Instance.QuitGame()));

        // 오른쪽 아래 큰 '계속하기'. 왼쪽 아래에는 Tip 스위치와 Esc 안내
        PrimaryButton(p, "계속하기", () => SetPaused(false), out _, out _);
        BuildTipSwitch(p, new Vector2(MenuLeft + 4, 70));
        var hint = NewText(p, "ESC   계속하기", 15, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.35f), FontMono);
        Place(hint.rectTransform, new Vector2(0, 0), new Vector2(MenuLeft + 4, 26), new Vector2(460, 24));
        return p.gameObject;
    }

    // 메뉴 뒤 배경: 화면 가장자리 비네트 + 왼쪽에서 오른쪽으로 옅어지는 어두운 막 (3D 배경이 오른쪽에 비친다)
    static void MenuBackdrop(RectTransform parent, float strength)
    {
        var vig = NewRect("Vignette", parent).gameObject.AddComponent<Image>();
        Stretch(vig.rectTransform);
        vig.sprite = UIStyle.Vignette;
        vig.color = new Color(0, 0, 0, 0.75f * strength);
        vig.raycastTarget = false;
        var shade = NewRect("Shade", parent).gameObject.AddComponent<Image>();
        shade.sprite = UIStyle.HorizontalFade;
        shade.color = new Color(0, 0, 0, strength);
        shade.raycastTarget = false;
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(0.6f, 1);
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
    }

    // 제목 묶음: 왼쪽 핏빛 세로 막대 + 큰 제목 + 작은 영문 부제 + 가는 가로선
    static void TitleBlock(RectTransform parent, string title, string subtitle, int size, float top)
    {
        var bar = NewRect("TitleBar", parent);
        Place(bar, new Vector2(0, 1), new Vector2(MenuLeft - 22, top + 12), new Vector2(6, size + 4));
        bar.pivot = new Vector2(0, 1);
        bar.anchoredPosition = new Vector2(MenuLeft - 22, top + 12);
        bar.gameObject.AddComponent<Image>().color = MenuRed;

        var t = NewText(parent, title, size, TextAnchor.LowerLeft, Text, FontTitle);
        t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        Place(t.rectTransform, new Vector2(0, 1), new Vector2(MenuLeft + 6, top), new Vector2(620, size + 28));
        var sub = NewText(parent, subtitle, 17, TextAnchor.UpperLeft, PaleDim, FontMono);
        Place(sub.rectTransform, new Vector2(0, 1), new Vector2(MenuLeft + 10, top - size - 30), new Vector2(620, 26));
        var line = NewRect("Rule", parent);
        Place(line, new Vector2(0, 1), new Vector2(MenuLeft + 8, top - size - 62), new Vector2(SlabW - 8, 1));
        line.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.14f);
    }

    // ---------- Tip 스위치 (일시정지 메뉴): 'Tip' 글자 옆의 좌우 알약 스위치 ----------

    RectTransform tipKnob;
    Image tipTrack, tipKnobImg;
    UnityEngine.UI.Text tipState;
    const float TipSwitchW = 58, TipSwitchH = 28, TipKnob = 22;

    void BuildTipSwitch(Transform parent, Vector2 pos)
    {
        var row = NewRect("TipRow", parent);
        Place(row, new Vector2(0, 0), pos, new Vector2(460, 34));

        var label = NewText(row, "Tip", 24, TextAnchor.MiddleLeft, Text, FontTitle);
        Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(56, 34));
        label.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

        // 알약 모양 홈 + 테두리 + 둥근 손잡이. 켜지면 홈이 호박색이 되고 손잡이가 오른쪽으로 간다
        var sw = NewRect("Switch", row);
        Place(sw, new Vector2(0, 0.5f), new Vector2(56, 0), new Vector2(TipSwitchW, TipSwitchH));
        tipTrack = sw.gameObject.AddComponent<Image>();
        tipTrack.sprite = UIStyle.Fill;
        tipTrack.type = Image.Type.Sliced;
        tipTrack.pixelsPerUnitMultiplier = 0.3f;
        var frame = NewRect("Frame", sw).gameObject.AddComponent<Image>();
        Stretch(frame.rectTransform);
        frame.sprite = UIStyle.Frame;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 0.3f;
        frame.color = new Color(1, 1, 1, 0.16f);
        frame.raycastTarget = false;
        tipKnob = NewRect("Knob", sw);
        tipKnob.anchorMin = tipKnob.anchorMax = new Vector2(0, 0.5f);
        tipKnob.pivot = new Vector2(0.5f, 0.5f);
        tipKnob.sizeDelta = new Vector2(TipKnob, TipKnob);
        tipKnobImg = tipKnob.gameObject.AddComponent<Image>();
        tipKnobImg.sprite = UIStyle.Dot;
        tipKnobImg.raycastTarget = false;
        tipKnob.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);

        var b = sw.gameObject.AddComponent<Button>();
        b.targetGraphic = tipTrack;
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(() =>
        {
            GameSettings.Tips = !GameSettings.Tips;
            GameSettings.Save();
            ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.45f, GameSettings.Tips ? 1.15f : 0.85f, SoundCategory.UI);
            UpdateTipSwitch(false);
        });
        UIPress.Attach(b, 1.04f);

        tipState = NewText(row, "", 15, TextAnchor.MiddleLeft, Dim);
        Place(tipState.rectTransform, new Vector2(0, 0.5f), new Vector2(56 + TipSwitchW + 12, 0), new Vector2(336, 34));
        UpdateTipSwitch(true);
    }

    // 스위치 색·글을 설정값에 맞춘다. 손잡이는 UpdateTipKnob에서 미끄러져 간다
    void UpdateTipSwitch(bool instant)
    {
        if (tipKnob == null) return;
        bool on = GameSettings.Tips;
        tipTrack.color = on ? new Color(Accent.r, Accent.g, Accent.b, 0.95f) : new Color(0.16f, 0.17f, 0.18f, 0.95f);
        tipKnobImg.color = on ? new Color(0.98f, 0.96f, 0.9f) : new Color(0.55f, 0.55f, 0.55f);
        tipState.text = on ? "켜짐  ·  할 일 · 나침반 · 대상 불빛 · 힌트 · 조작키" : "꺼짐  ·  안내 없이 플레이";
        if (instant) tipKnob.anchoredPosition = new Vector2(TipKnobX(on), 0);
    }

    static float TipKnobX(bool on) => on ? TipSwitchW - TipKnob / 2f - 3f : TipKnob / 2f + 3f;

    void UpdateTipKnob()
    {
        if (tipKnob == null || pauseRoot == null || !pauseRoot.activeSelf) return;
        tipKnob.anchoredPosition = new Vector2(Mathf.Lerp(tipKnob.anchoredPosition.x, TipKnobX(GameSettings.Tips), Time.unscaledDeltaTime * 18f), 0);
    }

    // (옛 유리 기둥은 쓰지 않는다. 메인 메뉴 블러도 없으므로 FitMenuGlass는 바로 돌아간다)
    void FitMenuGlass()
    {
        if (menuBlur == null) return;
        float w = root.rect.width;
        if (w > 0) menuBlur.uvRect = new Rect(0, 0, Mathf.Clamp01(MenuColumnWidth / w), 1);
    }

    // ---------- 더러운 강판 ----------

    // 더러운 강판 꾸밈: 흙먼지(타일) → 긁힘·먼지 → 가장자리 그늘 → 군데군데 칠이 벗겨진 가는 테두리("WornFrame", 돌려준다. 호버가 색을 바꾼다)
    static Image DirtyPlate(RectTransform rt, float dirt = 1f)
    {
        var dust = NewRect("Dust", rt).gameObject.AddComponent<Image>();
        Stretch(dust.rectTransform);
        dust.sprite = UIStyle.Dust;
        dust.type = Image.Type.Tiled;
        dust.color = new Color(0.45f, 0.36f, 0.22f, 0.28f * dirt);
        dust.raycastTarget = false;
        var grime = NewRect("Grime", rt).gameObject.AddComponent<Image>();
        Stretch(grime.rectTransform);
        grime.sprite = UIStyle.Grime;
        grime.type = Image.Type.Tiled;
        grime.color = new Color(1, 1, 1, 0.35f * dirt);
        grime.raycastTarget = false;
        var shade = NewRect("Shade", rt).gameObject.AddComponent<Image>();
        Stretch(shade.rectTransform);
        shade.sprite = UIStyle.Vignette;
        shade.color = new Color(0, 0, 0, 0.45f);
        shade.raycastTarget = false;
        var frame = NewRect("WornFrame", rt).gameObject.AddComponent<Image>();
        Stretch(frame.rectTransform);
        frame.sprite = UIStyle.WornFrame;
        frame.type = Image.Type.Tiled;
        frame.color = new Color(1, 1, 1, 0.3f);
        frame.raycastTarget = false;
        return frame;
    }

    // 오른쪽 끝의 붉은 막대와 그 빛 (빛을 돌려준다. 호버가 알파를 올린다)
    static Image RedBar(RectTransform rt, float inset, Vector2 size, float glowSize, float glowAlpha)
    {
        var glow = NewRect("RedGlow", rt).gameObject.AddComponent<Image>();
        Place(glow.rectTransform, new Vector2(1, 0.5f), new Vector2(-inset, 0), new Vector2(glowSize, glowSize));
        glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        glow.sprite = UIStyle.Glow;
        glow.color = new Color(MenuRed.r, MenuRed.g, MenuRed.b, glowAlpha);
        glow.raycastTarget = false;
        var bar = NewRect("RedBar", rt).gameObject.AddComponent<Image>();
        Place(bar.rectTransform, new Vector2(1, 0.5f), new Vector2(-inset, 0), size);
        bar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bar.color = MenuRed;
        bar.raycastTarget = false;
        return glow;
    }

    // 메뉴 버튼: 더러운 강판 위에 한글 이름(왼쪽)과 영문 꼬리표(오른쪽), 오른쪽 끝 붉은 막대.
    // 마우스를 올리면 판이 밝아지고 테두리가 또렷해지며 막대가 붉게 빛난다. note가 null이 아니면 꼬리표 자리에 설명 줄(이어하기의 최근 저장)을 둔다
    Button MenuButton(Transform parent, string label, string tag, Vector2 pos, Action onClick, bool interactable = true, string note = null)
    {
        var rt = NewRect("MenuButton", parent);
        Place(rt, new Vector2(0, 1), pos, new Vector2(SlabW, SlabH));
        var slab = rt.gameObject.AddComponent<Image>(); // 판. 클릭을 받는다
        slab.color = PlateFill;
        var frame = DirtyPlate(rt);

        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = slab;
        b.transition = Selectable.Transition.None; // 색은 UIHover가 바꾼다
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.interactable = interactable;
        if (onClick != null) b.onClick.AddListener(() => onClick());
        UIPress.Attach(b, 1.01f);

        var glow = RedBar(rt, 12, new Vector2(4, 28), 44, 0.25f);
        var t = NewText(rt, label, 27, TextAnchor.MiddleLeft, Text, FontTitle);
        Stretch(t.rectTransform, 22, 0);
        t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
        var tg = NewText(rt, tag, 13, TextAnchor.MiddleRight, PaleDim, FontMono);
        Stretch(tg.rectTransform, 30, 0);
        UnityEngine.UI.Text nt = null;
        if (note != null)
        {
            tg.text = "";
            nt = NewText(rt, note, 13, TextAnchor.MiddleRight, PaleDim);
            nt.name = "Note";
            Stretch(nt.rectTransform, 30, 0);
        }

        var hover = rt.gameObject.AddComponent<UIHover>();
        hover.Gate = b;
        hover.Add(slab, PlateFill, PlateHover)                                                           // 0 판
             .Add(frame, new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0.75f))                             // 1 테두리
             .Add(t, Text, Color.white)                                                                    // 2 이름
             .Add(tg, PaleDim, Pale)                                                                       // 3 꼬리표
             .Add(glow, new Color(MenuRed.r, MenuRed.g, MenuRed.b, 0.25f), new Color(1f, 0.25f, 0.15f, 0.8f)); // 4 붉은 빛
        if (nt != null) hover.Add(nt, PaleDim, Pale);                                                      // 5 설명 줄
        hover.Move = t.rectTransform;
        hover.MoveFrom = Vector2.zero;
        hover.MoveTo = new Vector2(6, 0);
        SetMenuButtonColors(hover, interactable);
        return b;
    }

    // 메뉴 버튼의 글자 색 (누를 수 있을 때 / 없을 때). UIHover 항목 순서: 0 판, 1 테두리, 2 이름, 3 꼬리표, 4 붉은 빛, 5 설명
    static void SetMenuButtonColors(UIHover hover, bool on)
    {
        var off = new Color(1, 1, 1, 0.3f);
        hover.SetColors(2, on ? Text : off, on ? Color.white : off);
        hover.SetColors(3, on ? PaleDim : off, on ? Pale : off);
        hover.SetColors(5, on ? PaleDim : off, on ? Pale : off);
    }

    // 오른쪽 아래 큰 주요 버튼: 더러운 강판 + 오른쪽 끝 붉은 막대, 이름은 막대 옆에 오른쪽 정렬.
    // info면 위에 흙색 정보 판(최근 저장: 제목 + noteText)을 붙인다 — RefreshContinueButton이 글을 채우고 저장이 없으면 감춘다
    Button PrimaryButton(RectTransform parent, string label, Action onClick, out UnityEngine.UI.Text labelText, out UnityEngine.UI.Text noteText, bool info = false)
    {
        var rt = NewRect("PrimaryButton", parent);
        Place(rt, new Vector2(1, 0), new Vector2(-56, 68), new Vector2(380, 72));
        var slab = rt.gameObject.AddComponent<Image>();
        slab.color = PlateFill;
        var frame = DirtyPlate(rt);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = slab;
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(() => onClick());
        UIPress.Attach(b, 1.03f);
        var glow = RedBar(rt, 16, new Vector2(6, 40), 72, 0.35f);

        labelText = NewText(rt, label, 34, TextAnchor.MiddleRight, Text, FontTitle);
        Stretch(labelText.rectTransform, 28, 0);
        labelText.rectTransform.offsetMax = new Vector2(-44, 0);
        labelText.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

        var hover = rt.gameObject.AddComponent<UIHover>();
        hover.Gate = b;
        hover.Add(slab, PlateFill, PlateHover)
             .Add(frame, new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0.75f))
             .Add(labelText, Text, Color.white)
             .Add(glow, new Color(MenuRed.r, MenuRed.g, MenuRed.b, 0.35f), new Color(1f, 0.25f, 0.15f, 0.9f));

        noteText = null;
        if (info)
        {
            var panel = NewRect("Info", parent);
            Place(panel, new Vector2(1, 0), new Vector2(-56, 68 + 72 + 10), new Vector2(380, 66));
            var fill = panel.gameObject.AddComponent<Image>();
            fill.color = InfoFill;
            fill.raycastTarget = false;
            var f = DirtyPlate(panel, 0.8f);
            f.color = new Color(1, 1, 1, 0.18f);
            primaryInfoTitle = NewText(panel, "최근 저장", 19, TextAnchor.UpperLeft, Text, FontTitle);
            Place(primaryInfoTitle.rectTransform, new Vector2(0, 1), new Vector2(18, -9), new Vector2(340, 26));
            noteText = NewText(panel, "", 13, TextAnchor.UpperLeft, PaleDim, FontMono);
            Place(noteText.rectTransform, new Vector2(0, 1), new Vector2(18, -36), new Vector2(340, 20));
            noteText.horizontalOverflow = HorizontalWrapMode.Overflow;
            primaryInfo = panel.gameObject;
        }
        return b;
    }

    // 메뉴 층을 다시 만든다 (설정 변경·디자인 손질용). 그리는 순서(창보다 아래)는 그대로 두고, 열려 있던 메인 메뉴는 다시 연다
    public void RebuildMenus()
    {
        bool menuOn = mainMenuRoot != null && mainMenuRoot.activeSelf;
        if (IsPaused) SetPaused(false);
        int mi = mainMenuRoot != null ? mainMenuRoot.transform.GetSiblingIndex() : -1;
        int pi = pauseRoot != null ? pauseRoot.transform.GetSiblingIndex() : -1;
        if (mainMenuRoot != null) DestroyImmediate(mainMenuRoot);
        if (pauseRoot != null) DestroyImmediate(pauseRoot);
        primaryInfo = null;
        primaryInfoTitle = primaryLabel = primaryNote = null;
        BuildMenuLayers();
        if (mi >= 0) mainMenuRoot.transform.SetSiblingIndex(mi);
        if (pi >= 0) pauseRoot.transform.SetSiblingIndex(pi);
        if (menuOn) ShowMainMenu();
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

    // 저장 파일이 있을 때만 '이어하기'를 누를 수 있고, 설명 줄에 턴과 저장 시각을 보여 준다. 오른쪽 아래 큰 버튼의 글과 정보 판도 맞춘다
    void RefreshContinueButton()
    {
        if (continueButton == null) return;
        int latest = SaveSystem.LatestSlot;
        bool has = latest > 0;
        continueButton.interactable = has;
        var note = continueButton.transform.Find("Note");
        if (note != null) note.GetComponent<UnityEngine.UI.Text>().text = has ? $"슬롯 {latest} · {SaveSystem.Summary(latest)}" : "저장된 게임 없음";
        var hover = continueButton.GetComponent<UIHover>();
        if (hover != null) SetMenuButtonColors(hover, has);
        if (primaryLabel != null) primaryLabel.text = has ? "이어하기" : "새 게임";
        if (primaryInfo != null) primaryInfo.SetActive(has);
        if (primaryNote != null) primaryNote.text = has ? $"SLOT {latest}  ·  {SaveSystem.Summary(latest)}" : "";
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
        UpdateTipSwitch(true);
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
                { K(GameAction.Interact), "줍기 · 문 열기 · 시설 사용 · 손전등 꺼내기/넣기" }, { K(GameAction.Flashlight), "손전등 켜기/끄기 (꺼내 든 뒤)" }, { "1 ~ 8 / 휠", "핫바 칸 선택" },
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
