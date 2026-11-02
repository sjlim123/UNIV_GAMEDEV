using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// HUD(상태·핫바·안내)와 각종 창을 코드로 만든다. 메뉴 화면은 UIManager.Menus.cs
// 디자인: 모든 창은 뒤 화면을 흐리게 깐 반투명 유리판(패치노트·제작대와 같은 모양). HUD는 블러 없이 반투명 유리판만 쓴다
public partial class UIManager : MonoBehaviour
{
    public static readonly Color Text = new Color(0.92f, 0.9f, 0.84f);
    public static readonly Color Dim = new Color(0.66f, 0.64f, 0.58f);
    public static readonly Color Accent = new Color(0.98f, 0.76f, 0.32f);
    public static readonly Color Danger = new Color(0.96f, 0.36f, 0.28f);
    public static readonly Color PanelFill = new Color(0.05f, 0.055f, 0.05f, 0.72f);
    public static readonly Color FrameColor = new Color(0.9f, 0.8f, 0.55f, 0.32f);

    // HUD 유리판 (블러 없음: 조금 더 진하게 해서 밝은 배경에서도 읽히게)
    static readonly Color HudFill = new Color(0.06f, 0.065f, 0.062f, 0.6f);
    static readonly Color HudFrame = new Color(1, 1, 1, 0.13f);
    // 창 유리판의 은은한 금색 테두리와 뒤 화면을 누르는 어두운 막
    static readonly Color GoldFrame = new Color(0.9f, 0.8f, 0.55f, 0.3f);
    static readonly Color WindowTint = new Color(0.02f, 0.025f, 0.03f, 0.34f);
    static readonly Color EndTint = new Color(0.03f, 0.02f, 0.02f, 0.55f);
    // 창 안의 줄(Row) 카드: 평소 / 마우스를 올렸을 때
    static readonly Color RowFill = new Color(1, 1, 1, 0.045f);
    static readonly Color RowFillHover = new Color(1, 1, 1, 0.09f);
    static readonly Color RowFrame = new Color(1, 1, 1, 0.07f);
    static readonly Color RowFrameHover = new Color(0.98f, 0.76f, 0.32f, 0.45f);

    // Pretendard (SIL OFL 1.1) — Assets/Resources/Fonts. 본문 Regular, 버튼·제목 SemiBold, 큰 제목 Bold
    public static Font Font { get; private set; }
    public static Font FontSemiBold { get; private set; }
    public static Font FontBold { get; private set; }

    RectTransform root;
    GameObject hud;
    UnityEngine.UI.Text promptText, toastText, flashlightText, itemNameText, fpsText;
    RectTransform promptBox;
    CanvasGroup toastGroup;
    GameObject flashBox;
    Image damageOverlay, fadeOverlay, redNightOverlay;
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
    UnityEngine.UI.Text panelTitle;
    GameObject panelCloseButton, panelTitleBar;
    RawImage panelBlur;
    Image panelTint;
    ScrollRect panelScroll;
    UnityEngine.UI.Text panelNotice;
    CanvasGroup panelNoticeGroup;
    float panelHeight;
    Action<RectTransform> panelBuilder;
    string panelId;
    Action afterPanelClosed;

    // 끝 화면의 남은 시간 표시
    UnityEngine.UI.Text endCountdown;
    float endDeadline;
    int endShown;

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
        fadeOverlay = FullScreenImage("Fade", new Color(0, 0, 0, 0));
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
        else HideMainMenu();
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

        // 오른쪽 아래: 손전등
        var flash = HudGlass(h, "Flashlight", HudFrame);
        Place(flash, new Vector2(1, 0), new Vector2(-24, 40), new Vector2(270, 42));
        flashBox = flash.gameObject;
        flashlightText = NewText(flash, "", 19, TextAnchor.MiddleCenter, Text);
        Stretch(flashlightText.rectTransform, 10, 0);

        // 왼쪽 아래: 조작 안내

        // 가운데 아래: 핫바 (유리 받침 위에 칸 8개)
        const float slot = 68, gap = 8;
        float barWidth = SlotCount * slot + (SlotCount - 1) * gap;
        var tray = HudGlass(h, "HotbarTray", HudFrame);
        Place(tray, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(barWidth + 20, slot + 20));
        hotbarTray = tray;
        // 핫바는 살짝 비쳐 보이게 (받침은 더 투명하게)
        tray.gameObject.AddComponent<CanvasGroup>().alpha = 0.6f;
        var bar = NewRect("Hotbar", h);
        Place(bar, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(barWidth, slot));
        hotbarBar = bar;
        bar.gameObject.AddComponent<CanvasGroup>().alpha = 0.82f;
        for (int i = 0; i < SlotCount; i++)
        {
            var v = NewItemSlot(bar, "Slot" + i, 7);
            var s = v.Rect;
            s.anchorMin = s.anchorMax = new Vector2(0, 0.5f);
            s.pivot = new Vector2(0.5f, 0.5f); // 고른 칸이 가운데를 기준으로 커지게
            s.anchoredPosition = new Vector2(i * (slot + gap) + slot / 2, 0);
            s.sizeDelta = new Vector2(slot, slot);
            hotbarSlots.Add(v);

            // 고른 칸: 바깥 금색 테두리 + 은은한 빛 (등급 테두리와 겹치지 않게 칸 밖에 그린다)
            var glow = NewRect("Glow", s).gameObject.AddComponent<Image>();
            glow.transform.SetAsFirstSibling();
            Stretch(glow.rectTransform, -5, -5);
            glow.sprite = UIStyle.Fill;
            glow.type = Image.Type.Sliced;
            glow.color = new Color(Accent.r, Accent.g, Accent.b, 0.22f);
            glow.raycastTarget = false;
            slotGlows.Add(glow);
            var sel = NewRect("Select", s).gameObject.AddComponent<Image>();
            Stretch(sel.rectTransform, -3, -3);
            sel.sprite = UIStyle.Frame;
            sel.type = Image.Type.Sliced;
            sel.pixelsPerUnitMultiplier = 0.7f; // 테두리를 조금 굵게
            sel.color = Accent;
            sel.raycastTarget = false;
            slotSelects.Add(sel);

            var key = NewText(s, (i + 1).ToString(), 13, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.45f));
            Stretch(key.rectTransform, 6, 3);
            key.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            slotKeys.Add(key);
            var count = NewText(s, "", 17, TextAnchor.LowerRight, Text, FontSemiBold);
            Stretch(count.rectTransform, 6, 3);
            count.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            slotCounts.Add(count);
        }
        itemNameText = NewText(h, "", 20, TextAnchor.MiddleCenter, Text);
        Place(itemNameText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 112), new Vector2(900, 30));
        itemNameText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);

        // 가운데: 조준점, 상호작용 안내, 알림
        BuildCrosshair(h);

        promptBox = HudGlass(h, "Prompt", new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        Place(promptBox, new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(300, 46));
        promptText = NewText(promptBox, "", 23, TextAnchor.MiddleCenter, Text);
        Stretch(promptText.rectTransform);
        promptBox.gameObject.SetActive(false);

        var toast = HudGlass(h, "Toast", HudFrame);
        Place(toast, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(980, 60));
        toastRect = toast;
        AccentBar(toast);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0;
        toastGroup.blocksRaycasts = false;
        toastText = NewText(toast, "", 23, TextAnchor.MiddleCenter, Text);
        Stretch(toastText.rectTransform, 24, 4);
    }

    // HUD용 반투명 유리판 (블러 없음): 어두운 채우기 + 은은한 테두리 + 윗면의 얇은 반사광
    static RectTransform HudGlass(Transform parent, string name, Color frame)
    {
        var rt = Box(parent, name, HudFill, frame);
        rt.GetComponent<Image>().raycastTarget = false;
        GlassEdge(rt);
        return rt;
    }

    // 유리판 윗면의 얇은 반사광 (창·HUD 공통)
    static void GlassEdge(RectTransform box, float alpha = 0.16f)
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

        hud.SetActive(PlayerController.Instance != null);
        if (hud.activeSelf)
        {
            UpdateHud(gm, s);
            UpdateCrosshair();
            AnimateHud();
        }

        if (toastTimer > 0)
        {
            toastTimer -= Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Clamp01(toastTimer * 2);
        }

        // 피격 효과 + 체력이 낮을 때 붉은 맥박. 일시정지·메뉴·컷씬처럼 게임을 하고 있지 않을 때는 보이지 않는다
        damageAlpha = Mathf.MoveTowards(damageAlpha, 0, Time.unscaledDeltaTime * 1.2f);
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

        bool hasLight = s.Carried.Count("flashlight") > 0;
        flashBox.SetActive(hasLight);
        if (hasLight)
        {
            int key = Mathf.RoundToInt(s.FlashlightCharge) * 10000 + s.Carried.Count("battery");
            if (key != flashKey)
            {
                flashKey = key;
                flashlightText.text = $"손전등  <color={(s.FlashlightCharge < 20 ? "#f05a48" : "#f9c252")}>{s.FlashlightCharge:0}%</color>   배터리 {s.Carried.Count("battery")}";
            }
        }

        UpdateHotbar(s);
    }

    readonly List<KeyValuePair<string, int>> hotbarItems = new List<KeyValuePair<string, int>>(SlotCount);

    void UpdateHotbar(GameState s)
    {
        var hotbar = PlayerHotbar.Instance;
        hotbarItems.Clear();
        int offBar = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            string id = s.Hotbar[i];
            hotbarItems.Add(new KeyValuePair<string, int>(id, id != null ? s.Carried.Count(id) : 0));
        }
        foreach (var kv in s.Carried.Sorted())
            if (s.HotbarIndex(kv.Key) < 0) offBar++;
        int selected = hotbar != null ? hotbar.SelectedIndex : -1;
        // 고른 칸이 바뀌면 부드럽게 커지고 작아진다 (매 프레임)
        for (int i = 0; i < SlotCount; i++)
        {
            float target = i == selected ? 1.1f : 1f;
            var rt = hotbarSlots[i].Rect;
            float sc = Mathf.Lerp(rt.localScale.x, target, Time.unscaledDeltaTime * 14f);
            rt.localScale = Vector3.one * sc;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, Mathf.Lerp(rt.anchoredPosition.y, i == selected ? 6f : 0f, Time.unscaledDeltaTime * 14f));
        }
        // 새총을 들고 있으면 남은 발 수가 바뀔 때도 다시 쓴다
        if (selected == hotbarSelected && s.SlingShots == hotbarSlingShots && SameHotbarItems(hotbarItems)) return;
        // 칸에 아이템이 새로 들어오거나 개수가 늘면 그 칸이 톡 튄다
        for (int i = 0; i < SlotCount && hotbarShown.Count == SlotCount; i++)
            if (hotbarItems[i].Key != null && (hotbarItems[i].Key != hotbarShown[i].Key || hotbarItems[i].Value > hotbarShown[i].Value))
                hotbarSlots[i].Rect.localScale = Vector3.one * 1.28f;
        hotbarSelected = selected;
        hotbarSlingShots = s.SlingShots;
        hotbarShown.Clear();
        hotbarShown.AddRange(hotbarItems);

        for (int i = 0; i < SlotCount; i++)
        {
            bool has = hotbarItems[i].Key != null;
            bool isSel = i == selected;
            SetItemSlot(hotbarSlots[i], hotbarItems[i].Key);
            slotCounts[i].text = has && hotbarItems[i].Value > 1 ? hotbarItems[i].Value.ToString() : "";
            slotGlows[i].enabled = slotSelects[i].enabled = isSel;
            slotKeys[i].color = isSel ? Accent : new Color(1, 1, 1, 0.45f);
        }

        if (selected >= 0 && hotbarItems[selected].Key != null)
        {
            var def = ItemDatabase.Get(hotbarItems[selected].Key);
            string action = PlayerHotbar.ActionHint(def.Id);
            itemNameText.text = $"{ColoredName(def.Id)} x{hotbarItems[selected].Value}   <size=17>{RarityTag(def.Rarity)}   <color=#a8a294>{action}</color></size>";
        }
        else itemNameText.text = offBar > 0 ? $"<size=17><color=#a8a294>핫바에 없는 아이템 {offBar}종 · {KeyBindings.Label(GameAction.Inventory)}에서 끌어다 놓기</color></size>" : "";
        itemNameFade = 0;
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

        // 탐험 일지: Esc·스페이스·엔터로 (다 적혔으면) 덮는다
        if (IsJournalOpen)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) JournalClick();
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
    }

    // 붉은 밤 화면 효과 (0 = 없음, 1 = 가득)
    public void SetRedNight(float amount) => redNight = Mathf.Clamp01(amount);

    public void FlashDamage(float strength)
    {
        damageAlpha = Mathf.Max(damageAlpha, 0.25f + 0.35f * strength);
        statusShake = Mathf.Max(statusShake, 0.5f + strength);
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

        panelBox = Box(p, "Box", GlassFill, GoldFrame);
        Place(panelBox, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelMinHeight));
        GlassEdge(panelBox, 0.14f);
        panelBox.gameObject.AddComponent<UIPopIn>();

        // 제목 줄: 제목 + 얇은 선(왼쪽은 금색) + 닫기 버튼
        var bar = NewRect("TitleBar", panelBox);
        Stretch(bar);
        panelTitleBar = bar.gameObject;
        panelTitle = NewText(bar, "", 36, TextAnchor.MiddleLeft, Text, FontBold);
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
        var accent = NewRect("TitleAccent", bar);
        Place(accent, new Vector2(0, 1), new Vector2(36, -99), new Vector2(96, 4));
        var accentImg = accent.gameObject.AddComponent<Image>();
        accentImg.color = new Color(Accent.r, Accent.g, Accent.b, 0.9f);
        accentImg.raycastTarget = false;

        var close = MakeButton(bar, "닫기 (Esc)", ClosePanel, true);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-36, -28), new Vector2(170, 46));
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
        panelScrollbar = NewRect("Scrollbar", panelBox);
        panelScrollbar.anchorMin = new Vector2(1, 0);
        panelScrollbar.anchorMax = new Vector2(1, 1);
        panelScrollbar.pivot = new Vector2(1, 0.5f);
        panelScrollbar.offsetMin = new Vector2(-32, PanelBottom);
        panelScrollbar.offsetMax = new Vector2(-26, -PanelTop);
        var track = panelScrollbar.gameObject.AddComponent<Image>();
        track.sprite = UIStyle.Fill;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 4;
        track.color = new Color(1, 1, 1, 0.05f);
        var area = NewRect("Area", panelScrollbar);
        Stretch(area);
        var handle = NewRect("Handle", area);
        Stretch(handle);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.sprite = UIStyle.Fill;
        handleImg.type = Image.Type.Sliced;
        handleImg.pixelsPerUnitMultiplier = 4;
        handleImg.color = Color.white;
        var sb = panelScrollbar.gameObject.AddComponent<Scrollbar>();
        sb.handleRect = handle;
        sb.targetGraphic = handleImg;
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.navigation = new Navigation { mode = Navigation.Mode.None };
        var sc = sb.colors;
        sc.normalColor = new Color(1, 1, 1, 0.24f);
        sc.highlightedColor = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
        sc.pressedColor = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
        sc.selectedColor = sc.normalColor;
        sc.fadeDuration = 0.08f;
        sb.colors = sc;
        panelScroll.verticalScrollbar = sb;
        panelScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // 창이 HUD 알림을 가리므로 같은 알림을 화면 위쪽에 보여 준다
        var notice = Box(p, "Notice", new Color(0.1f, 0.09f, 0.07f, 0.94f), new Color(Accent.r, Accent.g, Accent.b, 0.55f));
        Place(notice, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(900, 52));
        AccentBar(notice);
        panelNoticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        panelNoticeGroup.alpha = 0;
        panelNoticeGroup.blocksRaycasts = false;
        panelNotice = NewText(notice, "", 21, TextAnchor.MiddleCenter, Text);
        Stretch(panelNotice.rectTransform, 24, 2);

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
        panelTint.color = id == "end" ? EndTint : WindowTint;
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
        panelNotice.text = toastText.text;
        panelNoticeGroup.alpha = toastGroup.alpha;

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

    // 죽음·엔딩 화면. autoSeconds초 뒤 GameManager가 메인 메뉴로 보낸다 (여기서는 남은 시간만 보여 준다)
    public void ShowEndScreen(string title, string body, float autoSeconds)
    {
        SetPaused(false);
        endDeadline = Time.unscaledTime + autoSeconds;
        OpenPanelCore(title, c =>
        {
            Spacer(c, 6);
            var head = NewText(c, title, 76, TextAnchor.MiddleCenter, Text, FontBold);
            head.gameObject.AddComponent<LayoutElement>().minHeight = 100;
            var sh = head.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(2, -2);
            CenterBar(c, 120, 3, Accent);
            Spacer(c, 22);

            var b = NewText(c, body, 24, TextAnchor.MiddleCenter, Text);
            b.lineSpacing = 1.25f;
            b.gameObject.AddComponent<LayoutElement>().minHeight = 34;
            Spacer(c, 26);

            endCountdown = NewText(c, "", 19, TextAnchor.MiddleCenter, Dim);
            endCountdown.gameObject.AddComponent<LayoutElement>().minHeight = 30;
            endShown = -1;
            UpdatePanel();

            var row = Row(c, false);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.padding = new RectOffset(0, 0, 4, 4);
            var rle = row.GetComponent<LayoutElement>();
            rle.minHeight = rle.preferredHeight = 64;
            AccentButton(RowButton(row, "메인 메뉴로", () => GameManager.Instance.GoToMainMenu(), true, 300));
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

    static readonly Color ButtonFill = new Color(0.16f, 0.15f, 0.125f, 0.86f);
    static readonly Color ButtonFrame = new Color(0.9f, 0.8f, 0.55f, 0.4f);
    static readonly Color ButtonFrameHover = new Color(0.98f, 0.8f, 0.42f, 0.85f);
    static readonly Color ButtonFrameOff = new Color(1, 1, 1, 0.08f);

    // 주요 동작 버튼 (금색)
    static void AccentButton(Button b) =>
        TintButton(b, new Color(0.5f, 0.37f, 0.14f, 0.95f), new Color(0.64f, 0.48f, 0.2f, 1f), new Color(Accent.r, Accent.g, Accent.b, 0.7f));

    // 되돌릴 수 없는 동작 버튼 (붉은색)
    static void DangerButton(Button b) =>
        TintButton(b, new Color(0.4f, 0.12f, 0.09f, 0.92f), new Color(0.58f, 0.18f, 0.13f, 1f), new Color(Danger.r, Danger.g, Danger.b, 0.65f));

    // MakeButton으로 만든 버튼의 바탕·테두리 색을 바꾼다 (누를 수 없는 버튼은 그대로 둔다)
    static void TintButton(Button b, Color normal, Color highlight, Color frame)
    {
        if (b == null || !b.interactable) return;
        var colors = b.colors;
        colors.normalColor = colors.selectedColor = normal;
        colors.highlightedColor = highlight;
        b.colors = colors;
        var hover = b.GetComponent<UIHover>();
        if (hover != null) hover.SetColors(0, frame, Color.Lerp(frame, Color.white, 0.3f));
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
            var frame = NewRect("Frame", r).gameObject.AddComponent<Image>();
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(frame.rectTransform);
            frame.sprite = UIStyle.Frame;
            frame.type = Image.Type.Sliced;
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

    static readonly Color SlotEmptyFill = new Color(0.03f, 0.03f, 0.03f, 0.6f);
    static readonly Color SlotItemFill = new Color(0.04f, 0.04f, 0.04f, 0.88f);

    class ItemSlotView
    {
        public RectTransform Rect;
        public Image Fill, Strip, Frame, Icon;
        public RawImage Background;
    }

    // 그리는 순서: 바탕 → 등급 배경 그림 → 아래 띠 → 테두리 → 아이콘
    static ItemSlotView NewItemSlot(Transform parent, string name, float iconPad)
    {
        var v = new ItemSlotView { Rect = NewRect(name, parent) };
        v.Fill = v.Rect.gameObject.AddComponent<Image>();
        v.Fill.sprite = UIStyle.Fill;
        v.Fill.type = Image.Type.Sliced;
        v.Fill.color = SlotEmptyFill;
        v.Fill.raycastTarget = false;

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

        v.Fill.color = has ? SlotItemFill : SlotEmptyFill;
        v.Background.texture = bg;
        v.Background.enabled = hasBg;
        v.Strip.enabled = has && !hasBg;
        v.Strip.color = c;
        v.Frame.color = has ? new Color(c.r, c.g, c.b, hasBg ? 0.5f : 0.8f) : FrameColor;
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

    // 유리 버튼: 반투명 바탕 + 은은한 금색 테두리 + 윗면 반사광. 마우스를 올리면 바탕이 금빛으로, 테두리가 진해진다
    // 자식 구조(Frame·Edge·Text)는 다른 창이 찾아 쓰므로 바꾸지 않는다
    Button MakeButton(Transform parent, string label, Action onClick, bool interactable, int fontSize = 20)
    {
        var rt = Box(parent, "Button", Color.white, interactable ? ButtonFrame : ButtonFrameOff);
        var img = rt.GetComponent<Image>();
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.normalColor = ButtonFill;
        colors.highlightedColor = new Color(0.42f, 0.33f, 0.16f, 0.95f);
        colors.pressedColor = new Color(0.58f, 0.43f, 0.18f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.08f, 0.08f, 0.07f, 0.62f);
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.interactable = interactable;
        b.onClick.AddListener(() => onClick());
        UIPress.Attach(b);
        GlassEdge(rt, interactable ? 0.1f : 0.04f);
        if (interactable)
        {
            var hover = rt.gameObject.AddComponent<UIHover>();
            hover.Gate = b;
            hover.Add(rt.Find("Frame").GetComponent<Image>(), ButtonFrame, ButtonFrameHover);
        }
        var t = NewText(rt, label, fontSize, TextAnchor.MiddleCenter, interactable ? Text : new Color(0.45f, 0.44f, 0.4f), FontSemiBold);
        Stretch(t.rectTransform);
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
