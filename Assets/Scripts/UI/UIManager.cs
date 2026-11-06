using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// HUD(상태·핫바·안내)와 각종 창을 코드로 만든다. 메뉴 화면은 UIManager.Menus.cs
// 디자인 (2026-11-05 저녁): 모든 창은 뒤 화면을 흐리게 깔고 메인 메뉴와 같은 '더러운 강판'(흙먼지·긁힘·칠이 벗겨진 테두리·핏빛 제목 막대) 위에 놓인다.
// HUD는 블러 유리판(시간·날씨·할 일), 먹 번진 자국(상태·손전등), 흙색 강판(핫바)을 쓴다
public partial class UIManager : MonoBehaviour
{
    // 팔레트 (검은 유리 + 녹슨 호박): 글자는 종이색, 강조는 녹슨 호박색, 위험은 붉은색, 창은 거의 불투명한 차가운 검정
    public static readonly Color Text = new Color(0.9f, 0.87f, 0.8f);
    public static readonly Color Dim = new Color(0.6f, 0.58f, 0.52f);
    public static readonly Color Accent = new Color(0.86f, 0.57f, 0.2f);
    public static readonly Color Danger = new Color(0.93f, 0.36f, 0.26f);
    public static readonly Color PanelFill = new Color(0.03f, 0.032f, 0.035f, 0.9f);
    public static readonly Color FrameColor = new Color(0.72f, 0.48f, 0.2f, 0.3f);

    // HUD 명판: 녹슨 강철판 그림(UIStyle.Plate, 색은 그림에 있다) + 어두운 테두리. 그 위 글자는 종이색(Pale), 경고는 밝은 붉은색(Alert)
    static readonly Color HudFill = Color.white;
    static readonly Color HudFrame = new Color(0.05f, 0.045f, 0.04f, 0.7f);
    public static readonly Color Pale = new Color(0.90f, 0.86f, 0.76f);
    public static readonly Color PaleDim = new Color(0.68f, 0.64f, 0.56f);
    public static readonly Color Alert = new Color(0.93f, 0.44f, 0.3f);
    // 창 유리판의 은은한 호박색 테두리와 뒤 화면을 누르는 어두운 막
    static readonly Color GoldFrame = new Color(0.72f, 0.48f, 0.2f, 0.28f);
    static readonly Color WindowTint = new Color(0.01f, 0.012f, 0.015f, 0.5f);
    static readonly Color EndTint = new Color(0.02f, 0.012f, 0.012f, 1f); // 끝 화면: 어두운 막을 완전히 덮고 그 위에 글·핏자국이 서서히 나타난다
    // HUD 유리판 (2026-11-05): 뒤 화면을 흐려 비추는 어두운 유리. 가장자리는 투명하고 가운데로 갈수록 진하다 (UIStyle.GlassSoft + BunkerLab/HudGlass)
    static readonly Color HudGlassTint = new Color(0.035f, 0.038f, 0.045f, 0.86f);
    // 핫바 알약 유리(리퀴드 글래스): 더 비치고 가장자리가 굴절된다
    static readonly Color HudPillTint = new Color(0.56f, 0.56f, 0.54f, 0.7f); // 회백색 프로스트 유리 (흰색은 너무 밝고 검정은 다른 판과 같다는 의견 → 중간 회백색)
    static Material hudGlassMat, hudPillMat;
    // 창 안의 줄(Row) 카드 (2026-11-05 저녁, 메인 메뉴와 같은 더러운 강판): 마우스를 올리면 판이 밝아지고 벗겨진 테두리가 또렷해진다
    static readonly Color RowFill = new Color(0.095f, 0.095f, 0.1f, 0.9f);
    static readonly Color RowFillHover = new Color(0.18f, 0.175f, 0.17f, 0.95f);
    static readonly Color RowFrame = new Color(1, 1, 1, 0.22f);
    static readonly Color RowFrameHover = new Color(1, 1, 1, 0.6f);
    // 창 판 바탕 (메뉴 버튼 판보다 조금 어둡다)
    static readonly Color WindowFill = new Color(0.045f, 0.046f, 0.05f, 0.97f);

    // 글꼴 (모두 SIL OFL 1.1, Assets/Resources/Fonts): Pretendard 본문 Regular·버튼 SemiBold·큰 제목 Bold,
    // Do Hyeon(HUD 라벨), Nanum Pen Script(손글씨), Share Tech Mono(숫자·시간, 라틴만)
    public static Font Font { get; private set; }
    public static Font FontSemiBold { get; private set; }
    public static Font FontBold { get; private set; }
    public static Font FontTitle { get; private set; }
    public static Font FontHand { get; private set; }
    public static Font FontMono { get; private set; }

    RectTransform root;
    GameObject hud;
    UnityEngine.UI.Text promptText, toastText, flashlightText, batteryText, itemNameText, fpsText;
    RectTransform promptBox;
    CanvasGroup toastGroup;
    GameObject flashBox;
    Image damageOverlay, fadeOverlay, redNightOverlay, hitArc;
    // 손전등·배터리 줄의 그림 (손전등이 켜져 있으면 밝게)
    Image flashIcon, batteryIcon;
    PlayerFlashlight flashlight;
    float hitArcAlpha;
    // 마지막으로 맞은 자리 (적이 Damage 직전에 적는다) → 화면 가장자리에 그 방향으로 붉은 호가 번쩍인다
    public static Vector3? LastHitFrom;
    float redNight;
    // HUD 글자는 값이 바뀔 때만 다시 만든다 (매 프레임 문자열을 만들지 않게)
    int flashKey = int.MinValue;

    const int SlotCount = 8;
    readonly List<ItemSlotView> hotbarSlots = new List<ItemSlotView>();
    readonly List<Image> slotGlows = new List<Image>(), slotSelects = new List<Image>();
    readonly List<UnityEngine.UI.Text> slotCounts = new List<UnityEngine.UI.Text>(), slotKeys = new List<UnityEngine.UI.Text>();
    // 핫바에 지금 그려 둔 아이템과 고른 칸. 바뀌었을 때만 다시 그린다 (매 프레임 비교용 문자열을 만들지 않게)
    readonly List<KeyValuePair<string, int>> hotbarShown = new List<KeyValuePair<string, int>>();
    int hotbarSelected = int.MinValue; // int.MinValue면 다음 프레임에 무조건 다시 그린다
    int hotbarSlingShots;

    // 일반 창: 가운데 유리판. 높이는 내용에 맞추고(최소~최대), 넘치면 휠로 스크롤한다
    const float PanelWidth = 1000, PanelMinHeight = 280, PanelMaxHeight = 900;
    const float PanelTop = 122, PanelHeroTop = 56, PanelBottom = 40;
    GameObject panel;
    RectTransform panelContent, panelBox, panelView, panelScrollbar;
    UnityEngine.UI.Text panelTitle, panelTag;
    GameObject panelCloseButton, panelTitleBar;
    RawImage panelBlur;
    Image panelTint;
    ScrollRect panelScroll;
    NoticeBand panelNoticeBand;
    float panelHeight;
    Action<RectTransform> panelBuilder;
    string panelId;
    Action afterPanelClosed;

    // 끝 화면의 남은 시간 표시
    UnityEngine.UI.Text endCountdown;
    float endDeadline;
    int endShown;
    // 끝 화면: 서서히 나타나는 정도(0→1)와 핏자국 층 (죽었을 때만 켠다)
    const float EndFadeSeconds = 3f;
    CanvasGroup panelGroup;
    RectTransform endLayer;
    float endFade = 1f;
    bool endBlood;
    readonly List<Image> bloodImages = new List<Image>();
    readonly List<Color> bloodColors = new List<Color>();
    readonly List<float> bloodDelays = new List<float>();

    float toastTimer, damageAlpha, fpsTimer;
    int fpsFrames;

    public bool IsPanelOpen => panel != null && panel.activeSelf;
    // 창이나 일시정지 메뉴가 열려 있으면 플레이어 조작을 막는다
    public bool IsBlocking => IsPanelOpen || IsPaused || IsPatchNotesOpen || IsCutscenePlaying || IsWorkbenchOpen || IsInventoryOpen || IsTruckOpen || IsJournalOpen;

    void Awake()
    {
        var fallback = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
        Font = Resources.Load<Font>("Fonts/Pretendard-Regular") ?? fallback;
        FontSemiBold = Resources.Load<Font>("Fonts/Pretendard-SemiBold") ?? Font;
        FontBold = Resources.Load<Font>("Fonts/Pretendard-Bold") ?? FontSemiBold;
        FontTitle = Resources.Load<Font>("Fonts/DoHyeon-Regular") ?? FontBold;
        FontHand = Resources.Load<Font>("Fonts/NanumPenScript-Regular") ?? Font;
        FontMono = Resources.Load<Font>("Fonts/ShareTechMono-Regular") ?? FontSemiBold;

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.transform.SetParent(transform);
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        BuildHud();
        BuildMenuLayers();
        BuildPanel();

        // 버전 표시 (Project Settings > Player > Version 값을 그대로 보여준다)
        var version = NewText(root, "Ver " + Application.version, 16, TextAnchor.LowerRight, new Color(1, 1, 1, 0.45f));
        Place(version.rectTransform, new Vector2(1, 0), new Vector2(-16, 8), new Vector2(300, 24));

        fpsText = NewText(root, "", 16, TextAnchor.UpperCenter, new Color(0.6f, 1f, 0.6f, 0.8f));
        Place(fpsText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(300, 24));

        redNightOverlay = FullScreenImage("RedNight", new Color(0.45f, 0.02f, 0.01f, 0));
        redNightOverlay.sprite = UIStyle.Vignette;
        damageOverlay = FullScreenImage("Damage", new Color(0.5f, 0, 0, 0));
        hitArc = FullScreenImage("HitArc", new Color(0.85f, 0.06f, 0.02f, 0));
        hitArc.sprite = UIStyle.HitArc;
        hitArc.rectTransform.anchorMin = hitArc.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hitArc.rectTransform.sizeDelta = new Vector2(2400, 2400);
        fadeOverlay = FullScreenImage("Fade", new Color(0, 0, 0, 0));
        BuildLoading();
    }

    // ---------- 로딩 화면 (씬 전환 사이: 탐험 출발 · 방공호 귀환 · 메인 메뉴로 나가기) ----------
    // 검은 판 위에 옅은 긁힘, 메인 메뉴 제목 묶음과 같은 꼴(핏빛 세로 막대 + 제목 + 영문 꼬리표 + 가는 선), 왼쪽에서 오른쪽으로 흐르는 붉은 막대 조각,
    // 낡은 형광등처럼 깜빡이는 한 줄 메모 (2026-11-05 저녁, 메인 메뉴와 통일)

    GameObject loadingRoot;
    UnityEngine.UI.Text loadingTitle, loadingTip, loadingTag;
    RectTransform loadingBar;
    float loadingT;
    static readonly string[] LoadingTips =
    {
        "시간이 다 되기 전에 해치로 돌아올 것.",
        "짐이 들 수 있는 무게의 80%를 넘으면 걸음이 느려진다.",
        "길바닥에는 돌뿐이다. 쓸 만한 건 건물 안에 있다.",
        "달리면 소리가 커진다. 놈들은 소리를 듣는다.",
        "손전등 배터리는 생각보다 빨리 닳는다.",
        "발전기 전력이 0이 되면 방폭문이 열린다.",
        "앉으면 판자로 막힌 낮은 틈을 지나갈 수 있다.",
        "무거운 완성품은 창고에 들어간다.",
    };

    void BuildLoading()
    {
        var rt = NewRect("Loading", root);
        Stretch(rt);
        loadingRoot = rt.gameObject;
        var bg = loadingRoot.AddComponent<Image>();
        bg.color = new Color(0.012f, 0.012f, 0.014f, 1f);
        var grime = NewRect("Grime", rt).gameObject.AddComponent<Image>();
        Stretch(grime.rectTransform);
        grime.sprite = UIStyle.Grime;
        grime.type = Image.Type.Tiled;
        grime.pixelsPerUnitMultiplier = 0.4f; // 무늬를 키워 화면 전체에서 되풀이가 눈에 띄지 않게
        grime.color = new Color(1, 1, 1, 0.22f);
        grime.raycastTarget = false;
        var shade = NewRect("Shade", rt).gameObject.AddComponent<Image>();
        Stretch(shade.rectTransform);
        shade.sprite = UIStyle.Vignette;
        shade.color = new Color(0, 0, 0, 0.7f);
        shade.raycastTarget = false;

        var group = NewRect("Group", rt);
        Place(group, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(620, 150));
        var mark = NewRect("TitleMark", group);
        Place(mark, new Vector2(0, 1), new Vector2(0, -2), new Vector2(6, 60));
        var markImg = mark.gameObject.AddComponent<Image>();
        markImg.color = MenuRed;
        markImg.raycastTarget = false;
        loadingTitle = NewText(group, "", 46, TextAnchor.MiddleLeft, Text, FontTitle);
        Place(loadingTitle.rectTransform, new Vector2(0, 1), new Vector2(24, 0), new Vector2(590, 64));
        loadingTitle.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        loadingTag = NewText(group, "", 15, TextAnchor.UpperLeft, PaleDim, FontMono);
        Place(loadingTag.rectTransform, new Vector2(0, 1), new Vector2(28, -66), new Vector2(580, 22));
        var rule = NewRect("Rule", group);
        Place(rule, new Vector2(0, 1), new Vector2(26, -96), new Vector2(560, 1));
        var ruleImg = rule.gameObject.AddComponent<Image>();
        ruleImg.color = new Color(1, 1, 1, 0.14f);
        ruleImg.raycastTarget = false;
        var track = NewRect("Track", group);
        Place(track, new Vector2(0, 1), new Vector2(26, -108), new Vector2(560, 3));
        var trackImg = track.gameObject.AddComponent<Image>();
        trackImg.color = new Color(1, 1, 1, 0.06f);
        trackImg.raycastTarget = false;
        track.gameObject.AddComponent<RectMask2D>();
        loadingBar = NewRect("Fill", track);
        loadingBar.anchorMin = new Vector2(0, 0);
        loadingBar.anchorMax = new Vector2(0, 1);
        loadingBar.pivot = new Vector2(0, 0.5f);
        loadingBar.sizeDelta = new Vector2(160, 0);
        var fill = loadingBar.gameObject.AddComponent<Image>();
        fill.sprite = UIStyle.HorizontalFade;
        fill.color = new Color(0.85f, 0.14f, 0.1f);
        fill.raycastTarget = false;
        loadingTip = NewText(rt, "", 19, TextAnchor.MiddleCenter, Dim);
        loadingTip.fontStyle = FontStyle.Italic;
        Place(loadingTip.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 84), new Vector2(1200, 30));
        var ver = NewText(rt, "Ver " + Application.version, 15, TextAnchor.LowerRight, new Color(1, 1, 1, 0.35f));
        Place(ver.rectTransform, new Vector2(1, 0), new Vector2(-16, 8), new Vector2(300, 24));
        loadingRoot.SetActive(false);
    }

    public void ShowLoading(string title)
    {
        loadingTitle.text = title;
        loadingTag.text = title.Contains("방공호") ? "RETURNING  ·  SHELTER" : title.Contains("도시") ? "DEPARTING  ·  CITY" : title.Contains("메뉴") ? "RETURNING  ·  MAIN MENU" : "LOADING";
        loadingRoot.transform.SetAsLastSibling(); // 검은 막보다 위에 (창·컷씬이 열릴 때마다 막이 맨 위로 가므로 보일 때마다 다시 올린다)
        loadingTip.text = LoadingTips[UnityEngine.Random.Range(0, LoadingTips.Length)];
        loadingT = 0;
        loadingRoot.SetActive(true);
    }

    public void HideLoading() => loadingRoot.SetActive(false);

    void UpdateLoading()
    {
        if (loadingRoot == null || !loadingRoot.activeSelf) return;
        loadingT += Time.unscaledDeltaTime;
        float k = Mathf.Repeat(loadingT * 0.55f, 1f);
        loadingBar.anchoredPosition = new Vector2(Mathf.Lerp(-160, 560, k), 0);
        float flicker = Mathf.PerlinNoise(loadingT * 9f, 0.3f) > 0.84f ? 0.3f : 1f;
        loadingTip.color = new Color(Dim.r, Dim.g, Dim.b, flicker);
    }

    // 블러용 렌더 텍스처를 내린다
    void OnDestroy() => ReleaseBlur();

    // 빌드된 게임에서 창을 내리거나 다른 프로그램으로 가면 자동으로 일시정지한다
    void OnApplicationFocus(bool focus)
    {
        if (!focus && !Application.isEditor) SetPaused(true);
    }

    public void OnSceneLoaded(string sceneName)
    {
        SetPaused(false);
        ClosePatchNotes();
        if (journalRoot != null) journalRoot.SetActive(false);
        CloseWorkbench();
        CloseInventory();
        CloseTruck();
        HideCutscene();
        if (sceneName == GameManager.MenuScene) ShowMainMenu();
        else { HideMainMenu(); ShowKeyHints(); }
        hotbarSelected = int.MinValue;
        RestartHudIntro();
    }

    // ---------- HUD ----------

    void BuildHud()
    {
        hud = NewRect("HUD", root).gameObject;
        Stretch((RectTransform)hud.transform);
        var h = hud.transform;

        // 왼쪽 위: 상태 막대 / 오른쪽 위: 남은 시간·턴 계기판 (UIManager.Hud)
        BuildStatusPanel(h);
        BuildTimePanel(h);
        BuildWeatherPanel(h);
        BuildObjectivePanel(h);
        BuildCompass(h);
        BuildKeyHints(h);
        BuildEnemyBars(h); // 적 이름·체력바 (UIManager.EnemyBars)

        // 오른쪽 아래: 손전등·배터리 — 세로 두 줄([손전등 그림] 100% / [배터리 그림] 1), 뒤에 먹이 번진 자국(50%)
        var flash = NewRect("Flashlight", h);
        Place(flash, new Vector2(1, 0), new Vector2(-28, 48), new Vector2(156, 92));
        flashBox = flash.gameObject;
        var ink = NewRect("Ink", flash).gameObject.AddComponent<Image>();
        Place(ink.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-6, -2), new Vector2(300, 176));
        ink.rectTransform.localRotation = Quaternion.Euler(0, 0, -3f);
        ink.sprite = UIStyle.InkWash;
        ink.material = GlassMaterial(); // (2026-11-06부터 null: 비추지 않는 보통 반투명 먹 자국)
        ink.color = new Color(0.08f, 0.08f, 0.09f, 0.62f);
        ink.raycastTarget = false;
        flashIcon = HudItemRow(flash, "flashlight", 0, out flashlightText);
        batteryIcon = HudItemRow(flash, "battery", 1, out batteryText);

        // 가운데 아래: 핫바 — 메인 메뉴 '최근 저장' 판과 같은 흙색 더러운 강판 위에 어두운 홈 8개 (2026-11-05 저녁, 유리 알약에서 바꿈)
        const float slot = 68, gap = 8;
        float barWidth = SlotCount * slot + (SlotCount - 1) * gap;
        var tray = HudOlivePlate(h, "HotbarTray");
        Place(tray, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(barWidth + 28, slot + 32));
        hotbarTray = tray;
        var bar = NewRect("Hotbar", h);
        Place(bar, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(barWidth, slot)); // 고른 칸이 살짝 떠올라도 받침 테두리를 넘지 않게
        hotbarBar = bar;
        for (int i = 0; i < SlotCount; i++)
        {
            var v = NewItemSlot(bar, "Slot" + i, 7, true);
            var s = v.Rect;
            s.anchorMin = s.anchorMax = new Vector2(0, 0.5f);
            s.pivot = new Vector2(0.5f, 0.5f); // 고른 칸이 가운데를 기준으로 커지게
            s.anchoredPosition = new Vector2(i * (slot + gap) + slot / 2, 0);
            s.sizeDelta = new Vector2(slot, slot);
            hotbarSlots.Add(v);

            // 고른 칸: 호박색 테두리 + 칸 뒤에서 번지는 호박빛
            var sel = NewRect("Select", s).gameObject.AddComponent<Image>();
            Stretch(sel.rectTransform, -3, -3);
            sel.sprite = UIStyle.Frame;
            sel.type = Image.Type.Sliced;
            sel.pixelsPerUnitMultiplier = 0.7f; // 테두리를 조금 굵게
            sel.color = Accent;
            sel.raycastTarget = false;
            slotSelects.Add(sel);
            var halo = NewRect("Halo", s).gameObject.AddComponent<Image>();
            Place(halo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(slot + 36, slot + 36));
            halo.sprite = UIStyle.Glow;
            halo.color = new Color(Accent.r, Accent.g, Accent.b, 0.32f);
            halo.raycastTarget = false;
            halo.transform.SetAsFirstSibling();
            slotGlows.Add(halo);

            var key = NewText(s, (i + 1).ToString(), 14, TextAnchor.UpperLeft, PaleDim, FontMono);
            Stretch(key.rectTransform, 6, 3);
            key.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            slotKeys.Add(key);
            var count = NewText(s, "", 17, TextAnchor.LowerRight, Pale, FontSemiBold);
            Stretch(count.rectTransform, 6, 3);
            count.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            slotCounts.Add(count);
        }
        itemNameText = NewText(h, "", 20, TextAnchor.MiddleCenter, Pale);
        Place(itemNameText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 112), new Vector2(900, 30));
        itemNameText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);

        // 가운데: 조준점, 상호작용 안내와 알림 (테두리 없는 검은 띠 + 호박색 밑줄, 자막 느낌)
        BuildCrosshair(h);

        promptBox = BlackBand(h, "Prompt");
        Place(promptBox, new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(300, 46));
        promptText = NewText(promptBox, "", 23, TextAnchor.MiddleCenter, Text);
        Stretch(promptText.rectTransform);
        promptBox.gameObject.SetActive(false);

        // 알림 띠: 글 길이에 맞게 줄어든다 (FitToast)
        var toast = BlackBand(h, "Toast");
        Place(toast, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(520, 46));
        toastRect = toast;
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0;
        toastGroup.blocksRaycasts = false;
        toastText = NewText(toast, "", 20, TextAnchor.MiddleCenter, Text);
        Stretch(toastText.rectTransform, 24, 4);
    }

    // 검은 띠 (알림·상호작용 안내): 테두리 없는 어두운 띠 + 아래 가운데 호박색 밑줄
    static RectTransform BlackBand(Transform parent, string name)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Fill;
        img.type = Image.Type.Sliced;
        img.color = new Color(0, 0, 0, 0.78f);
        img.raycastTarget = false;
        var line = NewRect("Underline", rt);
        line.anchorMin = new Vector2(0.25f, 0);
        line.anchorMax = new Vector2(0.75f, 0);
        line.offsetMin = new Vector2(0, 4);
        line.offsetMax = new Vector2(0, 6);
        var li = line.gameObject.AddComponent<Image>();
        li.color = new Color(Accent.r, Accent.g, Accent.b, 0.9f);
        li.raycastTarget = false;
        return rt;
    }

    // HUD에 바탕 없이 띄우는 아이템 그림 (노란 윤곽선, 40px). x = 부모 왼쪽에서의 자리
    static Image HudItemIcon(RectTransform parent, string itemId, float x)
    {
        var rt = NewRect("Icon_" + itemId, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = new Vector2(40, 40);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = ItemIcons.Outlined(itemId, Accent);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    // 손전등·배터리 줄 하나: 부모 위에서 row번째 줄에 [그림] 숫자. 숫자 글자는 value로 돌려준다
    static Image HudItemRow(RectTransform parent, string itemId, int row, out UnityEngine.UI.Text value)
    {
        var rt = NewRect("Row_" + itemId, parent);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -row * 46);
        rt.sizeDelta = new Vector2(0, 44);
        var icon = HudItemIcon(rt, itemId, 10);
        value = HudValueText(rt, 58, 90);
        return icon;
    }

    // HUD 숫자 글자 (모노 글꼴, 검은 테두리로 바탕 없이도 읽힌다)
    static UnityEngine.UI.Text HudValueText(RectTransform parent, float x, float width)
    {
        var t = NewText(parent, "", 22, TextAnchor.MiddleLeft, Pale, FontMono);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 0.5f);
        t.rectTransform.pivot = new Vector2(0, 0.5f);
        t.rectTransform.anchoredPosition = new Vector2(x, 0);
        t.rectTransform.sizeDelta = new Vector2(width, 32);
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        return t;
    }

    // HUD용 명판: 녹슨 강철판(타일) + 어두운 테두리 + 네 귀퉁이 나사
    static RectTransform HudPlate(Transform parent, string name, Color frame)
    {
        var rt = Box(parent, name, HudFill, frame);
        var img = rt.GetComponent<Image>();
        img.sprite = UIStyle.Plate;
        img.type = Image.Type.Tiled;
        img.raycastTarget = false;
        // 가장자리로 갈수록 어두운 그늘 (판이 두껍게 보인다)
        var shade = NewRect("Shade", rt);
        Stretch(shade, 1, 1);
        var si = shade.gameObject.AddComponent<Image>();
        si.sprite = UIStyle.Vignette;
        si.color = new Color(0, 0, 0, 0.42f);
        si.raycastTarget = false;
        shade.SetSiblingIndex(0);
        Screws(rt);
        return rt;
    }

    // HUD 유리판 (2026-11-05): 뒤 화면을 흐려 비추는 어두운 유리 — 가장자리는 투명하고 가운데로 갈수록 진해 글·막대·그림이 또렷하다.
    // 판 자체에는 그림이 없고 자식 Glass가 pad만큼 밖으로 넓게 깔린다. Frame은 경고 때만 붉게 켜지는 둥근 테두리 (UpdateTimePanel)
    static RectTransform HudGlass(Transform parent, string name, float pad = 18f)
    {
        var rt = NewRect(name, parent);
        var glass = NewRect("Glass", rt).gameObject.AddComponent<Image>();
        Stretch(glass.rectTransform, -pad, -pad);
        glass.sprite = UIStyle.GlassSoft;
        glass.type = Image.Type.Sliced;
        glass.pixelsPerUnitMultiplier = 1.25f; // 페더 32px
        glass.material = GlassMaterial();
        glass.color = HudGlassTint;
        glass.raycastTarget = false;
        var frame = NewRect("Frame", rt).gameObject.AddComponent<Image>();
        Stretch(frame.rectTransform, -4, -4);
        frame.sprite = UIStyle.Frame;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 0.5f;
        frame.color = Color.clear;
        frame.raycastTarget = false;
        return rt;
    }

    // 알약 유리판 (핫바, 리퀴드 글래스): 부드러운 그림자 → 뒤 화면을 흐려 비추며 가장자리가 굴절되는 유리 → 테두리 반사선·윗면 광택
    static RectTransform HudPill(Transform parent, string name)
    {
        var rt = NewRect(name, parent);
        var shadow = NewRect("Shadow", rt).gameObject.AddComponent<Image>();
        Stretch(shadow.rectTransform, -14, -14);
        shadow.rectTransform.anchoredPosition = new Vector2(0, -8);
        shadow.sprite = UIStyle.GlassSoft;
        shadow.type = Image.Type.Sliced;
        shadow.pixelsPerUnitMultiplier = 1.6f;
        shadow.color = new Color(0, 0, 0, 0.5f);
        shadow.raycastTarget = false;
        var glass = NewRect("Glass", rt).gameObject.AddComponent<Image>();
        Stretch(glass.rectTransform);
        glass.sprite = UIStyle.GlassPill;
        glass.type = Image.Type.Sliced;
        glass.material = PillMaterial();
        glass.color = HudPillTint;
        glass.raycastTarget = false;
        var light = NewRect("Light", rt).gameObject.AddComponent<Image>();
        Stretch(light.rectTransform);
        light.sprite = UIStyle.GlassLight;
        light.type = Image.Type.Sliced;
        light.color = new Color(1, 1, 1, 0.35f);
        light.raycastTarget = false;
        return rt;
    }

    // 핫바 판 (2026-11-05 저녁): 메인 메뉴 '최근 저장' 정보 판과 같은 흙색 더러운 강판 — 아래 부드러운 그림자, 흙먼지·긁힘·가장자리 그늘, 칠이 벗겨진 가는 테두리
    static RectTransform HudOlivePlate(Transform parent, string name)
    {
        var rt = NewRect(name, parent);
        var shadow = NewRect("Shadow", rt).gameObject.AddComponent<Image>();
        Stretch(shadow.rectTransform, -12, -12);
        shadow.rectTransform.anchoredPosition = new Vector2(0, -6);
        shadow.sprite = UIStyle.GlassSoft;
        shadow.type = Image.Type.Sliced;
        shadow.pixelsPerUnitMultiplier = 1.6f;
        shadow.color = new Color(0, 0, 0, 0.45f);
        shadow.raycastTarget = false;
        var plate = NewRect("Plate", rt);
        Stretch(plate);
        var fill = plate.gameObject.AddComponent<Image>();
        fill.sprite = UIStyle.Fill;
        fill.type = Image.Type.Sliced;
        fill.color = new Color(InfoFill.r, InfoFill.g, InfoFill.b, 0.88f);
        fill.raycastTarget = false;
        var frame = DirtyPlate(plate, 0.8f, 0.7f);
        frame.color = new Color(1, 1, 1, 0.2f);
        return rt;
    }

    // 유리 재질 (Resources/Shaders/HudGlass.shader): _Blur 흐림 간격(텍셀), _Darken 바탕색 비율, _Refract 가장자리 굴절.
    // 셰이더가 없으면 null을 돌려주고 보통 반투명 그림으로 그린다
    // HUD 판은 뒤 화면을 비추지 않는다 (2026-11-06): 뛰며 돌아볼 때 흐린 뒤 배경이 번들거리며 반짝여 몰입을 깬다는 의견 → 기본 UI 재질의 보통 반투명 그림으로.
    // 흐림 유리(BunkerLab/HudGlass)는 NewGlassMaterial로 남겨 두되 HUD 판(시간·날씨·할 일·상태·손전등 먹 자국·적 체력바)에는 쓰지 않는다
    static Material GlassMaterial() => null;
    static Material PillMaterial() => hudPillMat != null ? hudPillMat : hudPillMat = NewGlassMaterial("HudPill", 1.6f, 0.5f, 0.06f);

    static Material NewGlassMaterial(string name, float blur, float darken, float refract)
    {
        var shader = Shader.Find("BunkerLab/HudGlass");
        if (shader == null)
        {
            Debug.LogWarning("[UIManager] BunkerLab/HudGlass 셰이더를 찾지 못해 HUD 유리판을 보통 그림으로 그린다.");
            return null;
        }
        var m = new Material(shader) { name = name };
        m.SetFloat("_Blur", blur);
        m.SetFloat("_Darken", darken);
        m.SetFloat("_Refract", refract);
        return m;
    }

    // 뒤 화면 텍스처(URP 불투명 텍스처)를 만들지 않는 파이프라인 설정이면 유리가 바탕색만 칠하게 한다 (0.5초마다 확인)
    float glassCheckTimer;

    void UpdateGlassMaterials()
    {
        glassCheckTimer -= Time.unscaledDeltaTime;
        if (glassCheckTimer > 0) return;
        glassCheckTimer = 0.5f;
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        float backdrop = urp != null && urp.supportsCameraOpaqueTexture ? 1f : 0f;
        if (hudGlassMat != null) hudGlassMat.SetFloat("_Backdrop", backdrop);
        if (hudPillMat != null) hudPillMat.SetFloat("_Backdrop", backdrop);
    }

    // 명판 귀퉁이의 나사 네 개 (제각각 조금씩 돌아가 있다)
    static void Screws(RectTransform plate, float inset = 9f)
    {
        for (int i = 0; i < 4; i++)
        {
            int ax = i % 2, ay = i / 2;
            var s = NewRect("Screw", plate);
            s.anchorMin = s.anchorMax = new Vector2(ax, ay);
            s.pivot = new Vector2(0.5f, 0.5f);
            s.anchoredPosition = new Vector2(ax == 0 ? inset : -inset, ay == 0 ? inset : -inset);
            s.sizeDelta = new Vector2(10, 10);
            s.localRotation = Quaternion.Euler(0, 0, 37f * i + 20f);
            var img = s.gameObject.AddComponent<Image>();
            img.sprite = UIStyle.Screw;
            img.raycastTarget = false;
        }
    }

    // 테이프 조각을 붙인다 (핫바의 고른 칸): anchor 자리에서 pos만큼 옮긴 가운데, size 크기, angle도 기울여
    static Image TapeStrip(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, float angle, Color color)
    {
        var t = NewRect("Tape", parent);
        t.anchorMin = t.anchorMax = anchor;
        t.pivot = new Vector2(0.5f, 0.5f);
        t.anchoredPosition = pos;
        t.sizeDelta = size;
        t.localRotation = Quaternion.Euler(0, 0, angle);
        var img = t.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Tape;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 유리 위의 긁힘·먼지 얼룩과 가장자리 그늘 (창·메뉴 기둥). index 자리에 끼워 내용물 아래에 깔린다
    static void Grime(RectTransform box, float amount = 1f, int index = 0)
    {
        var g = NewRect("Grime", box);
        Stretch(g, 1, 1);
        var gi = g.gameObject.AddComponent<Image>();
        gi.sprite = UIStyle.Grime;
        gi.type = Image.Type.Tiled;
        gi.color = new Color(1, 1, 1, 0.4f * amount);
        gi.raycastTarget = false;
        g.SetSiblingIndex(index);
        var v = NewRect("Shade", box);
        Stretch(v, 1, 1);
        var vi = v.gameObject.AddComponent<Image>();
        vi.sprite = UIStyle.Vignette;
        vi.color = new Color(0, 0, 0, 0.45f * amount);
        vi.raycastTarget = false;
        v.SetSiblingIndex(index + 1);
    }

    // 유리판 윗면의 얇은 반사광 (창·HUD 공통)
    static void GlassEdge(RectTransform box, float alpha = 0.08f)
    {
        var edge = NewRect("Edge", box);
        edge.anchorMin = new Vector2(0, 1);
        edge.anchorMax = new Vector2(1, 1);
        edge.pivot = new Vector2(0.5f, 1);
        edge.anchoredPosition = new Vector2(0, -1);
        edge.sizeDelta = new Vector2(-18, 1);
        var img = edge.gameObject.AddComponent<Image>();
        img.color = new Color(1, 1, 1, alpha);
        img.raycastTarget = false;
    }

    // 상자 왼쪽 안쪽의 금색 세로 막대 (알림 등)
    static void AccentBar(RectTransform box)
    {
        var bar = NewRect("AccentBar", box);
        bar.anchorMin = new Vector2(0, 0);
        bar.anchorMax = new Vector2(0, 1);
        bar.pivot = new Vector2(0, 0.5f);
        bar.offsetMin = new Vector2(8, 14);
        bar.offsetMax = new Vector2(11, -14);
        var img = bar.gameObject.AddComponent<Image>();
        img.color = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
        img.raycastTarget = false;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var s = gm.State;

        UpdateLoading();
        WatchItemDrag(); // 끌던 그림이 남지 않도록 버튼이 떼어졌는지 매 프레임 본다
        UpdateTipKnob();
        bool inField = PlayerController.Instance != null && !gm.IsGameOver && boxTarget < 0.5f; // 죽거나 엔딩이면, 그리고 출입 연출 동안은 HUD를 치운다
        if (inField && !hud.activeSelf) ShowKeyHints(); // HUD가 켜지는 순간(씬에 들어온 뒤) 조작키를 잠깐 보여 준다
        hud.SetActive(inField);
        if (hud.activeSelf)
        {
            UpdateHud(gm, s);
            UpdateCrosshair();
            AnimateHud();
            UpdateGlassMaterials();
            UpdateEnemyBars();
        }

        if (toastTimer > 0)
        {
            toastTimer -= Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Clamp01(toastTimer * 2);
        }

        // 피격 효과 + 체력이 낮을 때 붉은 맥박. 일시정지·메뉴·컷씬처럼 게임을 하고 있지 않을 때는 보이지 않는다
        damageAlpha = Mathf.MoveTowards(damageAlpha, 0, Time.unscaledDeltaTime * 1.2f);
        hitArcAlpha = Mathf.MoveTowards(hitArcAlpha, 0, Time.unscaledDeltaTime * 1.5f);
        hitArc.color = new Color(0.85f, 0.06f, 0.02f, hitArcAlpha);
        bool inGame = PlayerController.Instance != null && !gm.IsGameOver && !IsPaused && !IsCutscenePlaying && !gm.InMenu;
        if (!inGame) damageAlpha = 0;
        float lowHealth = s.Health < 30 && inGame ? (0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 6)) * (1 - s.Health / 30f) : 0;
        damageOverlay.color = new Color(0.45f, 0, 0, Mathf.Max(damageAlpha, lowHealth));
        // 붉은 밤: 화면 가장자리가 붉게 고동친다
        float rn = inGame ? redNight : 0;
        redNightOverlay.color = new Color(0.55f, 0.03f, 0.02f, rn * (0.55f + 0.2f * Mathf.Sin(Time.unscaledTime * 2.4f)));

        // FPS 표시 (설정에서 켜고 끈다)
        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f)
        {
            fpsText.text = GameSettings.ShowFps ? $"{fpsFrames / fpsTimer:0} FPS" : "";
            fpsFrames = 0;
            fpsTimer = 0;
        }

        UpdateSleepText();
        UpdateLetterbox();
        KeepPauseOnTop(); // 레터박스 띠가 일시정지 창을 가리지 않게 (UIManager.Menus)
        UpdateJournal();
        if (IsPanelOpen) UpdatePanel();
        if (IsWorkbenchOpen) UpdateWorkbench();
        if (IsCutscenePlaying) UpdateCutscene();
        HandleKeys(gm);
        UpdateGlass(); // 키로 창을 닫은 프레임에도 아래 창의 블러가 낡은 텍스처를 그리지 않게 키 처리 뒤에 한다

        bool needCursor = IsBlocking || gm.IsGameOver || PlayerController.Instance == null;
        Cursor.lockState = needCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = needCursor;
    }

    void UpdateHud(GameManager gm, GameState s)
    {
        UpdateStatusPanel(s);
        UpdateTimePanel(gm, s);
        UpdateWeatherPanel();
        UpdateObjectivePanel();
        UpdateCompass();
        Tutorial.RefreshBeacon();

        bool hasLight = s.Carried.Count("flashlight") > 0;
        flashBox.SetActive(hasLight);
        if (hasLight)
        {
            // 손전등이 켜져 있으면 그림이 밝고, 꺼져 있으면 흐리다
            if (flashlight == null) flashlight = FindFirstObjectByType<PlayerFlashlight>();
            bool lit = flashlight != null && flashlight.IsOn;
            flashIcon.color = lit ? Color.white : new Color(1, 1, 1, 0.55f);
            int key = Mathf.RoundToInt(s.FlashlightCharge) * 10000 + s.Carried.Count("battery");
            if (key != flashKey)
            {
                flashKey = key;
                flashlightText.text = $"{s.FlashlightCharge:0}%";
                flashlightText.color = s.FlashlightCharge < 20 ? Alert : Pale;
                if (s.FlashlightCharge < 20) Tutorial.Hint(Tutorial.HintBattery, $"손전등 배터리가 거의 없다. 다 되면 {KeyBindings.Label(GameAction.Reload)} 키를 눌러 배터리를 갈아 끼운다.");
                batteryText.text = s.Carried.Count("battery").ToString();
            }
        }

        UpdateHotbar(s);
    }

    readonly List<KeyValuePair<string, int>> hotbarItems = new List<KeyValuePair<string, int>>(SlotCount);

    bool hotbarTipsShown = true; // 핫바 위 줄을 그릴 때의 Tip 설정 (바뀌면 줄을 다시 쓴다)

    void UpdateHotbar(GameState s)
    {
        var hotbar = PlayerHotbar.Instance;
        hotbarItems.Clear();
        for (int i = 0; i < SlotCount; i++)
        {
            string id = s.Hotbar[i];
            hotbarItems.Add(new KeyValuePair<string, int>(id, id != null ? s.Carried.Count(id) : 0));
        }
        int selected = hotbar != null ? hotbar.SelectedIndex : -1;
        // 고른 칸이 바뀌면 부드럽게 커지고 작아진다 (매 프레임)
        for (int i = 0; i < SlotCount; i++)
        {
            float target = i == selected ? 1.1f : 1f;
            var rt = hotbarSlots[i].Rect;
            float sc = Mathf.Lerp(rt.localScale.x, target, Time.unscaledDeltaTime * 14f);
            rt.localScale = Vector3.one * sc;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, Mathf.Lerp(rt.anchoredPosition.y, i == selected ? 4f : 0f, Time.unscaledDeltaTime * 14f));
        }
        // 새총을 들고 있으면 남은 발 수가 바뀔 때도 다시 쓴다
        if (selected == hotbarSelected && s.SlingShots == hotbarSlingShots && hotbarTipsShown == GameSettings.Tips && SameHotbarItems(hotbarItems)) return;
        // 칸에 아이템이 새로 들어오거나 개수가 늘면 그 칸이 톡 튄다
        for (int i = 0; i < SlotCount && hotbarShown.Count == SlotCount; i++)
            if (hotbarItems[i].Key != null && (hotbarItems[i].Key != hotbarShown[i].Key || hotbarItems[i].Value > hotbarShown[i].Value))
                hotbarSlots[i].Rect.localScale = Vector3.one * 1.28f;
        hotbarSelected = selected;
        hotbarSlingShots = s.SlingShots;
        hotbarTipsShown = GameSettings.Tips;
        hotbarShown.Clear();
        hotbarShown.AddRange(hotbarItems);

        for (int i = 0; i < SlotCount; i++)
        {
            bool has = hotbarItems[i].Key != null;
            bool isSel = i == selected;
            SetItemSlot(hotbarSlots[i], hotbarItems[i].Key);
            slotCounts[i].text = has && hotbarItems[i].Value > 1 ? hotbarItems[i].Value.ToString() : "";
            slotGlows[i].enabled = slotSelects[i].enabled = isSel;
            slotKeys[i].color = isSel ? Accent : PaleDim;
        }

        if (selected >= 0 && hotbarItems[selected].Key != null)
        {
            var def = ItemDatabase.Get(hotbarItems[selected].Key);
            // 쓰는 법(팁)은 Tip 스위치가 켜져 있을 때만 이름 옆에 붙는다
            string action = GameSettings.Tips ? $"   <color=#a8a294>{PlayerHotbar.ActionHint(def.Id)}</color>" : "";
            itemNameText.text = $"{ColoredName(def.Id)} x{hotbarItems[selected].Value}   <size=17>{RarityTag(def.Rarity)}{action}</size>";
        }
        else itemNameText.text = ""; // ('핫바에 없는 아이템 n종 · E에서 끌어다 놓기' 안내 줄은 뺐다 — 2026-11-05 저녁)
    }

    // 그려 둔 핫바와 같은 아이템·개수인가
    bool SameHotbarItems(List<KeyValuePair<string, int>> items)
    {
        if (items.Count != hotbarShown.Count) return false;
        for (int i = 0; i < items.Count; i++)
            if (items[i].Value != hotbarShown[i].Value || items[i].Key != hotbarShown[i].Key) return false;
        return true;
    }

    void HandleKeys(GameManager gm)
    {
        var kb = Keyboard.current;
        if (kb == null || gm.IsTransitioning) return;
        bool inGame = PlayerController.Instance != null && !gm.IsGameOver;

        // 설정 → 컨트롤: 새 키를 기다리는 중이면 다음에 누른 키를 그 동작에 정한다 (Esc는 취소)
        if (rebinding.HasValue)
        {
            if (!IsPanelOpen) { rebinding = null; return; }
            if (kb.escapeKey.wasPressedThisFrame) { rebinding = null; RefreshPanel(); return; }
            int code = KeyBindings.ReadAnyPressed();
            if (code >= 0)
            {
                KeyBindings.Set(rebinding.Value, code);
                rebinding = null;
                ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.5f, 1f, SoundCategory.UI);
                RefreshPanel();
            }
            return;
        }

        // 탐색 결과: 아무 키나 누르면 (적는 중이면 다 적고, 다 적혔으면) 넘어간다
        if (IsJournalOpen)
        {
            if (kb.anyKey.wasPressedThisFrame) JournalClick();
            return;
        }

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (IsPatchNotesOpen) ClosePatchNotes();
            else if (IsWorkbenchOpen && !IsPaused) CloseWorkbench();
            else if (IsInventoryOpen && !IsPaused) CloseInventory();
            else if (IsTruckOpen && !IsPaused) CloseTruck();
            else if (IsPanelOpen) { if (panelCloseButton.activeSelf) ClosePanel(); }
            else if (inGame) SetPaused(!IsPaused);
        }
        else if (KeyBindings.Pressed(GameAction.Inventory) && inGame && !IsPaused)
        {
            if (IsInventoryOpen) CloseInventory();
            else if (!IsPanelOpen && !IsWorkbenchOpen && !IsTruckOpen) OpenInventory();
        }
    }

    public void SetPrompt(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            promptBox.gameObject.SetActive(false);
            return;
        }
        if (text != promptShown || !promptBox.gameObject.activeSelf) promptPop = 0;
        promptShown = text;
        promptText.text = text;
        promptBox.sizeDelta = new Vector2(promptText.preferredWidth + 56, 46);
        promptBox.gameObject.SetActive(true);
    }

    public void ShowMessage(string text, float duration = 2.5f)
    {
        toastText.text = text;
        toastTimer = duration;
        FitToast();
    }

    // 알림 띠를 글에 맞게 줄인다: 한 줄이면 글 너비만큼, 길면 최대 너비에서 줄을 접고 높이를 늘린다
    const float ToastMaxWidth = 980f, ToastMinWidth = 220f, ToastHeight = 46f;

    void FitToast()
    {
        if (toastRect == null) return;
        float need = toastText.preferredWidth + 60f;
        float w = Mathf.Clamp(need, ToastMinWidth, ToastMaxWidth), h = ToastHeight;
        if (need > ToastMaxWidth)
        {
            var settings = toastText.GetGenerationSettings(new Vector2(ToastMaxWidth - 48f, 0));
            h = Mathf.Max(ToastHeight, toastText.cachedTextGeneratorForLayout.GetPreferredHeight(toastText.text, settings) / toastText.pixelsPerUnit + 18f);
        }
        toastRect.sizeDelta = new Vector2(w, h);
    }

    // 붉은 밤 화면 효과 (0 = 없음, 1 = 가득)
    public void SetRedNight(float amount) => redNight = Mathf.Clamp01(amount);

    public void FlashDamage(float strength)
    {
        damageAlpha = Mathf.Max(damageAlpha, 0.25f + 0.35f * strength);
        statusShake = Mathf.Max(statusShake, 0.5f + strength);
        // 맞은 방향 (카메라 앞 = 화면 위)
        if (LastHitFrom.HasValue && Camera.main != null)
        {
            var cam = Camera.main.transform;
            var to = LastHitFrom.Value - cam.position;
            to.y = 0;
            var fwd = cam.forward;
            fwd.y = 0;
            hitArc.rectTransform.localRotation = Quaternion.Euler(0, 0, -Vector3.SignedAngle(fwd, to, Vector3.up));
            hitArcAlpha = 0.9f;
            LastHitFrom = null;
        }
    }

    // 잠잘 때 검은 화면 가운데 글자 (null이면 감춘다)
    UnityEngine.UI.Text sleepText;
    float sleepTextAlpha, sleepTextTarget;

    public void ShowSleepText(string text)
    {
        if (sleepText == null)
        {
            sleepText = NewText(root, "", 40, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.95f, 0), FontSemiBold);
            Place(sleepText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 80));
        }
        sleepText.transform.SetAsLastSibling();
        if (!string.IsNullOrEmpty(text)) sleepText.text = text;
        sleepTextTarget = string.IsNullOrEmpty(text) ? 0 : 1;
    }

    void UpdateSleepText()
    {
        if (sleepText == null) return;
        sleepTextAlpha = Mathf.MoveTowards(sleepTextAlpha, sleepTextTarget, Time.unscaledDeltaTime * 1.2f);
        sleepText.color = new Color(0.85f, 0.85f, 0.95f, sleepTextAlpha);
        // 천천히 떠오르며 흔들린다
        sleepText.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 1.3f) * 6f, Mathf.Sin(Time.unscaledTime * 0.9f) * 8f);
    }

    // 검은 막의 현재 투명도 (1 = 완전히 검다)
    public float FadeAlpha => fadeOverlay != null ? fadeOverlay.color.a : 0f;

    public IEnumerator Fade(float target, float duration)
    {
        float start = fadeOverlay.color.a;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            fadeOverlay.color = new Color(0, 0, 0, Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        fadeOverlay.color = new Color(0, 0, 0, target);
    }

    // ---------- 창 안 알림 띠 ----------
    // 창이 HUD 알림을 가리므로 같은 알림(toastText)을 창 안에 비춘다. "힌트" 알림은 머리글 '힌트 :'를 왼쪽에 고정하고
    // 본문은 줄 간격을 좁혀 두 줄까지 보여 준다 (띠 높이가 본문에 맞춰 늘어난다). 제작대·소지품·일반 창이 같이 쓴다 (2026-11-05)
    const string HintPrefix = "<color=#d8a523>힌트</color>  ";

    class NoticeBand
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public UnityEngine.UI.Text Label, Body;
        public float BaseHeight, LeftPad;
        public string Shown;
    }

    NoticeBand MakeNoticeBand(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, bool accentBar)
    {
        var box = Box(parent, "Notice", new Color(0.1f, 0.09f, 0.07f, 0.96f), new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        Place(box, anchor, pos, size);
        if (accentBar) AccentBar(box);
        var band = new NoticeBand { Rect = box, BaseHeight = size.y, LeftPad = accentBar ? 24 : 16 };
        band.Group = box.gameObject.AddComponent<CanvasGroup>();
        band.Group.alpha = 0;
        band.Group.blocksRaycasts = false;
        band.Label = NewText(box, "힌트 :", 18, TextAnchor.MiddleLeft, Accent, FontTitle);
        var lr = band.Label.rectTransform;
        lr.anchorMin = new Vector2(0, 0);
        lr.anchorMax = new Vector2(0, 1);
        lr.pivot = new Vector2(0, 0.5f);
        lr.offsetMin = new Vector2(band.LeftPad, 0);
        lr.offsetMax = new Vector2(band.LeftPad + 58, 0);
        band.Label.gameObject.SetActive(false);
        band.Body = NewText(box, "", 18, TextAnchor.MiddleLeft, Text);
        Stretch(band.Body.rectTransform, band.LeftPad, 2);
        band.Body.lineSpacing = 0.9f;
        return band;
    }

    // 매 프레임: HUD 알림과 같은 글·투명도. 글이 바뀔 때만 머리글·여백·높이를 다시 맞춘다
    void SyncNoticeBand(NoticeBand band)
    {
        if (band == null) return;
        band.Group.alpha = toastGroup.alpha;
        string raw = toastText.text;
        if (raw == band.Shown) return;
        band.Shown = raw;
        bool hint = raw.StartsWith(HintPrefix, StringComparison.Ordinal);
        band.Label.gameObject.SetActive(hint);
        band.Body.text = hint ? raw.Substring(HintPrefix.Length) : raw;
        float left = hint ? band.LeftPad + 62 : band.LeftPad;
        band.Body.rectTransform.offsetMin = new Vector2(left, 2);
        band.Body.rectTransform.offsetMax = new Vector2(-16, -2);
        float width = Mathf.Max(50, band.Rect.sizeDelta.x - left - 16);
        var settings = band.Body.GetGenerationSettings(new Vector2(width, 0));
        float h = band.Body.cachedTextGeneratorForLayout.GetPreferredHeight(band.Body.text, settings) / band.Body.pixelsPerUnit;
        band.Rect.sizeDelta = new Vector2(band.Rect.sizeDelta.x, Mathf.Max(band.BaseHeight, h + 14));
    }

    // ---------- 창(Panel): 흐린 배경 + 가운데 유리판 ----------

    void BuildPanel()
    {
        var p = NewRect("Panel", root);
        Stretch(p);
        panel = p.gameObject;

        // 뒤 화면 블러 + 어두운 막 (막이 뒤 화면 클릭도 막는다)
        panelBlur = BlurBackdrop(p, WindowTint);
        panelBlur.enabled = false;
        panelTint = p.GetChild(p.childCount - 1).GetComponent<Image>();

        // 창 몸통. 끝 화면은 핏자국 층과 함께 서서히 나타나므로 CanvasGroup(panelGroup)으로 묶는다 (어두운 막은 묶지 않아 바로 덮인다)
        var body = NewRect("Body", p);
        Stretch(body);
        panelGroup = body.gameObject.AddComponent<CanvasGroup>();
        BuildEndLayer(body);
        panelBox = PlateWindow(body, "Box");
        Place(panelBox, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelMinHeight));
        panelBox.gameObject.AddComponent<UIPopIn>();

        // 제목 줄: 제목 + 얇은 선(왼쪽은 금색) + 닫기 버튼
        var bar = NewRect("TitleBar", panelBox);
        Stretch(bar);
        panelTitleBar = bar.gameObject;
        panelTitle = NewText(bar, "", 36, TextAnchor.MiddleLeft, Text, FontTitle);
        var trt = panelTitle.rectTransform;
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0, 1);
        trt.offsetMin = new Vector2(44, -80);
        trt.offsetMax = new Vector2(-236, -24);
        var line = NewRect("TitleLine", bar);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, -100);
        line.sizeDelta = new Vector2(-72, 2);
        var lineImg = line.gameObject.AddComponent<Image>();
        lineImg.color = new Color(1, 1, 1, 0.12f);
        lineImg.raycastTarget = false;
        RedTitleBar(bar, new Vector2(30, -30), 48); // 제목 왼쪽의 핏빛 막대 (메인 메뉴 제목과 같은 꼴)
        // 제목 줄 오른쪽 끝의 영문 꼬리표 (SETTINGS · CONTROLS …, 메뉴 버튼의 꼬리표와 같은 꼴)
        panelTag = NewText(bar, "", 13, TextAnchor.MiddleRight, PaleDim, FontMono);
        Place(panelTag.rectTransform, new Vector2(1, 1), new Vector2(-204, -52), new Vector2(400, 20));

        var close = PlateButton(bar, "닫기", "ESC", ClosePanel);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-36, -28), new Vector2(150, 44));
        panelCloseButton = close.gameObject;

        // 내용 (휠로 스크롤)
        panelView = NewRect("View", panelBox);
        panelView.anchorMin = Vector2.zero;
        panelView.anchorMax = Vector2.one;
        panelView.offsetMin = new Vector2(44, PanelBottom);
        panelView.offsetMax = new Vector2(-52, -PanelTop);
        panelView.gameObject.AddComponent<RectMask2D>();
        panelView.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // 스크롤 영역 클릭용

        panelContent = NewRect("Content", panelView);
        panelContent.anchorMin = new Vector2(0, 1);
        panelContent.anchorMax = new Vector2(1, 1);
        panelContent.pivot = new Vector2(0.5f, 1);
        panelContent.sizeDelta = Vector2.zero;
        var layout = panelContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(0, 0, 2, 4);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        panelContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        panelScroll = panelView.gameObject.AddComponent<ScrollRect>();
        panelScroll.content = panelContent;
        panelScroll.viewport = panelView;
        panelScroll.horizontal = false;
        panelScroll.movementType = ScrollRect.MovementType.Clamped;
        panelScroll.scrollSensitivity = 36;

        // 오른쪽 가는 스크롤 막대 (내용이 넘칠 때만 보인다)
        panelScrollbar = MakeScrollbar(panelBox, panelScroll, new Vector2(-32, PanelBottom), new Vector2(-26, -PanelTop));

        // 창이 HUD 알림을 가리므로 같은 알림을 화면 위쪽에 보여 준다
        panelNoticeBand = MakeNoticeBand(p, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(900, 52), true);

        panel.SetActive(false);
    }

    public void OpenPanel(string title, Action<RectTransform> build, string id = null, bool closable = true, Action afterClose = null)
        => OpenPanelCore(title, build, id, closable, afterClose, true);

    // titleBar가 false면 제목 줄 없이 내용만 보여 준다 (끝 화면처럼 큰 제목을 내용 안에 직접 그릴 때)
    void OpenPanelCore(string title, Action<RectTransform> build, string id, bool closable, Action afterClose, bool titleBar)
    {
        CloseWorkbench();
        panelId = id;
        panelBuilder = build;
        afterPanelClosed = afterClose;
        panelTitle.text = title;
        panelCloseButton.SetActive(closable);
        panelTitleBar.SetActive(titleBar);
        float top = titleBar ? PanelTop : PanelHeroTop;
        panelView.offsetMax = new Vector2(-52, -top);
        panelScrollbar.offsetMax = new Vector2(-26, -top);
        bool end = id == "end";
        panelTint.color = end ? EndTint : WindowTint;
        panelTag.text = PanelTag(title, id);
        // 끝 화면: 창 판을 감추고(어두운 화면 위에 글만) 죽었으면 핏자국 층을 켠 뒤 EndFadeSeconds에 걸쳐 서서히 나타난다 (UpdatePanel). 다른 창은 바로 보인다
        SetPlateVisible(panelBox, !end);
        endLayer.gameObject.SetActive(end && endBlood);
        endFade = end ? 0f : 1f;
        panelGroup.alpha = endFade;
        panelHeight = 0;
        endCountdown = null;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        fadeOverlay.transform.SetAsLastSibling();
        RefreshGlass();
        RefreshPanel();
        blurTimer = BlurInterval; // 방금 찍었으니 다음 갱신은 조금 뒤에
        panelContent.anchoredPosition = Vector2.zero;
        panelScroll.velocity = Vector2.zero;
    }

    public void RefreshPanel()
    {
        if (!IsPanelOpen || panelBuilder == null) return;
        for (int i = panelContent.childCount - 1; i >= 0; i--)
        {
            var child = panelContent.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
        panelBuilder(panelContent);
        FitPanel();
        blurTimer = 0; // 설정을 바꿨을 수 있으니 뒤 배경을 바로 다시 찍는다
    }

    // 창 높이를 내용에 맞춘다. 같은 창을 보는 동안에는 줄이지 않는다 (누를 때마다 버튼이 움직이지 않게)
    void FitPanel()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelContent);
        float top = panelTitleBar.activeSelf ? PanelTop : PanelHeroTop;
        float need = LayoutUtility.GetPreferredHeight(panelContent) + top + PanelBottom;
        float max = Mathf.Max(PanelMinHeight, Mathf.Min(PanelMaxHeight, root.rect.height - 72));
        panelHeight = Mathf.Clamp(Mathf.Max(panelHeight, need), PanelMinHeight, max);
        panelBox.sizeDelta = new Vector2(PanelWidth, panelHeight);
    }

    // 창이 열려 있을 때 매 프레임: 알림 따라 보여 주기, 끝 화면 남은 시간
    void UpdatePanel()
    {
        SyncNoticeBand(panelNoticeBand);
        if (endFade < 1f)
        {
            endFade = Mathf.Min(1f, endFade + Time.unscaledDeltaTime / EndFadeSeconds);
            panelGroup.alpha = endFade * endFade; // 느리게 시작해 또렷해진다
            AnimateEndLayer(endFade);
        }

        if (endCountdown == null) return;
        int left = Mathf.Max(0, Mathf.CeilToInt(endDeadline - Time.unscaledTime));
        if (left == endShown) return;
        endShown = left;
        endCountdown.text = left > 0 ? $"{left}초 후 메인 메뉴로" : "메인 메뉴로 돌아가는 중...";
    }

    // 제작대 창도 함께 닫는다 (씬 이동·휴식 등에서 열린 창을 모두 닫을 때 쓴다)
    public void ClosePanel()
    {
        CloseWorkbench();
        CloseTruck();
        ClosePanelOnly();
    }

    // 일반 창(panel)만 닫는다
    void ClosePanelOnly()
    {
        bool wasOpen = IsPanelOpen;
        if (panel != null) panel.SetActive(false);
        panelBuilder = null;
        panelId = null;
        endCountdown = null;
        var after = afterPanelClosed;
        afterPanelClosed = null;
        if (wasOpen) after?.Invoke();
    }

    // 죽음·엔딩 화면. autoSeconds초 뒤 GameManager가 메인 메뉴로 보낸다 (여기서는 남은 시간만 보여 준다).
    // 창 판 없이 어두운 화면 위에 바로 글을 놓고, 죽었을 때는 뒤에 붉은 물감처럼 번진 핏자국이 깔린다. 모두 EndFadeSeconds에 걸쳐 서서히 나타난다 (2026-11-05 저녁)
    public void ShowEndScreen(string title, string body, float autoSeconds)
    {
        SetPaused(false);
        endDeadline = Time.unscaledTime + autoSeconds;
        endBlood = GameManager.Instance != null && GameManager.Instance.IsDying;
        OpenPanelCore(title, c =>
        {
            Spacer(c, 6);
            var head = NewText(c, title, 92, TextAnchor.MiddleCenter, Text, FontTitle);
            head.gameObject.AddComponent<LayoutElement>().minHeight = 116;
            var sh = head.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f);
            sh.effectDistance = new Vector2(2, -2);
            CenterBar(c, 140, 4, MenuRed);
            Spacer(c, 24);

            var b = NewText(c, body, 24, TextAnchor.MiddleCenter, Pale);
            b.lineSpacing = 1.25f;
            b.gameObject.AddComponent<LayoutElement>().minHeight = 34;
            b.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            Spacer(c, 26);

            endCountdown = NewText(c, "", 17, TextAnchor.MiddleCenter, PaleDim);
            endCountdown.gameObject.AddComponent<LayoutElement>().minHeight = 30;
            endShown = -1;
            UpdatePanel();

            var holder = NewRect("ButtonRow", c);
            var hle = holder.gameObject.AddComponent<LayoutElement>();
            hle.minHeight = hle.preferredHeight = 76;
            var menu = PlateButton(holder, "메인 메뉴로", "MAIN MENU", () => GameManager.Instance.GoToMainMenu(), 24, 28f);
            Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 60));
        }, "end", false, null, false);
    }

    // 가운데 정렬된 짧은 막대 (세로 목록 안에서)
    static void CenterBar(RectTransform parent, float width, float height, Color color)
    {
        var holder = NewRect("Bar", parent);
        var le = holder.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
        var bar = NewRect("Fill", holder);
        Place(bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
        var img = bar.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    // ---------- 강판 창 공통 (2026-11-05 저녁) ----------

    // 창 판: 어두운 강판 + 흙먼지·긁힘·가장자리 그늘 + 칠이 벗겨진 가는 테두리("Frame"). 설정·조작법·제작진·패치노트·소지품·제작대·트럭 창이 쓴다
    static RectTransform PlateWindow(Transform parent, string name)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Fill;
        img.type = Image.Type.Sliced;
        img.color = WindowFill;
        var frame = DirtyPlate(rt, 0.85f, 0.4f); // 큰 판: 무늬를 2.5배 키워 되풀이가 눈에 띄지 않게
        frame.name = "Frame";
        frame.color = new Color(1, 1, 1, 0.3f);
        return rt;
    }

    // 창 판(바탕·흙먼지·테두리)을 보이거나 감춘다 (끝 화면은 판 없이 글만 놓는다)
    static void SetPlateVisible(RectTransform box, bool on)
    {
        box.GetComponent<Image>().enabled = on;
        foreach (Transform c in box)
            if (c.name == "Dust" || c.name == "Grime" || c.name == "Shade" || c.name == "Frame") c.gameObject.SetActive(on);
    }

    // 창 제목 옆 영문 꼬리표 (메인 메뉴 버튼의 꼬리표와 같은 꼴)
    static string PanelTag(string title, string id)
    {
        switch (id)
        {
            case "settings": return "SETTINGS";
            case "controls": return "CONTROLS";
            case "credits": return "CREDITS";
            case "end": return "";
        }
        if (title.Contains("탐험 출발")) return "DEPARTURE";
        if (title.Contains("새 게임")) return "NEW GAME";
        if (title.Contains("이어하기")) return "CONTINUE";
        if (title.Contains("저장 삭제")) return "DELETE SAVE";
        if (title.Contains("메인 메뉴")) return "MAIN MENU";
        if (title.Contains("게임 종료")) return "QUIT";
        return "";
    }

    // 창 오른쪽의 가는 스크롤 막대 (내용이 넘칠 때만 보인다). 자리는 부모 오른쪽 가장자리 기준 offsetMin·offsetMax
    static RectTransform MakeScrollbar(RectTransform parent, ScrollRect scroll, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = NewRect("Scrollbar", parent);
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var track = rt.gameObject.AddComponent<Image>();
        track.sprite = UIStyle.Fill;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 4;
        track.color = new Color(1, 1, 1, 0.05f);
        var area = NewRect("Area", rt);
        Stretch(area);
        var handle = NewRect("Handle", area);
        Stretch(handle);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.sprite = UIStyle.Fill;
        handleImg.type = Image.Type.Sliced;
        handleImg.pixelsPerUnitMultiplier = 4;
        handleImg.color = Color.white;
        var sb = rt.gameObject.AddComponent<Scrollbar>();
        sb.handleRect = handle;
        sb.targetGraphic = handleImg;
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.navigation = new Navigation { mode = Navigation.Mode.None };
        var sc = sb.colors;
        sc.normalColor = new Color(1, 1, 1, 0.24f);
        sc.highlightedColor = new Color(0.9f, 0.86f, 0.76f, 0.5f);
        sc.pressedColor = new Color(0.72f, 0.1f, 0.08f, 0.9f);
        sc.selectedColor = sc.normalColor;
        sc.fadeDuration = 0.08f;
        sb.colors = sc;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return rt;
    }

    // ---------- 끝 화면의 핏자국 층 ----------

    // 붉은 물감이 번진 자국 몇 개 (큰 자국 + 진한 웅덩이 + 튄 자국 둘) 와 가장자리 그늘. 죽었을 때만 켠다 (ShowEndScreen)
    void BuildEndLayer(RectTransform parent)
    {
        endLayer = NewRect("EndLayer", parent);
        Stretch(endLayer);
        AddBlood(new Vector2(70, 60), 1500, -6f, new Color(0.42f, 0.02f, 0.015f, 0.9f), 0f);
        AddBlood(new Vector2(-60, 90), 1040, 14f, new Color(0.17f, 0.004f, 0.004f, 0.95f), 0.1f); // 가운데는 마른 피처럼 어둡게 (글이 또렷하게)
        AddBlood(new Vector2(-760, 320), 720, 38f, new Color(0.4f, 0.02f, 0.015f, 0.85f), 0.25f);
        AddBlood(new Vector2(780, -380), 560, -112f, new Color(0.34f, 0.015f, 0.01f, 0.85f), 0.4f);
        var shade = NewRect("Shade", endLayer).gameObject.AddComponent<Image>();
        Stretch(shade.rectTransform);
        shade.sprite = UIStyle.Vignette;
        shade.color = new Color(0, 0, 0, 0.55f);
        shade.raycastTarget = false;
        endLayer.gameObject.SetActive(false);
    }

    void AddBlood(Vector2 pos, float size, float angle, Color color, float delay)
    {
        var img = NewRect("Blood", endLayer).gameObject.AddComponent<Image>();
        Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        img.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        img.sprite = UIStyle.BloodWash;
        img.color = color;
        img.raycastTarget = false;
        bloodImages.Add(img);
        bloodColors.Add(color);
        bloodDelays.Add(delay);
    }

    // 자국들이 차례로 번져 나온다 (작게 시작해 커지며 진해진다)
    void AnimateEndLayer(float t)
    {
        if (!endLayer.gameObject.activeSelf) return;
        for (int i = 0; i < bloodImages.Count; i++)
        {
            float k = Mathf.Clamp01((t - bloodDelays[i]) / (1f - bloodDelays[i]));
            k = 1 - (1 - k) * (1 - k) * (1 - k);
            bloodImages[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, k);
            var c = bloodColors[i];
            bloodImages[i].color = new Color(c.r, c.g, c.b, c.a * k);
        }
    }

    // ---------- 유리 창 블러 (패치노트 · 일반 창 · 일시정지 메뉴 · 메인 메뉴 기둥) ----------
    // 블러 텍스처는 하나를 같이 쓰므로 맨 위에 보이는 창 하나만 찍고, 나머지 창도 같은 텍스처를 가리키게 한다.
    // 제작대·트럭·소지품 창은 각자 찍는다. HUD에는 블러를 쓰지 않는다

    const float BlurInterval = 0.1f, MenuBlurInterval = 0.2f;
    float blurTimer;

    // 지금 뒤 배경을 찍어야 하는 창 (없으면 null)
    RawImage GlassTarget()
    {
        if (IsPatchNotesOpen) return patchBlur; // 메인 메뉴에서만 열리고, 열 때 일반 창을 닫는다
        if (IsPanelOpen) return panelBlur;
        if (IsPaused) return pauseBlur;
        if (IsWorkbenchOpen || IsTruckOpen || IsCutscenePlaying) return null;
        return mainMenuRoot != null && mainMenuRoot.activeSelf ? menuBlur : null;
    }

    // 매 프레임: 열린 창의 뒤 배경을 낮은 빈도로 다시 찍는다.
    // 일시정지 중에는 화면이 멈춰 있으니 다시 찍지 않는다 (설정을 바꾸면 RefreshPanel이 바로 찍게 한다)
    void UpdateGlass()
    {
        var target = GlassTarget();
        if (target == null) return;
        if (!IsPaused) blurTimer -= Time.unscaledDeltaTime;
        // 다른 창이 블러 텍스처를 새로 만들었으면(화면 크기 변경) 바로 다시 찍는다
        if (target.enabled && target.texture != blurResult) blurTimer = 0;
        if (blurTimer > 0) return;
        CaptureGlass(target);
    }

    // 창을 열 때 뒤 배경을 바로 찍는다
    void RefreshGlass()
    {
        var target = GlassTarget();
        if (target != null) CaptureGlass(target);
    }

    void CaptureGlass(RawImage target)
    {
        blurTimer = target == menuBlur ? MenuBlurInterval : BlurInterval;
        bool live = Camera.main != null;
        if (live) UpdateBlur(target);
        // 카메라가 없으면 블러를 끄고 어두운 막만 보인다 (빈 RawImage는 하얗게 보이므로)
        SyncGlass(patchBlur, live);
        SyncGlass(panelBlur, live);
        SyncGlass(pauseBlur, live);
        SyncGlass(menuBlur, live);
        FitMenuGlass();
    }

    void SyncGlass(RawImage img, bool live)
    {
        if (img == null) return;
        if (img.texture != blurResult) img.texture = blurResult;
        img.enabled = live && blurResult != null;
    }

    // ---------- 버튼 색 ----------

    static readonly Color ButtonFill = new Color(0.085f, 0.085f, 0.09f, 0.94f);
    static readonly Color ButtonFrame = new Color(1, 1, 1, 0.3f);
    static readonly Color ButtonFrameHover = new Color(1, 1, 1, 0.75f);
    static readonly Color ButtonFrameOff = new Color(1, 1, 1, 0.1f);

    // 켜진 상태의 토글 버튼 (다른 클래스에서 쓴다: 상인 창의 '거래 가능만 보기')
    public static void MarkOn(Button b) => AccentButton(b);

    // 주요 동작 버튼 (고른 탭·기본 동작): '최근 저장' 판과 같은 흙색 강판 + 오른쪽 끝 붉은 막대
    static void AccentButton(Button b) =>
        TintButton(b, new Color(0.3f, 0.28f, 0.18f, 0.95f), new Color(0.4f, 0.37f, 0.25f, 1f), new Color(1, 1, 1, 0.55f));

    // 되돌릴 수 없는 동작 버튼 (어두운 핏빛 강판 + 붉은 막대)
    static void DangerButton(Button b) =>
        TintButton(b, new Color(0.34f, 0.08f, 0.06f, 0.95f), new Color(0.46f, 0.12f, 0.09f, 1f), new Color(1f, 0.6f, 0.5f, 0.45f));

    // MakeButton으로 만든 버튼의 바탕·테두리 색을 바꾸고 오른쪽 끝 붉은 막대를 켠다 (누를 수 없는 버튼은 그대로 둔다)
    static void TintButton(Button b, Color normal, Color highlight, Color frame)
    {
        if (b == null || !b.interactable) return;
        var colors = b.colors;
        colors.normalColor = colors.selectedColor = normal;
        colors.highlightedColor = highlight;
        b.colors = colors;
        var hover = b.GetComponent<UIHover>();
        if (hover != null) hover.SetColors(0, frame, Color.Lerp(frame, Color.white, 0.3f));
        var bar = b.transform.Find("RedBar");
        if (bar != null) bar.GetComponent<Image>().color = MenuRed;
        var glow = b.transform.Find("RedGlow");
        if (glow != null) glow.GetComponent<Image>().color = new Color(MenuRed.r, MenuRed.g, MenuRed.b, 0.3f);
    }

    // ---------- 창 내용 만들기 도우미 ----------

    public UnityEngine.UI.Text Line(RectTransform parent, string text, int size = 22, Color? color = null)
    {
        var t = NewText(parent, text, size, TextAnchor.MiddleLeft, color ?? Text);
        t.gameObject.AddComponent<LayoutElement>().minHeight = size + 10;
        return t;
    }

    // 한 줄. background면 유리 카드(마우스를 올리면 밝아지고 테두리가 금색으로)
    public RectTransform Row(RectTransform parent, bool background = true)
    {
        var r = NewRect("Row", parent);
        if (background)
        {
            var bg = r.gameObject.AddComponent<Image>();
            bg.sprite = UIStyle.Fill;
            bg.type = Image.Type.Sliced;
            bg.color = RowFill;
            // 흙먼지 + 칠이 벗겨진 테두리 (둘 다 줄 배치에서 빠진다)
            var dust = NewRect("Dust", r).gameObject.AddComponent<Image>();
            dust.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(dust.rectTransform);
            dust.sprite = UIStyle.Dust;
            dust.type = Image.Type.Tiled;
            dust.color = new Color(0.45f, 0.36f, 0.22f, 0.14f);
            dust.raycastTarget = false;
            var frame = NewRect("Frame", r).gameObject.AddComponent<Image>();
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(frame.rectTransform);
            frame.sprite = UIStyle.WornFrame;
            frame.type = Image.Type.Tiled;
            frame.raycastTarget = false;
            r.gameObject.AddComponent<UIHover>().Add(bg, RowFill, RowFillHover).Add(frame, RowFrame, RowFrameHover);
        }
        var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8;
        h.padding = new RectOffset(10, 6, 4, 4);
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        var le = r.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 50;
        le.preferredHeight = 50;
        return r;
    }

    public UnityEngine.UI.Text RowLabel(RectTransform row, string text, Color? color = null)
    {
        var t = NewText(row, text, 22, TextAnchor.MiddleLeft, color ?? Text);
        t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        return t;
    }

    // 줄 맨 앞의 아이템 칸 (등급 배경·테두리 포함)
    public void RowIcon(RectTransform row, string itemId)
    {
        var v = NewItemSlot(row, "Icon", 4);
        SetItemSlot(v, itemId);
        var le = v.Rect.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 42;
    }

    // 줄 오른쪽의 무게 표시 (예: 1.5 kg)
    public UnityEngine.UI.Text RowWeight(RectTransform row, float kg)
    {
        var t = NewText(row, $"{kg:0.0} kg", 18, TextAnchor.MiddleRight, Dim);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 70;
        return t;
    }

    // ---------- 아이템 칸 (등급 배경 + 테두리 + 아이콘) ----------
    // 핫바·가방·창고·거래·제작대가 함께 쓴다. 등급 색과 배경 그림은 ItemRarityInfo에서 가져오므로
    // 등급 디자인을 바꿀 때는 ItemRarityInfo(색)와 Resources/SurvivalInventory/Rarity(그림)만 바꾸면 된다

    // 칸은 비치지 않는 어두운 주머니 (창 안). 핫바(흙색 강판 위)의 칸은 판보다 어두운 홈 (glass)
    static readonly Color SlotEmptyFill = new Color(0.05f, 0.048f, 0.045f, 1f);
    static readonly Color SlotItemFill = new Color(0.07f, 0.065f, 0.06f, 1f);
    static readonly Color SlotEmptyGlass = new Color(0, 0, 0, 0.3f);
    static readonly Color SlotItemGlass = new Color(0, 0, 0, 0.42f);
    static readonly Color SlotFrameGlass = new Color(0.9f, 0.85f, 0.72f, 0.2f);

    class ItemSlotView
    {
        public RectTransform Rect;
        public Image Fill, Strip, Frame, Icon;
        public RawImage Background;
        public bool Glass; // 비치는 유리 칸 (핫바)
    }

    // 그리는 순서: 바탕 → 등급 배경 그림 → 아래 띠 → 테두리 → 아이콘
    static ItemSlotView NewItemSlot(Transform parent, string name, float iconPad, bool glass = false)
    {
        var v = new ItemSlotView { Rect = NewRect(name, parent), Glass = glass };
        v.Fill = v.Rect.gameObject.AddComponent<Image>();
        v.Fill.sprite = UIStyle.Fill;
        v.Fill.type = Image.Type.Sliced;
        v.Fill.color = SlotEmptyFill;
        v.Fill.raycastTarget = false;
        if (glass) v.Fill.pixelsPerUnitMultiplier = 0.7f; // 모서리를 조금 더 둥글게

        v.Background = NewRect("Rarity", v.Rect).gameObject.AddComponent<RawImage>();
        Stretch(v.Background.rectTransform);
        v.Background.raycastTarget = false;

        v.Strip = NewRect("Strip", v.Rect).gameObject.AddComponent<Image>();
        var srt = v.Strip.rectTransform;
        srt.anchorMin = new Vector2(0, 0);
        srt.anchorMax = new Vector2(1, 0);
        srt.pivot = new Vector2(0.5f, 0);
        srt.anchoredPosition = new Vector2(0, 3);
        srt.sizeDelta = new Vector2(-14, 3);
        v.Strip.raycastTarget = false;

        v.Frame = NewRect("Frame", v.Rect).gameObject.AddComponent<Image>();
        Stretch(v.Frame.rectTransform);
        v.Frame.sprite = UIStyle.Frame;
        v.Frame.type = Image.Type.Sliced;
        v.Frame.raycastTarget = false;
        if (glass) v.Frame.pixelsPerUnitMultiplier = 0.7f;

        v.Icon = NewRect("Icon", v.Rect).gameObject.AddComponent<Image>();
        Stretch(v.Icon.rectTransform, iconPad, iconPad);
        v.Icon.preserveAspect = true;
        v.Icon.raycastTarget = false;

        SetItemSlot(v, null);
        return v;
    }

    // 칸에 아이템을 넣는다 (null이면 빈 칸). 등급 배경 그림이 있으면 깔고, 없으면 어두운 바탕 + 등급 색 테두리·아래 띠
    static void SetItemSlot(ItemSlotView v, string itemId)
    {
        bool has = !string.IsNullOrEmpty(itemId);
        var rarity = has ? ItemDatabase.Get(itemId).Rarity : ItemRarity.Common;
        var c = ItemRarityInfo.Color(rarity);
        var bg = has ? ItemRarityInfo.Background(rarity) : null;
        bool hasBg = bg != null;

        v.Fill.color = v.Glass ? (has ? SlotItemGlass : SlotEmptyGlass) : (has ? SlotItemFill : SlotEmptyFill);
        v.Background.texture = bg;
        v.Background.enabled = hasBg;
        v.Background.color = new Color(1, 1, 1, v.Glass ? 0.85f : 1f);
        v.Strip.enabled = has && !hasBg;
        v.Strip.color = c;
        v.Frame.color = has ? new Color(c.r, c.g, c.b, hasBg ? 0.5f : 0.8f) : v.Glass ? SlotFrameGlass : FrameColor;
        v.Icon.enabled = has;
        v.Icon.sprite = has ? ItemIcons.Get(itemId) : null;
    }

    // 등급 색으로 칠한 아이템 이름 (리치 텍스트)
    public static string ColoredName(string itemId)
    {
        var def = ItemDatabase.Get(itemId);
        return $"<color={ItemRarityInfo.Hex(def.Rarity)}>{def.Name}</color>";
    }

    // 등급 이름 (예: <color=#4A8FE6>희귀</color>)
    public static string RarityTag(ItemRarity r) => $"<color={ItemRarityInfo.Hex(r)}>{ItemRarityInfo.Name(r)}</color>";

    public static string CategoryName(ItemCategory c)
    {
        switch (c)
        {
            case ItemCategory.Health: return "의료";
            case ItemCategory.Food: return "식량";
            case ItemCategory.Equipment: return "장비";
            default: return "재료";
        }
    }

    public Button RowButton(RectTransform row, string label, Action onClick, bool interactable = true, float width = 110)
    {
        var b = MakeButton(row, label, onClick, interactable);
        var le = b.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        return b;
    }

    // 강판 버튼 (2026-11-05 저녁, 메인 메뉴와 같은 '더러운 강판'): 어두운 강판 + 흙먼지·긁힘 + 칠이 벗겨진 테두리("Frame"), 오른쪽 끝에 평소엔 감춘 붉은 막대
    // (AccentButton·DangerButton이 켠다). 마우스를 올리면 판이 밝아지고 테두리가 또렷해진다. 자식 "Frame"·"Edge"는 다른 창이 찾아 쓰므로 이름을 바꾸지 않는다
    Button MakeButton(Transform parent, string label, Action onClick, bool interactable, int fontSize = 20)
    {
        var rt = NewRect("Button", parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Fill;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        var frame = DirtyPlate(rt, 0.7f);
        frame.name = "Frame";
        frame.color = interactable ? ButtonFrame : ButtonFrameOff;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.normalColor = ButtonFill;
        colors.highlightedColor = PlateHover;
        colors.pressedColor = new Color(0.24f, 0.23f, 0.22f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.06f, 0.06f, 0.062f, 0.7f);
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.interactable = interactable;
        b.onClick.AddListener(() => onClick());
        UIPress.Attach(b);
        GlassEdge(rt, interactable ? 0.05f : 0.02f);
        RedBar(rt, 9, new Vector2(3, 18), 32, 0f);
        rt.Find("RedBar").GetComponent<Image>().color = Color.clear; // 평소엔 감춘다 (TintButton이 켠다)
        if (interactable)
        {
            var hover = rt.gameObject.AddComponent<UIHover>();
            hover.Gate = b;
            hover.Add(frame, ButtonFrame, ButtonFrameHover);
        }
        var t = NewText(rt, label, fontSize, TextAnchor.MiddleCenter, interactable ? Text : new Color(0.45f, 0.44f, 0.4f), FontTitle);
        Stretch(t.rectTransform);
        t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
        return b;
    }

    // ---------- 기본 도우미 ----------

    // 반투명 채우기 + 테두리가 있는 상자
    static RectTransform Box(Transform parent, string name, Color fill, Color frame)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Fill;
        img.type = Image.Type.Sliced;
        img.color = fill;
        var f = NewRect("Frame", rt);
        Stretch(f);
        var fi = f.gameObject.AddComponent<Image>();
        fi.sprite = UIStyle.Frame;
        fi.type = Image.Type.Sliced;
        fi.color = frame;
        fi.raycastTarget = false;
        return rt;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static UnityEngine.UI.Text NewText(Transform parent, string text, int size, TextAnchor anchor, Color color, Font font = null)
    {
        var rt = NewRect("Text", parent);
        var t = rt.gameObject.AddComponent<UnityEngine.UI.Text>();
        t.font = font != null ? font : Font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float padX = 0, float padY = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    Image FullScreenImage(string name, Color color)
    {
        var rt = NewRect(name, root);
        Stretch(rt);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
