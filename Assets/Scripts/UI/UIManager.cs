using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// HUD(상태·핫바·안내)와 각종 창을 코드로 만든다. 메뉴 화면은 UIManager.Menus.cs
public partial class UIManager : MonoBehaviour
{
    public static readonly Color Text = new Color(0.92f, 0.9f, 0.84f);
    public static readonly Color Dim = new Color(0.66f, 0.64f, 0.58f);
    public static readonly Color Accent = new Color(0.98f, 0.76f, 0.32f);
    public static readonly Color Danger = new Color(0.96f, 0.36f, 0.28f);
    public static readonly Color PanelFill = new Color(0.05f, 0.055f, 0.05f, 0.72f);
    public static readonly Color FrameColor = new Color(0.9f, 0.8f, 0.55f, 0.32f);

    // Pretendard (SIL OFL 1.1) — Assets/Resources/Fonts. 본문 Regular, 버튼·제목 SemiBold, 큰 제목 Bold
    public static Font Font { get; private set; }
    public static Font FontSemiBold { get; private set; }
    public static Font FontBold { get; private set; }

    RectTransform root;
    GameObject hud;
    RectTransform healthFill, hungerFill, thirstFill;
    UnityEngine.UI.Text healthValue, hungerValue, thirstValue;
    UnityEngine.UI.Text infoText, promptText, toastText, flashlightText, itemNameText, fpsText;
    RectTransform promptBox;
    CanvasGroup toastGroup;
    GameObject flashBox;
    Image damageOverlay, fadeOverlay;

    const int SlotCount = 8;
    readonly List<Image> slotFills = new List<Image>(), slotFrames = new List<Image>(), slotIcons = new List<Image>();
    readonly List<UnityEngine.UI.Text> slotCounts = new List<UnityEngine.UI.Text>();
    string hotbarSignature;

    GameObject panel;
    RectTransform panelContent;
    UnityEngine.UI.Text panelTitle;
    GameObject panelCloseButton;
    Action<RectTransform> panelBuilder;
    string panelId;
    Action afterPanelClosed;

    float toastTimer, damageAlpha, fpsTimer;
    int fpsFrames;

    public bool IsPanelOpen => panel != null && panel.activeSelf;
    // 창이나 일시정지 메뉴가 열려 있으면 플레이어 조작을 막는다
    public bool IsBlocking => IsPanelOpen || IsPaused || IsPatchNotesOpen;

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

        damageOverlay = FullScreenImage("Damage", new Color(0.5f, 0, 0, 0));
        fadeOverlay = FullScreenImage("Fade", new Color(0, 0, 0, 0));
    }

    // 빌드된 게임에서 창을 내리거나 다른 프로그램으로 가면 자동으로 일시정지한다
    void OnApplicationFocus(bool focus)
    {
        if (!focus && !Application.isEditor) SetPaused(true);
    }

    public void OnSceneLoaded(string sceneName)
    {
        SetPaused(false);
        ClosePatchNotes();
        if (sceneName == GameManager.MenuScene) ShowMainMenu();
        else HideMainMenu();
        hotbarSignature = null;
    }

    // ---------- HUD ----------

    void BuildHud()
    {
        hud = NewRect("HUD", root).gameObject;
        Stretch((RectTransform)hud.transform);
        var h = hud.transform;

        // 왼쪽 위: 상태
        var status = Box(h, "Status", PanelFill, FrameColor);
        Place(status, new Vector2(0, 1), new Vector2(24, -24), new Vector2(340, 118));
        healthFill = MakeBar(status, "체력", 0, new Color(0.78f, 0.2f, 0.16f), out healthValue);
        hungerFill = MakeBar(status, "배고픔", 1, new Color(0.85f, 0.58f, 0.2f), out hungerValue);
        thirstFill = MakeBar(status, "갈증", 2, new Color(0.28f, 0.55f, 0.85f), out thirstValue);

        // 오른쪽 위: 턴·시간·전력
        var info = Box(h, "Info", PanelFill, FrameColor);
        Place(info, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(270, 90));
        infoText = NewText(info, "", 28, TextAnchor.MiddleRight, Text, FontSemiBold);
        Stretch(infoText.rectTransform, 18, 8);

        // 오른쪽 아래: 손전등
        var flash = Box(h, "Flashlight", PanelFill, FrameColor);
        Place(flash, new Vector2(1, 0), new Vector2(-24, 40), new Vector2(270, 40));
        flashBox = flash.gameObject;
        flashlightText = NewText(flash, "", 19, TextAnchor.MiddleCenter, Text);
        Stretch(flashlightText.rectTransform, 10, 0);

        // 왼쪽 아래: 조작 안내
        var hints = NewText(h, "WASD 이동   Shift 달리기   C 앉기   F 상호작용   Q 손전등\n1~8·휠 선택   우클릭 사용   G 버리기   좌클릭 공격   Tab 가방   Esc 메뉴", 16, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.5f));
        Place(hints.rectTransform, new Vector2(0, 0), new Vector2(24, 20), new Vector2(620, 50));
        hints.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);

        // 가운데 아래: 핫바
        var bar = NewRect("Hotbar", h);
        const float slot = 68, gap = 8;
        Place(bar, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(SlotCount * slot + (SlotCount - 1) * gap, slot));
        for (int i = 0; i < SlotCount; i++)
        {
            var s = Box(bar, "Slot" + i, new Color(0.03f, 0.03f, 0.03f, 0.6f), FrameColor);
            s.anchorMin = s.anchorMax = s.pivot = new Vector2(0, 0.5f);
            s.anchoredPosition = new Vector2(i * (slot + gap), 0);
            s.sizeDelta = new Vector2(slot, slot);
            slotFills.Add(s.GetComponent<Image>());
            slotFrames.Add(s.GetChild(0).GetComponent<Image>());
            var icon = NewRect("Icon", s).gameObject.AddComponent<Image>();
            Stretch(icon.rectTransform, 7, 7);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            slotIcons.Add(icon);
            var key = NewText(s, (i + 1).ToString(), 13, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.45f));
            Stretch(key.rectTransform, 6, 3);
            var count = NewText(s, "", 17, TextAnchor.LowerRight, Text, FontSemiBold);
            Stretch(count.rectTransform, 6, 3);
            count.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            slotCounts.Add(count);
        }
        itemNameText = NewText(h, "", 20, TextAnchor.MiddleCenter, Text);
        Place(itemNameText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 104), new Vector2(900, 30));
        itemNameText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);

        // 가운데: 조준점, 상호작용 안내, 알림
        var cross = NewRect("Crosshair", h);
        Place(cross, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5));
        var crossImg = cross.gameObject.AddComponent<Image>();
        crossImg.sprite = UIStyle.Fill;
        crossImg.color = new Color(1, 1, 1, 0.7f);
        cross.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);

        promptBox = Box(h, "Prompt", new Color(0.03f, 0.03f, 0.03f, 0.7f), new Color(0.98f, 0.76f, 0.32f, 0.55f));
        Place(promptBox, new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(300, 46));
        promptText = NewText(promptBox, "", 23, TextAnchor.MiddleCenter, Text);
        Stretch(promptText.rectTransform);
        promptBox.gameObject.SetActive(false);

        var toast = Box(h, "Toast", new Color(0.03f, 0.03f, 0.03f, 0.65f), FrameColor);
        Place(toast, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(980, 60));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0;
        toastText = NewText(toast, "", 23, TextAnchor.MiddleCenter, Text);
        Stretch(toastText.rectTransform, 20, 4);
    }

    RectTransform MakeBar(RectTransform parent, string label, int index, Color color, out UnityEngine.UI.Text value)
    {
        float y = -24 - index * 34;
        var lbl = NewText(parent, label, 19, TextAnchor.MiddleLeft, Dim);
        Place(lbl.rectTransform, new Vector2(0, 1), new Vector2(16, y + 12), new Vector2(80, 24));
        var bg = NewRect(label + "Bar", parent);
        Place(bg, new Vector2(0, 1), new Vector2(96, y + 6), new Vector2(178, 12));
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = UIStyle.Fill;
        bgImg.type = Image.Type.Sliced;
        bgImg.pixelsPerUnitMultiplier = 3;
        bgImg.color = new Color(1, 1, 1, 0.08f);
        var fill = NewRect("Fill", bg);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        var img = fill.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Fill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 3;
        img.color = color;
        value = NewText(parent, "", 19, TextAnchor.MiddleRight, Text);
        Place(value.rectTransform, new Vector2(0, 1), new Vector2(282, y + 12), new Vector2(44, 24));
        return fill;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var s = gm.State;

        hud.SetActive(PlayerController.Instance != null);
        if (hud.activeSelf) UpdateHud(gm, s);

        if (toastTimer > 0)
        {
            toastTimer -= Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Clamp01(toastTimer * 2);
        }

        // 피격 효과 + 체력이 낮을 때 붉은 맥박
        damageAlpha = Mathf.MoveTowards(damageAlpha, 0, Time.unscaledDeltaTime * 1.2f);
        bool inGame = PlayerController.Instance != null && !gm.IsGameOver;
        float lowHealth = s.Health < 30 && inGame ? (0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 6)) * (1 - s.Health / 30f) : 0;
        damageOverlay.color = new Color(0.45f, 0, 0, Mathf.Max(damageAlpha, lowHealth));

        // FPS 표시 (설정에서 켜고 끈다)
        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f)
        {
            fpsText.text = GameSettings.ShowFps ? $"{fpsFrames / fpsTimer:0} FPS" : "";
            fpsFrames = 0;
            fpsTimer = 0;
        }

        if (IsPatchNotesOpen) UpdatePatchBlur();
        HandleKeys(gm);

        bool needCursor = IsBlocking || gm.IsGameOver || PlayerController.Instance == null;
        Cursor.lockState = needCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = needCursor;
    }

    void UpdateHud(GameManager gm, GameState s)
    {
        SetBar(healthFill, healthValue, s.Health);
        SetBar(hungerFill, hungerValue, s.Hunger);
        SetBar(thirstFill, thirstValue, s.Thirst);

        var city = CityDirector.Instance;
        if (city != null)
        {
            float t = Mathf.Max(0, city.TimeLeft);
            infoText.text = $"남은 시간  {(int)t / 60}:{(int)t % 60:00}\n<size=19><color=#a8a294>턴 {s.Turn} / {GameState.MaxTurns}</color></size>";
            infoText.color = t < 60 ? Danger : Text;
        }
        else
        {
            infoText.text = $"턴  {s.Turn} / {GameState.MaxTurns}\n<size=19><color=#a8a294>전력 {s.Power:0}%</color></size>";
            infoText.color = Text;
        }

        bool hasLight = s.Carried.Count("flashlight") > 0;
        flashBox.SetActive(hasLight);
        if (hasLight)
            flashlightText.text = $"손전등  <color={(s.FlashlightCharge < 20 ? "#f05a48" : "#f9c252")}>{s.FlashlightCharge:0}%</color>   배터리 {s.Carried.Count("battery")}";

        UpdateHotbar(s);
    }

    void UpdateHotbar(GameState s)
    {
        var hotbar = PlayerHotbar.Instance;
        var items = s.Carried.Sorted();
        int selected = hotbar != null ? hotbar.SelectedIndex : -1;
        string sig = selected + "|";
        foreach (var kv in items) sig += kv.Key + kv.Value + ",";
        if (sig == hotbarSignature) return;
        hotbarSignature = sig;

        for (int i = 0; i < SlotCount; i++)
        {
            bool has = i < items.Count;
            bool isSel = i == selected;
            slotIcons[i].enabled = has;
            if (has) slotIcons[i].sprite = ItemIcons.Get(items[i].Key);
            slotCounts[i].text = has && items[i].Value > 1 ? items[i].Value.ToString() : "";
            slotFrames[i].color = isSel ? Accent : FrameColor;
            slotFills[i].color = isSel ? new Color(0.22f, 0.17f, 0.08f, 0.75f) : new Color(0.03f, 0.03f, 0.03f, 0.6f);
            slotFills[i].rectTransform.localScale = Vector3.one * (isSel ? 1.08f : 1f);
        }

        if (selected >= 0 && selected < items.Count)
        {
            var def = ItemDatabase.Get(items[selected].Key);
            string action = def.IsConsumable ? "우클릭: 사용" : def.Id == "club" ? "좌클릭: 휘두르기" : def.Id == "flashlight" ? "Q: 켜기/끄기" : def.Id == "fuel" ? "방공호 발전기 연료" : "방공호 제작대 재료";
            itemNameText.text = $"{def.Name} x{items[selected].Value}   <size=17><color=#a8a294>{action}</color></size>";
        }
        else itemNameText.text = items.Count > SlotCount ? $"<size=17><color=#a8a294>+{items.Count - SlotCount}개 더 (Tab)</color></size>" : "";
    }

    void HandleKeys(GameManager gm)
    {
        var kb = Keyboard.current;
        if (kb == null || gm.IsTransitioning) return;
        bool inGame = PlayerController.Instance != null && !gm.IsGameOver;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (IsPatchNotesOpen) ClosePatchNotes();
            else if (IsPanelOpen) { if (panelCloseButton.activeSelf) ClosePanel(); }
            else if (inGame) SetPaused(!IsPaused);
        }
        else if (kb.tabKey.wasPressedThisFrame && inGame && !IsPaused)
        {
            if (IsPanelOpen && panelId == "inventory") ClosePanel();
            else if (!IsPanelOpen) OpenInventory();
        }
    }

    static void SetBar(RectTransform fill, UnityEngine.UI.Text value, float v)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(v / 100f), 1);
        value.text = Mathf.CeilToInt(v).ToString();
        value.color = v < 25 ? Danger : Text;
    }

    public void SetPrompt(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            promptBox.gameObject.SetActive(false);
            return;
        }
        promptText.text = text;
        promptBox.sizeDelta = new Vector2(promptText.preferredWidth + 48, 46);
        promptBox.gameObject.SetActive(true);
    }

    public void ShowMessage(string text, float duration = 2.5f)
    {
        toastText.text = text;
        toastTimer = duration;
    }

    public void FlashDamage(float strength) => damageAlpha = Mathf.Max(damageAlpha, 0.25f + 0.35f * strength);

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

    // ---------- 창(Panel) ----------

    void BuildPanel()
    {
        var p = NewRect("Panel", root);
        Stretch(p);
        p.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
        panel = p.gameObject;

        var box = Box(p, "Box", new Color(0.05f, 0.05f, 0.045f, 0.9f), FrameColor);
        Place(box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 740));

        panelTitle = NewText(box, "", 32, TextAnchor.MiddleLeft, Accent, FontBold);
        panelTitle.rectTransform.anchorMin = new Vector2(0, 1);
        panelTitle.rectTransform.anchorMax = new Vector2(1, 1);
        panelTitle.rectTransform.pivot = new Vector2(0.5f, 1);
        panelTitle.rectTransform.anchoredPosition = new Vector2(0, -16);
        panelTitle.rectTransform.sizeDelta = new Vector2(-64, 52);
        var line = NewRect("TitleLine", box);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, -72);
        line.sizeDelta = new Vector2(-64, 2);
        line.gameObject.AddComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, 0.35f);

        var view = NewRect("View", box);
        view.anchorMin = Vector2.zero;
        view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(32, 88);
        view.offsetMax = new Vector2(-32, -86);
        view.gameObject.AddComponent<RectMask2D>();
        view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // 스크롤 영역 클릭용

        panelContent = NewRect("Content", view);
        panelContent.anchorMin = new Vector2(0, 1);
        panelContent.anchorMax = new Vector2(1, 1);
        panelContent.pivot = new Vector2(0.5f, 1);
        panelContent.sizeDelta = Vector2.zero;
        var layout = panelContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        panelContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = box.gameObject.AddComponent<ScrollRect>();
        scroll.content = panelContent;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;

        var close = MakeButton(box, "닫기 (Esc)", ClosePanel, true);
        var crt = (RectTransform)close.transform;
        Place(crt, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(240, 50));
        panelCloseButton = close.gameObject;

        panel.SetActive(false);
    }

    public void OpenPanel(string title, Action<RectTransform> build, string id = null, bool closable = true, Action afterClose = null)
    {
        panelId = id;
        panelBuilder = build;
        afterPanelClosed = afterClose;
        panelTitle.text = title;
        panelCloseButton.SetActive(closable);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        fadeOverlay.transform.SetAsLastSibling();
        RefreshPanel();
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
    }

    public void ClosePanel()
    {
        bool wasOpen = IsPanelOpen;
        if (panel != null) panel.SetActive(false);
        panelBuilder = null;
        panelId = null;
        var after = afterPanelClosed;
        afterPanelClosed = null;
        if (wasOpen) after?.Invoke();
    }

    public void ShowEndScreen(string title, string body)
    {
        SetPaused(false);
        OpenPanel(title, c =>
        {
            Line(c, body, 26);
            Line(c, "");
            var row = Row(c, false);
            RowButton(row, "처음부터 다시", () => GameManager.Instance.Restart(), true, 260);
            RowButton(row, "메인 메뉴로", () => GameManager.Instance.GoToMainMenu(), true, 260);
        }, "end", false);
    }

    public void OpenInventory()
    {
        OpenPanel("가방", c =>
        {
            var s = GameManager.Instance.State;
            Line(c, $"무게 {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg", 22, Dim);
            if (s.Carried.IsEmpty) Line(c, "가진 것이 없다.");
            foreach (var kv in s.Carried.Sorted())
            {
                var def = ItemDatabase.Get(kv.Key);
                var row = Row(c);
                RowIcon(row, def.Id);
                RowLabel(row, $"{def.Name} x{kv.Value}   <size=18><color=#a8a294>{def.Description}</color></size>");
                if (def.IsConsumable)
                    RowButton(row, "사용", () => { GameManager.Instance.UseItem(def.Id, s.Carried); RefreshPanel(); });
                RowButton(row, "버리기", () =>
                {
                    if (PlayerHotbar.Instance != null) PlayerHotbar.Instance.Drop(def.Id);
                    RefreshPanel();
                });
            }
        }, "inventory");
    }

    // ---------- 창 내용 만들기 도우미 ----------

    public UnityEngine.UI.Text Line(RectTransform parent, string text, int size = 22, Color? color = null)
    {
        var t = NewText(parent, text, size, TextAnchor.MiddleLeft, color ?? Text);
        t.gameObject.AddComponent<LayoutElement>().minHeight = size + 10;
        return t;
    }

    public RectTransform Row(RectTransform parent, bool background = true)
    {
        var r = NewRect("Row", parent);
        if (background)
        {
            var bg = r.gameObject.AddComponent<Image>();
            bg.sprite = UIStyle.Fill;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1, 1, 1, 0.045f);
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

    public void RowIcon(RectTransform row, string itemId)
    {
        var img = NewRect("Icon", row).gameObject.AddComponent<Image>();
        img.sprite = ItemIcons.Get(itemId);
        img.preserveAspect = true;
        img.raycastTarget = false;
        var le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 40;
    }

    public Button RowButton(RectTransform row, string label, Action onClick, bool interactable = true, float width = 110)
    {
        var b = MakeButton(row, label, onClick, interactable);
        var le = b.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        return b;
    }

    Button MakeButton(Transform parent, string label, Action onClick, bool interactable, int fontSize = 20)
    {
        var rt = Box(parent, "Button", Color.white, interactable ? new Color(0.9f, 0.8f, 0.55f, 0.4f) : new Color(1, 1, 1, 0.08f));
        var img = rt.GetComponent<Image>();
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.normalColor = new Color(0.17f, 0.16f, 0.13f, 0.92f);
        colors.highlightedColor = new Color(0.42f, 0.33f, 0.16f, 0.95f);
        colors.pressedColor = new Color(0.58f, 0.43f, 0.18f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.08f, 0.08f, 0.07f, 0.8f);
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.interactable = interactable;
        b.onClick.AddListener(() => onClick());
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
