using UnityEngine;
using UnityEngine.UI;

// HUD 왼쪽 아래 상태 판(체력·갈증·배고픔·스테미너, 90% 크기)과 오른쪽 위 남은 시간 유리판, HUD 움직임.
// 시간 판은 뒤 화면을 흐려 비추는 어두운 유리(UIManager.HudGlass). 상태 판은 손전등 줄처럼 먹이 번진 자국 위에 놓인다 (2026-11-05 저녁).
// 상태 계기: 아이콘 + 크레파스로 그은 색 선(옛 막대의 1/4쯤 굵기, UIStyle.Crayon) + 숫자(모노 글꼴). 빈 자리에는 옅은 밑선.
// 값이 줄면 깎인 만큼 종이색 선이 남았다가 따라 줄고, 오르면 반짝이며, 25 아래면 선·숫자가 붉어지고 아이콘 뒤가 붉게 고동친다. 맞으면 판이 흔들린다.
// 남은 시간: 모래시계 + 큰 숫자 + 20칸 눈금 + 위험도. 1분 아래면 붉게 고동치고, 붉은 밤이면 경과 시간을 센다
public partial class UIManager
{
    class StatBar
    {
        public RectTransform Fill, Chip;
        public Image FillImage, ChipImage, Icon, Halo;
        public UnityEngine.UI.Text Value;
        public Color Color;
        public float Shown = -1, ChipValue, ChipDelay, Glow;
        public int Number = int.MinValue;
    }

    // 계기 색 (크레파스): 체력은 붉은색, 갈증은 바랜 파랑, 배고픔은 구운 고기 같은 갈색, 스테미너는 노란색
    static readonly Color StatHealth = new Color(0.82f, 0.17f, 0.13f);
    static readonly Color StatThirst = new Color(0.42f, 0.62f, 0.86f);
    static readonly Color StatHunger = new Color(0.74f, 0.44f, 0.2f);
    static readonly Color StatStamina = new Color(0.88f, 0.7f, 0.2f);
    static readonly Color TileFill = new Color(0.07f, 0.065f, 0.06f, 0.9f);
    // 크레파스 선: 길이(값 100%), 선 그림의 높이(실제 선은 그 안의 6~7px, 사용자 요청으로 처음의 1.5배), 빈 자리의 옅은 밑선
    const float StatBarWidth = 250, StatStrokeH = 24, StatusScale = 0.9f;
    static readonly Color StatGuide = new Color(0.9f, 0.86f, 0.78f, 0.13f);

    readonly StatBar[] statBars = new StatBar[4];
    RectTransform statusPanel, timePanel, hotbarTray, hotbarBar, toastRect;
    UnityEngine.UI.Text statusTag, timeLabel, timeValue, timeSub;
    Image timeIcon, timeFrame;
    readonly Image[] timeSegs = new Image[20];
    float statusShake, hudIntro = 1f, promptPop = 1f;
    string promptShown;

    void BuildStatusPanel(Transform h)
    {
        // 뒤에는 손전등·배터리 줄과 같은 먹 번진 자국 (뒤 화면을 흐려 비추는 유리) — 2026-11-05 저녁, 유리판에서 바꿈
        statusPanel = NewRect("Status", h);
        Place(statusPanel, new Vector2(0, 0), new Vector2(24, 24), new Vector2(376, 168));
        statusPanel.localScale = Vector3.one * StatusScale;
        var ink = NewRect("Ink", statusPanel).gameObject.AddComponent<Image>();
        Place(ink.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-4, -2), new Vector2(690, 340));
        ink.rectTransform.localRotation = Quaternion.Euler(0, 0, 2.5f);
        ink.sprite = UIStyle.InkWash;
        ink.material = GlassMaterial();
        ink.color = new Color(0.1f, 0.1f, 0.11f, 0.5f);
        ink.raycastTarget = false;
        // 짐 경고: 판 바로 위에 작게
        statusTag = NewText(statusPanel, "", 16, TextAnchor.LowerLeft, Alert, FontTitle);
        Place(statusTag.rectTransform, new Vector2(0, 1), new Vector2(6, 4), new Vector2(370, 24));
        statusTag.rectTransform.pivot = new Vector2(0, 0);
        statusTag.rectTransform.anchoredPosition = new Vector2(6, 4);
        statusTag.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);

        statBars[0] = MakeStatBar(statusPanel, "heart", "체력", 0, StatHealth);
        statBars[1] = MakeStatBar(statusPanel, "bottle", "갈증", 1, StatThirst);
        statBars[2] = MakeStatBar(statusPanel, "meat", "배고픔", 2, StatHunger);
        statBars[3] = MakeStatBar(statusPanel, "bolt", "스테미너", 3, StatStamina);
    }

    // 줄 하나: [아이콘] 크레파스 선 (빈 자리엔 옅은 밑선, 깎인 자국은 종이색 선) 숫자. 아이콘 뒤의 붉은 빛은 25 아래일 때만 켜진다
    StatBar MakeStatBar(RectTransform parent, string icon, string label, int index, Color color)
    {
        var b = new StatBar { Color = color };
        float y = -26 - index * 36; // 줄 가운데 (판 위에서부터)
        var halo = NewRect(label + "Halo", parent);
        Place(halo, new Vector2(0, 1), new Vector2(23, y), new Vector2(48, 48));
        halo.pivot = new Vector2(0.5f, 0.5f);
        halo.anchoredPosition = new Vector2(23, y);
        b.Halo = halo.gameObject.AddComponent<Image>();
        b.Halo.sprite = UIStyle.Glow;
        b.Halo.color = Color.clear;
        b.Halo.raycastTarget = false;
        // 아이콘은 입체감 있게 그린 색 그림 (HudIcons.Shaded)이라 색을 입히지 않는다
        var ic = NewRect("Icon", parent);
        Place(ic, new Vector2(0, 1), new Vector2(23, y), new Vector2(26, 26));
        ic.pivot = new Vector2(0.5f, 0.5f);
        ic.anchoredPosition = new Vector2(23, y);
        b.Icon = ic.gameObject.AddComponent<Image>();
        b.Icon.sprite = HudIcons.Shaded(icon);
        b.Icon.color = Color.white;
        b.Icon.raycastTarget = false;

        // 선: 옅은 밑선(전체 길이) → 깎인 자국(Chip, 종이색) → 색 선(Fill). 셋 다 같은 크레파스 그림이고 길이만 다르다
        CrayonStroke(parent, label + "Guide", y, StatBarWidth, StatGuide);
        b.ChipImage = CrayonStroke(parent, label + "Chip", y, StatBarWidth, new Color(Pale.r, Pale.g, Pale.b, 0.6f));
        b.Chip = b.ChipImage.rectTransform;
        b.FillImage = CrayonStroke(parent, label + "Fill", y, StatBarWidth, color);
        b.Fill = b.FillImage.rectTransform;

        // 숫자 (모노 글꼴, 줄 끝에 오른쪽 정렬)
        b.Value = NewText(parent, "", 18, TextAnchor.MiddleRight, Pale, FontMono);
        Place(b.Value.rectTransform, new Vector2(0, 1), new Vector2(304, y), new Vector2(62, 26));
        b.Value.rectTransform.pivot = new Vector2(0, 0.5f);
        b.Value.rectTransform.anchoredPosition = new Vector2(304, y);
        b.Value.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.75f);
        return b;
    }

    // 크레파스로 그은 선 하나 (왼쪽 x=46에서 시작, 길이 width). Tiled라 길이가 줄어도 결이 늘어나지 않고 끝이 뭉툭하게 남는다
    static Image CrayonStroke(RectTransform parent, string name, float y, float width, Color color)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(46, y);
        rt.sizeDelta = new Vector2(width, StatStrokeH);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Crayon;
        img.type = Image.Type.Tiled;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 막대 하나: 부드럽게 따라가고, 줄면 흰 자국을 남기고, 오르면 반짝이고, 낮으면 깜빡인다
    void UpdateStatBar(StatBar b, float value, float max)
    {
        float dt = Time.unscaledDeltaTime;
        float target = max > 0 ? Mathf.Clamp01(value / max) : 0;
        if (b.Shown < 0) { b.Shown = b.ChipValue = target; }
        if (target < b.Shown - 0.0001f)
        {
            if (b.Shown - target > 0.02f) b.ChipDelay = 0.45f; // 크게 깎이면 자국이 잠깐 머문다
            b.ChipValue = Mathf.Max(b.ChipValue, b.Shown);
        }
        else if (target > b.Shown + 0.02f) b.Glow = 1f;
        b.Shown = Mathf.MoveTowards(b.Shown, target, dt * Mathf.Max(0.6f, Mathf.Abs(target - b.Shown) * 8f));
        if (b.ChipDelay > 0) b.ChipDelay -= dt;
        else b.ChipValue = Mathf.MoveTowards(b.ChipValue, b.Shown, dt * 0.7f);
        b.ChipValue = Mathf.Max(b.ChipValue, b.Shown);
        // 선 길이 = 값. 아주 짧아도 크레파스 끝이 남게 최소 8px. 깎인 자국은 색 선보다 길 때만 보인다
        b.Fill.sizeDelta = new Vector2(Mathf.Max(8f, b.Shown * StatBarWidth), StatStrokeH);
        b.Chip.sizeDelta = new Vector2(Mathf.Max(8f, b.ChipValue * StatBarWidth), StatStrokeH);
        b.ChipImage.enabled = b.ChipValue > b.Shown + 0.002f;

        bool low = target < 0.25f;
        float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f) : 0;
        b.Glow = Mathf.MoveTowards(b.Glow, 0, dt * 2.5f);
        var c = Color.Lerp(b.Color, Pale, b.Glow * 0.45f);
        if (low) c = Color.Lerp(c, Alert, 0.35f + 0.3f * pulse);
        b.FillImage.color = c;
        b.Halo.color = low ? new Color(0.9f, 0.12f, 0.06f, 0.25f + 0.45f * pulse) : Color.clear;
        b.Icon.rectTransform.localScale = Vector3.one * (1 + b.Glow * 0.25f + pulse * 0.1f);
        b.Value.color = low ? Color.Lerp(Alert, Pale, 0.3f * pulse) : Pale;

        int n = Mathf.CeilToInt(value);
        if (n != b.Number)
        {
            b.Number = n;
            b.Value.text = n.ToString();
        }
    }

    void UpdateStatusPanel(GameState s)
    {
        var pc = PlayerController.Instance;
        UpdateStatBar(statBars[0], s.Health, s.MaxHealth);
        UpdateStatBar(statBars[1], s.Thirst, 100f);
        UpdateStatBar(statBars[2], s.Hunger, 100f);
        UpdateStatBar(statBars[3], pc != null ? pc.Stamina : 100f, GameBalance.Data.staminaMax);
        // 짐이 무거우면 스테미너가 빨리 닳는다고 알린다
        bool heavy = pc != null && pc.IsHeavy;
        string tag = heavy ? "짐이 무겁다 · 스테미너 소모 ↑" : "";
        if (statusTag.text != tag) statusTag.text = tag;
        if (heavy) statusTag.color = Color.Lerp(Alert, Accent, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));

        // 맞으면 판이 흔들린다 + 처음 나타날 때 왼쪽에서 미끄러져 들어온다
        statusShake = Mathf.MoveTowards(statusShake, 0, Time.unscaledDeltaTime * 3f);
        var shake = statusShake > 0 ? new Vector2(Mathf.Sin(Time.unscaledTime * 70f), Mathf.Cos(Time.unscaledTime * 53f)) * 6f * statusShake : Vector2.zero;
        float intro = 1 - Ease(hudIntro);
        statusPanel.anchoredPosition = new Vector2(24 - 420 * intro, 24) + shake;
    }

    // ---------- 남은 시간 계기판 ----------

    void BuildTimePanel(Transform h)
    {
        timePanel = HudGlass(h, "Time", 18f);
        Place(timePanel, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(318, 128));
        timeFrame = timePanel.Find("Frame").GetComponent<Image>();
        var tile = NewRect("IconTile", timePanel);
        Place(tile, new Vector2(0, 1), new Vector2(14, -14), new Vector2(36, 36));
        var tileImg = tile.gameObject.AddComponent<Image>();
        tileImg.sprite = UIStyle.Fill;
        tileImg.type = Image.Type.Sliced;
        tileImg.pixelsPerUnitMultiplier = 3;
        tileImg.color = TileFill;
        tileImg.raycastTarget = false;
        var ic = NewRect("Icon", tile);
        Stretch(ic, 6, 6);
        timeIcon = ic.gameObject.AddComponent<Image>();
        timeIcon.raycastTarget = false;
        timeLabel = NewText(timePanel, "", 17, TextAnchor.UpperLeft, PaleDim, FontTitle);
        Place(timeLabel.rectTransform, new Vector2(0, 1), new Vector2(60, -15), new Vector2(150, 24));
        timeValue = NewText(timePanel, "", 42, TextAnchor.UpperRight, Pale, FontMono);
        Place(timeValue.rectTransform, new Vector2(1, 1), new Vector2(-16, -6), new Vector2(200, 50));
        timeValue.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
        // 20칸 눈금
        const float segW = 12.4f, segGap = 2.5f;
        for (int i = 0; i < timeSegs.Length; i++)
        {
            var seg = NewRect("Seg" + i, timePanel);
            Place(seg, new Vector2(0, 1), new Vector2(14 + i * (segW + segGap), -66), new Vector2(segW, 12));
            var img = seg.gameObject.AddComponent<Image>();
            img.sprite = UIStyle.Fill;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 6;
            img.raycastTarget = false;
            timeSegs[i] = img;
        }
        timeSub = NewText(timePanel, "", 15, TextAnchor.UpperLeft, PaleDim);
        Place(timeSub.rectTransform, new Vector2(0, 1), new Vector2(14, -90), new Vector2(296, 30));
    }

    // ---------- 날씨 명판 (왼쪽 위, 도시에서만) ----------

    RectTransform weatherPanel;
    Image weatherIcon;
    UnityEngine.UI.Text weatherLabel;
    WeatherKind weatherShown = (WeatherKind)(-1);

    void BuildWeatherPanel(Transform h)
    {
        weatherPanel = HudGlass(h, "Weather", 14f);
        Place(weatherPanel, new Vector2(0, 1), new Vector2(24, -24), new Vector2(172, 58));
        var tile = NewRect("IconTile", weatherPanel);
        Place(tile, new Vector2(0, 1), new Vector2(10, -9), new Vector2(40, 40));
        var tileImg = tile.gameObject.AddComponent<Image>();
        tileImg.sprite = UIStyle.Fill;
        tileImg.type = Image.Type.Sliced;
        tileImg.pixelsPerUnitMultiplier = 3;
        tileImg.color = TileFill;
        tileImg.raycastTarget = false;
        var ic = NewRect("Icon", tile);
        Stretch(ic, 2, 2);
        weatherIcon = ic.gameObject.AddComponent<Image>();
        weatherIcon.raycastTarget = false;
        weatherLabel = NewText(weatherPanel, "", 22, TextAnchor.MiddleLeft, Pale, FontTitle);
        Place(weatherLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(104, 30));
        weatherPanel.gameObject.SetActive(false);
    }

    void UpdateWeatherPanel()
    {
        bool show = Weather.Instance != null;
        if (weatherPanel.gameObject.activeSelf != show) weatherPanel.gameObject.SetActive(show);
        if (!show) return;
        var k = Weather.Current;
        if (k == weatherShown) return;
        weatherShown = k;
        weatherIcon.sprite = HudIcons.Shaded(k == WeatherKind.Fog ? "fog" : k == WeatherKind.Rain ? "rain" : "sun");
        weatherLabel.text = Weather.Name(k);
    }

    // ---------- 지금 할 일 명판 (왼쪽 위, 날씨 명판이 있으면 그 아래) ----------
    // 게임의 목적 한 줄 + 지금 할 일 + 왜 해야 하는지. 내용은 Tutorial이 정한다

    RectTransform objectivePanel;
    UnityEngine.UI.Text objectiveLine, objectiveWhy;
    string objectiveShown;

    void BuildObjectivePanel(Transform h)
    {
        objectivePanel = HudGlass(h, "Objective", 16f);
        Place(objectivePanel, new Vector2(0, 1), new Vector2(24, -24), new Vector2(364, 98));
        var purpose = NewText(objectivePanel, "목적 · " + Tutorial.Purpose, 14, TextAnchor.UpperLeft, PaleDim, FontTitle);
        Place(purpose.rectTransform, new Vector2(0, 1), new Vector2(14, -10), new Vector2(340, 20));
        objectiveLine = NewText(objectivePanel, "", 20, TextAnchor.UpperLeft, Pale, FontTitle);
        Place(objectiveLine.rectTransform, new Vector2(0, 1), new Vector2(14, -32), new Vector2(340, 28));
        objectiveWhy = NewText(objectivePanel, "", 13, TextAnchor.UpperLeft, PaleDim);
        Place(objectiveWhy.rectTransform, new Vector2(0, 1), new Vector2(14, -60), new Vector2(340, 34));
    }

    void UpdateObjectivePanel()
    {
        if (objectivePanel.gameObject.activeSelf != GameSettings.Tips) objectivePanel.gameObject.SetActive(GameSettings.Tips);
        if (!GameSettings.Tips) return;
        objectivePanel.anchoredPosition = new Vector2(24, weatherPanel != null && weatherPanel.gameObject.activeSelf ? -92f : -24f);
        string line = (Tutorial.Done ? "" : "○ ") + Tutorial.Line;
        if (line == objectiveShown) return;
        objectiveShown = line;
        objectiveLine.text = line;
        objectiveWhy.text = Tutorial.Why;
    }

    // ---------- 해치 나침반 (도시에서만, 화면 위 가운데) ----------
    // 가는 띠 위에서 집 표식이 해치 방향으로 움직이고 거리를 적는다. 붉은 밤이면 붉게 깜빡인다

    RectTransform compass, compassMark;
    Image compassIcon;
    UnityEngine.UI.Text compassText;
    CityExit cityExit;

    void BuildCompass(Transform h)
    {
        compass = NewRect("Compass", h);
        Place(compass, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(440, 34));
        var line = NewRect("Line", compass);
        line.anchorMin = new Vector2(0, 0.5f);
        line.anchorMax = new Vector2(1, 0.5f);
        line.offsetMin = new Vector2(0, -1);
        line.offsetMax = new Vector2(0, 1);
        var li = line.gameObject.AddComponent<Image>();
        li.color = new Color(Pale.r, Pale.g, Pale.b, 0.3f);
        li.raycastTarget = false;
        var tick = NewRect("Tick", compass);
        Place(tick, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 12));
        var ti = tick.gameObject.AddComponent<Image>();
        ti.color = new Color(Pale.r, Pale.g, Pale.b, 0.55f);
        ti.raycastTarget = false;
        compassMark = NewRect("Mark", compass);
        Place(compassMark, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 34));
        var icon = NewRect("Icon", compassMark);
        Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(16, 16));
        compassIcon = icon.gameObject.AddComponent<Image>();
        compassIcon.sprite = HudIcons.Get("home");
        compassIcon.color = Accent;
        compassIcon.raycastTarget = false;
        compassText = NewText(compassMark, "", 13, TextAnchor.UpperCenter, Pale, FontMono);
        Place(compassText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -3), new Vector2(120, 16));
        compassText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
        compass.gameObject.SetActive(false);
    }

    void UpdateCompass()
    {
        bool show = GameSettings.Tips && CityDirector.Instance != null && Camera.main != null;
        if (compass.gameObject.activeSelf != show) compass.gameObject.SetActive(show);
        if (!show) return;
        if (cityExit == null) cityExit = FindFirstObjectByType<CityExit>();
        if (cityExit == null) return;
        var cam = Camera.main.transform;
        var to = cityExit.transform.position - cam.position;
        float dist = to.magnitude;
        to.y = 0;
        var fwd = cam.forward;
        fwd.y = 0;
        float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
        compassMark.anchoredPosition = new Vector2(Mathf.Clamp(angle / 75f, -1f, 1f) * 200f, 0);
        compassText.text = $"{dist:0}m";
        bool red = CityDirector.Instance.IsRedNight;
        var c = red ? Color.Lerp(Alert, Pale, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) : Accent;
        float visible = Mathf.Abs(angle) > 75f ? 0.55f : 1f; // 시야 밖이면 가장자리에서 반투명
        compassIcon.color = new Color(c.r, c.g, c.b, visible);
        compassText.color = red ? c : Pale;
    }

    // ---------- 씬에 들어올 때 잠깐 보이는 조작키 안내 ----------

    UnityEngine.UI.Text keyHints;
    float keyHintTimer;

    void BuildKeyHints(Transform h)
    {
        keyHints = NewText(h, "", 17, TextAnchor.MiddleCenter, Pale, FontTitle);
        Place(keyHints.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 148), new Vector2(1200, 26));
        keyHints.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        keyHints.color = new Color(Pale.r, Pale.g, Pale.b, 0);
    }

    public void ShowKeyHints()
    {
        if (keyHints == null || !GameSettings.Tips) return;
        string K(GameAction a) => KeyBindings.Label(a);
        keyHints.text = $"{K(GameAction.Interact)}  줍기·사용·손전등 꺼내기        {K(GameAction.Inventory)}  소지품        {K(GameAction.Flashlight)}  손전등 켜기·끄기        {K(GameAction.Run)}  달리기        {K(GameAction.Crouch)}  앉기        {K(GameAction.Attack)}  공격";
        keyHintTimer = 9f;
    }

    void UpdateKeyHints(float dt)
    {
        if (keyHints == null || (keyHintTimer <= 0 && keyHints.color.a <= 0)) return;
        if (!GameSettings.Tips) keyHintTimer = 0; // 보이는 중에 꺼도 바로 사라진다
        keyHintTimer -= dt;
        keyHints.color = new Color(Pale.r, Pale.g, Pale.b, Mathf.Clamp01(keyHintTimer / 1.2f)); // 마지막 1.2초 동안 사라진다
    }

    static readonly string[] DangerNames = { "낮음", "보통", "높음", "매우 높음", "극도" };

    void UpdateTimePanel(GameManager gm, GameState s)
    {
        var city = CityDirector.Instance;
        float now = Time.unscaledTime;
        Color frame = Color.clear; // 유리판 테두리는 경고 때만 붉게 켜진다
        if (city != null && city.IsRedNight)
        {
            // 붉은 밤: 지난 시간을 세고 계속 붉게 고동친다
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 5f);
            int t = (int)city.RedNightElapsed;
            SetTimeTexts("moon", "시간 초과", $"+{t / 60}:{t % 60:00}", $"체력 -{GameBalance.Data.redNightDamage:0} / {GameBalance.Data.redNightInterval:0}초 · 해치로 돌아가자!");
            timeLabel.color = timeValue.color = Color.Lerp(Alert, new Color(1f, 0.6f, 0.5f), pulse * 0.5f);
            timeIcon.color = Alert;
            for (int i = 0; i < timeSegs.Length; i++)
                timeSegs[i].color = Color.Lerp(new Color(0.45f, 0.04f, 0.03f, 0.9f), new Color(0.95f, 0.15f, 0.1f, 0.95f), Mathf.Repeat(i / 20f - now * 0.8f, 1f) < 0.25f ? 1 : pulse * 0.4f);
            frame = new Color(Alert.r, Alert.g, Alert.b, 0.5f + 0.4f * pulse);
        }
        else if (city != null)
        {
            float left = Mathf.Max(0, city.TimeLeft), total = Mathf.Max(1, CityDirector.DefaultTimeLimit);
            int t = Mathf.CeilToInt(left);
            float ratio = left / total;
            bool hurry = left <= 60;
            if (hurry) Tutorial.Hint(Tutorial.HintRedNight, "남은 시간이 1분이다. 화면 위 나침반의 집 표식을 따라 해치로 돌아가자.");
            float pulse = hurry ? 0.5f + 0.5f * Mathf.Sin(now * 6f) : 0;
            int danger = Mathf.Clamp(Mathf.FloorToInt(city.Danger * DangerNames.Length), 0, DangerNames.Length - 1);
            SetTimeTexts("hourglass", "남은 시간", $"{t / 60:00}:{t % 60:00}", $"위험도 {DangerNames[danger]}   ·   턴 {s.Turn} / {GameState.MaxTurns}");
            timeLabel.color = hurry ? Alert : PaleDim;
            timeValue.color = hurry ? Color.Lerp(Alert, Pale, pulse * 0.35f) : Pale;
            timeIcon.color = hurry ? Alert : Accent;
            timeValue.rectTransform.localScale = Vector3.one * (1 + (hurry ? pulse * 0.05f : 0));
            float filled = ratio * timeSegs.Length;
            for (int i = 0; i < timeSegs.Length; i++)
            {
                // 남은 칸은 호박색 → 빨강 (시간이 줄수록), 마지막 칸은 깜빡인다
                var on = Color.Lerp(new Color(0.95f, 0.3f, 0.15f, 0.95f), new Color(0.91f, 0.64f, 0.29f, 0.95f), ratio);
                float k = Mathf.Clamp01(filled - i);
                if (k > 0 && k < 1) on.a *= 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(now * 8f));
                timeSegs[i].color = k > 0 ? on : new Color(0, 0, 0, 0.35f);
            }
            if (hurry) frame = new Color(Alert.r, Alert.g, Alert.b, 0.4f + 0.4f * pulse);
        }
        else
        {
            // 방공호: 턴과 발전기 전력
            float power = Mathf.Clamp(s.Power, 0, 100);
            SetTimeTexts("home", "방공호 · 턴", $"{s.Turn} / {GameState.MaxTurns}", power <= 0 ? "전력 0% · 정전! 방폭문이 열렸다" : $"발전기 전력 {power:0}%");
            timeLabel.color = PaleDim;
            timeValue.color = Pale;
            timeIcon.color = Accent;
            timeValue.rectTransform.localScale = Vector3.one;
            float filled = power / 100f * timeSegs.Length;
            for (int i = 0; i < timeSegs.Length; i++)
                timeSegs[i].color = Mathf.Clamp01(filled - i) > 0 ? (power < 20 ? new Color(0.95f, 0.3f, 0.15f, 0.95f) : new Color(0.55f, 0.8f, 0.35f, 0.95f)) : new Color(0, 0, 0, 0.35f);
            timeSub.color = power <= 0 ? Alert : PaleDim;
        }
        timeFrame.color = frame;
        float intro = 1 - Ease(hudIntro);
        timePanel.anchoredPosition = new Vector2(-24 + 360 * intro, -24);
    }

    void SetTimeTexts(string icon, string label, string value, string sub)
    {
        var sprite = HudIcons.Get(icon);
        if (timeIcon.sprite != sprite) timeIcon.sprite = sprite;
        if (timeLabel.text != label) timeLabel.text = label;
        if (timeValue.text != value) timeValue.text = value;
        if (timeSub.text != sub) timeSub.text = sub;
    }

    // ---------- HUD 움직임 ----------

    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return 1 - (1 - t) * (1 - t) * (1 - t);
    }

    // 씬이 열리면 HUD가 가장자리에서 미끄러져 들어온다
    void RestartHudIntro() => hudIntro = 0f;

    void AnimateHud()
    {
        float dt = Time.unscaledDeltaTime;
        hudIntro = Mathf.Min(1f, hudIntro + dt / 0.7f);
        float intro = 1 - Ease(hudIntro);
        UpdateKeyHints(dt);
        if (hotbarTray != null) hotbarTray.anchoredPosition = new Vector2(0, 14 - 140 * intro);
        if (hotbarBar != null) hotbarBar.anchoredPosition = new Vector2(0, 30 - 140 * intro);
        if (flashBox != null) ((RectTransform)flashBox.transform).anchoredPosition = new Vector2(-28 + 320 * intro, 48);

        // 알림: 위에서 살짝 내려오며 나타나고 올라가며 사라진다
        if (toastRect != null)
        {
            float a = toastGroup.alpha;
            toastRect.anchoredPosition = new Vector2(0, 230 + (1 - Ease(a)) * 24f);
            toastRect.localScale = Vector3.one * (0.96f + 0.04f * Ease(a));
        }
        // 상호작용 안내: 새로 뜰 때 톡 튀어나온다
        if (promptBox != null && promptBox.gameObject.activeSelf)
        {
            promptPop = Mathf.Min(1f, promptPop + dt / 0.16f);
            float k = Ease(promptPop);
            promptBox.localScale = Vector3.one * (0.85f + 0.15f * k + Mathf.Sin(k * Mathf.PI) * 0.05f);
            promptText.color = new Color(Text.r, Text.g, Text.b, k);
        }
        // 손에 든 아이템 이름·쓰는 법: 칸을 바꿔도 떠오르거나 사라지지 않고 그 자리에서 글만 바뀐다 (뜨고 사라지는 게 번잡하다는 의견)
        itemNameText.color = Pale;
        itemNameText.rectTransform.anchoredPosition = new Vector2(0, 112 - 140 * intro);
    }
}
