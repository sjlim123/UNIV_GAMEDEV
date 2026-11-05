using UnityEngine;
using UnityEngine.UI;

// HUD 왼쪽 아래 상태 유리판(체력·갈증·배고픔·스테미너, 90% 크기)과 오른쪽 위 남은 시간 유리판, HUD 움직임.
// 판은 뒤 화면을 흐려 비추는 어두운 유리(UIManager.HudGlass): 가장자리는 투명하고 가운데로 갈수록 진해 글·막대가 또렷하다 (2026-11-05).
// 상태 계기: 아이콘 칸 + 눈금이 있는 홈 안의 색 막대 + 숫자(모노 글꼴) + 줄 끝의 전구.
// 값이 줄면 깎인 만큼 밝은 자국이 남았다가 따라 줄고, 오르면 반짝이며, 25 아래면 막대와 전구가 붉게 깜빡인다. 맞으면 판이 흔들린다.
// 남은 시간: 모래시계 + 큰 숫자 + 20칸 눈금 + 위험도. 1분 아래면 붉게 고동치고, 붉은 밤이면 경과 시간을 센다
public partial class UIManager
{
    class StatBar
    {
        public RectTransform Fill, Chip;
        public Image FillImage, Tile, Icon, Bulb;
        public UnityEngine.UI.Text Value;
        public Color Color;
        public float Shown = -1, ChipValue, ChipDelay, Glow;
        public int Number = int.MinValue;
    }

    // 계기 색: 체력은 어두운 붉은색, 갈증은 바랜 파랑, 배고픔은 구운 고기 같은 갈색, 스테미너는 어두운 노랑
    static readonly Color StatHealth = new Color(0.6f, 0.12f, 0.1f);
    static readonly Color StatThirst = new Color(0.36f, 0.54f, 0.74f);
    static readonly Color StatHunger = new Color(0.52f, 0.3f, 0.16f);
    static readonly Color StatStamina = new Color(0.7f, 0.54f, 0.12f);
    static readonly Color TileFill = new Color(0.07f, 0.065f, 0.06f, 0.9f);
    static readonly Color BulbOff = new Color(0.5f, 0.36f, 0.18f, 0.55f);
    const float StatBarWidth = 288, StatusScale = 0.9f;

    readonly StatBar[] statBars = new StatBar[4];
    RectTransform statusPanel, timePanel, hotbarTray, hotbarBar, toastRect;
    UnityEngine.UI.Text statusTag, timeLabel, timeValue, timeSub;
    Image timeIcon, timeFrame;
    readonly Image[] timeSegs = new Image[20];
    float statusShake, hudIntro = 1f, promptPop = 1f;
    string promptShown;

    void BuildStatusPanel(Transform h)
    {
        statusPanel = HudGlass(h, "Status", 20f);
        Place(statusPanel, new Vector2(0, 0), new Vector2(24, 24), new Vector2(376, 168));
        statusPanel.localScale = Vector3.one * StatusScale;
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

    StatBar MakeStatBar(RectTransform parent, string icon, string label, int index, Color color)
    {
        var b = new StatBar { Color = color };
        float y = -24 - index * 38; // 줄 가운데 (판 위에서부터)
        // 왼쪽 아이콘 칸 (어두운 사각형 + 흰 아이콘)
        var tile = NewRect(label + "Tile", parent);
        Place(tile, new Vector2(0, 1), new Vector2(12, y + 15), new Vector2(30, 30));
        tile.pivot = new Vector2(0, 1);
        tile.anchoredPosition = new Vector2(12, y + 15);
        b.Tile = tile.gameObject.AddComponent<Image>();
        b.Tile.sprite = UIStyle.Fill;
        b.Tile.type = Image.Type.Sliced;
        b.Tile.pixelsPerUnitMultiplier = 3;
        b.Tile.color = TileFill;
        b.Tile.raycastTarget = false;
        // 아이콘은 입체감 있게 그린 색 그림 (HudIcons.Shaded)이라 색을 입히지 않는다
        var ic = NewRect("Icon", tile);
        Stretch(ic, 3, 3);
        b.Icon = ic.gameObject.AddComponent<Image>();
        b.Icon.sprite = HudIcons.Shaded(icon);
        b.Icon.color = Color.white;
        b.Icon.raycastTarget = false;

        // 계기: 어두운 홈 → 깎인 자국(Chip) → 색 막대(Fill) → 눈금 → 숫자·이름, 줄 끝에 전구
        var track = NewRect(label + "Track", parent);
        track.anchorMin = track.anchorMax = new Vector2(0, 1);
        track.pivot = new Vector2(0, 0.5f);
        track.anchoredPosition = new Vector2(50, y);
        track.sizeDelta = new Vector2(StatBarWidth, 26);
        var trackImg = track.gameObject.AddComponent<Image>();
        trackImg.sprite = UIStyle.Fill;
        trackImg.type = Image.Type.Sliced;
        trackImg.pixelsPerUnitMultiplier = 4;
        trackImg.color = new Color(0.02f, 0.02f, 0.02f, 0.72f);
        trackImg.raycastTarget = false;

        b.Chip = NewRect("Chip", track);
        b.Chip.anchorMin = Vector2.zero;
        b.Chip.anchorMax = new Vector2(1, 1);
        b.Chip.offsetMin = new Vector2(2, 2);
        b.Chip.offsetMax = new Vector2(-2, -2);
        var chipImg = b.Chip.gameObject.AddComponent<Image>();
        chipImg.sprite = UIStyle.Fill;
        chipImg.type = Image.Type.Sliced;
        chipImg.pixelsPerUnitMultiplier = 4;
        chipImg.color = new Color(Pale.r, Pale.g, Pale.b, 0.7f);
        chipImg.raycastTarget = false;

        b.Fill = NewRect("Fill", track);
        b.Fill.anchorMin = Vector2.zero;
        b.Fill.anchorMax = new Vector2(1, 1);
        b.Fill.offsetMin = new Vector2(2, 2);
        b.Fill.offsetMax = new Vector2(-2, -2);
        b.FillImage = b.Fill.gameObject.AddComponent<Image>();
        b.FillImage.sprite = UIStyle.Fill;
        b.FillImage.type = Image.Type.Sliced;
        b.FillImage.pixelsPerUnitMultiplier = 4;
        b.FillImage.color = color;
        b.FillImage.raycastTarget = false;
        // 10칸 눈금 (막대 위에 어두운 선)
        for (int i = 1; i < 10; i++)
        {
            var tick = NewRect("Tick", track);
            tick.anchorMin = new Vector2(i / 10f, 0);
            tick.anchorMax = new Vector2(i / 10f, 1);
            tick.pivot = new Vector2(0.5f, 0.5f);
            tick.anchoredPosition = Vector2.zero;
            tick.sizeDelta = new Vector2(1, i % 5 == 0 ? -4 : -14);
            var tickImg = tick.gameObject.AddComponent<Image>();
            tickImg.color = new Color(0, 0, 0, 0.45f);
            tickImg.raycastTarget = false;
        }

        // 숫자만 적는다 (이름 글씨는 아이콘이 대신한다)
        b.Value = NewText(track, "", 19, TextAnchor.MiddleLeft, Pale, FontMono);
        Stretch(b.Value.rectTransform, 10, 0);
        b.Value.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.75f);

        // 줄 끝의 전구: 평소엔 꺼진 호박색, 25 아래면 붉게 깜빡인다
        var bulb = NewRect("Bulb", parent);
        Place(bulb, new Vector2(0, 1), new Vector2(StatBarWidth + 54, y), new Vector2(16, 16));
        bulb.pivot = new Vector2(0, 0.5f);
        bulb.anchoredPosition = new Vector2(StatBarWidth + 54, y);
        b.Bulb = bulb.gameObject.AddComponent<Image>();
        b.Bulb.sprite = UIStyle.Glow;
        b.Bulb.color = BulbOff;
        b.Bulb.raycastTarget = false;
        return b;
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
        b.Fill.anchorMax = new Vector2(Mathf.Max(0.0001f, b.Shown), 1);
        b.Chip.anchorMax = new Vector2(Mathf.Max(0.0001f, b.ChipValue), 1);

        bool low = target < 0.25f;
        float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f) : 0;
        b.Glow = Mathf.MoveTowards(b.Glow, 0, dt * 2.5f);
        var c = Color.Lerp(b.Color, Pale, b.Glow * 0.45f);
        if (low) c = Color.Lerp(c, Alert, 0.35f + 0.3f * pulse);
        b.FillImage.color = c;
        b.Tile.color = low ? Color.Lerp(TileFill, new Color(0.45f, 0.06f, 0.04f, 0.9f), pulse) : TileFill;
        b.Icon.rectTransform.localScale = Vector3.one * (1 + b.Glow * 0.25f + pulse * 0.08f);
        b.Bulb.color = low ? Color.Lerp(new Color(0.5f, 0.08f, 0.04f, 0.7f), new Color(1f, 0.35f, 0.2f, 1f), pulse) : BulbOff;
        b.Bulb.rectTransform.localScale = Vector3.one * (low ? 1.2f + 0.5f * pulse : 1f);

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
