using UnityEngine;
using UnityEngine.UI;

// HUD 왼쪽 아래 상태 막대(체력·갈증·배고픔·스테미너, 90% 크기)와 오른쪽 위 남은 시간 계기판, HUD 움직임.
// 상태 막대: 아이콘 칸 + 색 막대 안에 숫자 (참고 이미지 스타일). 값이 줄면 깎인 만큼 흰 자국이 남았다가 따라 줄고,
// 오르면 반짝이며, 25 아래면 깜빡인다. 맞으면 판이 흔들린다.
// 남은 시간: 모래시계 + 큰 숫자 + 20칸 눈금 + 위험도. 1분 아래면 붉게 고동치고, 붉은 밤이면 경과 시간을 센다
public partial class UIManager
{
    class StatBar
    {
        public RectTransform Fill, Chip;
        public Image FillImage, Tile, Icon, Shine;
        public UnityEngine.UI.Text Value;
        public Color Color;
        public float Shown = -1, ChipValue, ChipDelay, Glow;
        public int Number = int.MinValue;
    }

    static readonly Color StatGreen = new Color(0.36f, 0.72f, 0.27f);
    static readonly Color StatBlue = new Color(0.25f, 0.5f, 0.86f);
    static readonly Color StatOrange = new Color(0.9f, 0.4f, 0.2f);
    static readonly Color StatYellow = new Color(0.9f, 0.74f, 0.22f);
    const float StatBarWidth = 296, StatusScale = 0.9f;

    readonly StatBar[] statBars = new StatBar[4];
    RectTransform statusPanel, timePanel, hotbarTray, hotbarBar, toastRect;
    UnityEngine.UI.Text statusTag, timeLabel, timeValue, timeSub;
    Image timeIcon, timeFrame;
    readonly Image[] timeSegs = new Image[20];
    float statusShake, hudIntro = 1f, promptPop = 1f, itemNameFade = 1f;
    string promptShown;

    void BuildStatusPanel(Transform h)
    {
        statusPanel = HudGlass(h, "Status", HudFrame);
        Place(statusPanel, new Vector2(0, 0), new Vector2(24, 24), new Vector2(376, 168));
        statusPanel.localScale = Vector3.one * StatusScale;
        // 짐 경고: 판 바로 위에 작게
        statusTag = NewText(statusPanel, "", 16, TextAnchor.LowerLeft, Danger, FontSemiBold);
        Place(statusTag.rectTransform, new Vector2(0, 1), new Vector2(6, 4), new Vector2(370, 24));
        statusTag.rectTransform.pivot = new Vector2(0, 0);
        statusTag.rectTransform.anchoredPosition = new Vector2(6, 4);
        statusTag.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);

        statBars[0] = MakeStatBar(statusPanel, "plus", "체력", 0, StatGreen);
        statBars[1] = MakeStatBar(statusPanel, "drop", "갈증", 1, StatBlue);
        statBars[2] = MakeStatBar(statusPanel, "food", "배고픔", 2, StatOrange);
        statBars[3] = MakeStatBar(statusPanel, "bolt", "스테미너", 3, StatYellow);
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
        b.Tile.color = new Color(0.03f, 0.035f, 0.04f, 0.85f);
        b.Tile.raycastTarget = false;
        var ic = NewRect("Icon", tile);
        Stretch(ic, 5, 5);
        b.Icon = ic.gameObject.AddComponent<Image>();
        b.Icon.sprite = HudIcons.Get(icon);
        b.Icon.color = color;
        b.Icon.raycastTarget = false;

        // 막대: 어두운 홈 → 깎인 자국(Chip) → 색 막대(Fill) → 윗면 광택 → 숫자·이름
        var track = NewRect(label + "Track", parent);
        track.anchorMin = track.anchorMax = new Vector2(0, 1);
        track.pivot = new Vector2(0, 0.5f);
        track.anchoredPosition = new Vector2(50, y);
        track.sizeDelta = new Vector2(StatBarWidth, 26);
        var trackImg = track.gameObject.AddComponent<Image>();
        trackImg.sprite = UIStyle.Fill;
        trackImg.type = Image.Type.Sliced;
        trackImg.pixelsPerUnitMultiplier = 4;
        trackImg.color = new Color(0, 0, 0, 0.55f);
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
        chipImg.color = new Color(1f, 0.92f, 0.85f, 0.75f);
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
        var shine = NewRect("Shine", b.Fill);
        shine.anchorMin = new Vector2(0, 0.55f);
        shine.anchorMax = Vector2.one;
        shine.offsetMin = new Vector2(2, 0);
        shine.offsetMax = new Vector2(-2, -1);
        b.Shine = shine.gameObject.AddComponent<Image>();
        b.Shine.color = new Color(1, 1, 1, 0.16f);
        b.Shine.raycastTarget = false;

        b.Value = NewText(track, "", 18, TextAnchor.MiddleLeft, Color.white, FontBold);
        Stretch(b.Value.rectTransform, 10, 0);
        b.Value.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.75f);
        var name = NewText(track, label, 13, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.55f), FontSemiBold);
        Stretch(name.rectTransform, 10, 0);
        name.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
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
        var c = Color.Lerp(b.Color, Color.white, b.Glow * 0.45f);
        if (low) c = Color.Lerp(c, Danger, 0.35f + 0.3f * pulse);
        b.FillImage.color = c;
        b.Tile.color = low ? Color.Lerp(new Color(0.03f, 0.035f, 0.04f, 0.85f), new Color(0.45f, 0.06f, 0.04f, 0.9f), pulse) : new Color(0.03f, 0.035f, 0.04f, 0.85f);
        b.Icon.rectTransform.localScale = Vector3.one * (1 + b.Glow * 0.25f + pulse * 0.08f);

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
        if (heavy) statusTag.color = Color.Lerp(Danger, Accent, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));

        // 맞으면 판이 흔들린다 + 처음 나타날 때 왼쪽에서 미끄러져 들어온다
        statusShake = Mathf.MoveTowards(statusShake, 0, Time.unscaledDeltaTime * 3f);
        var shake = statusShake > 0 ? new Vector2(Mathf.Sin(Time.unscaledTime * 70f), Mathf.Cos(Time.unscaledTime * 53f)) * 6f * statusShake : Vector2.zero;
        float intro = 1 - Ease(hudIntro);
        statusPanel.anchoredPosition = new Vector2(24 - 420 * intro, 24) + shake;
    }

    // ---------- 남은 시간 계기판 ----------

    void BuildTimePanel(Transform h)
    {
        timePanel = HudGlass(h, "Time", HudFrame);
        Place(timePanel, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(318, 128));
        timeFrame = timePanel.Find("Frame").GetComponent<Image>();
        var tile = NewRect("IconTile", timePanel);
        Place(tile, new Vector2(0, 1), new Vector2(14, -14), new Vector2(36, 36));
        var tileImg = tile.gameObject.AddComponent<Image>();
        tileImg.sprite = UIStyle.Fill;
        tileImg.type = Image.Type.Sliced;
        tileImg.pixelsPerUnitMultiplier = 3;
        tileImg.color = new Color(0.03f, 0.035f, 0.04f, 0.85f);
        tileImg.raycastTarget = false;
        var ic = NewRect("Icon", tile);
        Stretch(ic, 6, 6);
        timeIcon = ic.gameObject.AddComponent<Image>();
        timeIcon.raycastTarget = false;
        timeLabel = NewText(timePanel, "", 16, TextAnchor.UpperLeft, Dim, FontSemiBold);
        Place(timeLabel.rectTransform, new Vector2(0, 1), new Vector2(60, -14), new Vector2(150, 24));
        timeValue = NewText(timePanel, "", 40, TextAnchor.UpperRight, Text, FontBold);
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
        timeSub = NewText(timePanel, "", 15, TextAnchor.UpperLeft, Dim);
        Place(timeSub.rectTransform, new Vector2(0, 1), new Vector2(14, -90), new Vector2(296, 30));
    }

    static readonly string[] DangerNames = { "낮음", "보통", "높음", "매우 높음", "극도" };

    void UpdateTimePanel(GameManager gm, GameState s)
    {
        var city = CityDirector.Instance;
        float now = Time.unscaledTime;
        Color frame = HudFrame;
        if (city != null && city.IsRedNight)
        {
            // 붉은 밤: 지난 시간을 세고 계속 붉게 고동친다
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 5f);
            int t = (int)city.RedNightElapsed;
            SetTimeTexts("moon", "붉은 밤", $"+{t / 60}:{t % 60:00}", $"체력 -{GameBalance.Data.redNightDamage:0} / {GameBalance.Data.redNightInterval:0}초 · 해치로 돌아가자!");
            timeLabel.color = timeValue.color = Color.Lerp(Danger, new Color(1f, 0.6f, 0.5f), pulse * 0.5f);
            timeIcon.color = Danger;
            for (int i = 0; i < timeSegs.Length; i++)
                timeSegs[i].color = Color.Lerp(new Color(0.45f, 0.04f, 0.03f, 0.9f), new Color(0.95f, 0.15f, 0.1f, 0.95f), Mathf.Repeat(i / 20f - now * 0.8f, 1f) < 0.25f ? 1 : pulse * 0.4f);
            frame = new Color(Danger.r, Danger.g, Danger.b, 0.4f + 0.4f * pulse);
        }
        else if (city != null)
        {
            float left = Mathf.Max(0, city.TimeLeft), total = Mathf.Max(1, CityDirector.DefaultTimeLimit);
            int t = Mathf.CeilToInt(left);
            float ratio = left / total;
            bool hurry = left <= 60;
            float pulse = hurry ? 0.5f + 0.5f * Mathf.Sin(now * 6f) : 0;
            int danger = Mathf.Clamp(Mathf.FloorToInt(city.Danger * DangerNames.Length), 0, DangerNames.Length - 1);
            SetTimeTexts("hourglass", "붉은 밤까지", $"{t / 60:00}:{t % 60:00}", $"위험도 {DangerNames[danger]}   ·   턴 {s.Turn} / {GameState.MaxTurns}");
            timeLabel.color = hurry ? Danger : Dim;
            timeValue.color = hurry ? Color.Lerp(Danger, Color.white, pulse * 0.35f) : Text;
            timeIcon.color = hurry ? Danger : Accent;
            timeValue.rectTransform.localScale = Vector3.one * (1 + (hurry ? pulse * 0.05f : 0));
            float filled = ratio * timeSegs.Length;
            for (int i = 0; i < timeSegs.Length; i++)
            {
                // 남은 칸은 호박색 → 빨강 (시간이 줄수록), 마지막 칸은 깜빡인다
                var on = Color.Lerp(new Color(0.95f, 0.3f, 0.15f, 0.95f), new Color(0.98f, 0.76f, 0.32f, 0.95f), ratio);
                float k = Mathf.Clamp01(filled - i);
                if (k > 0 && k < 1) on.a *= 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(now * 8f));
                timeSegs[i].color = k > 0 ? on : new Color(1, 1, 1, 0.08f);
            }
            if (hurry) frame = new Color(Danger.r, Danger.g, Danger.b, 0.3f + 0.4f * pulse);
        }
        else
        {
            // 방공호: 턴과 발전기 전력
            float power = Mathf.Clamp(s.Power, 0, 100);
            SetTimeTexts("home", "방공호 · 턴", $"{s.Turn} / {GameState.MaxTurns}", power <= 0 ? "전력 0% · 정전! 방폭문이 열렸다" : $"발전기 전력 {power:0}%");
            timeLabel.color = Dim;
            timeValue.color = Text;
            timeIcon.color = Accent;
            timeValue.rectTransform.localScale = Vector3.one;
            float filled = power / 100f * timeSegs.Length;
            for (int i = 0; i < timeSegs.Length; i++)
                timeSegs[i].color = Mathf.Clamp01(filled - i) > 0 ? (power < 20 ? new Color(0.95f, 0.3f, 0.15f, 0.95f) : new Color(0.55f, 0.8f, 0.35f, 0.95f)) : new Color(1, 1, 1, 0.08f);
            timeSub.color = power <= 0 ? Danger : Dim;
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
        if (hotbarTray != null) hotbarTray.anchoredPosition = new Vector2(0, 16 - 140 * intro);
        if (hotbarBar != null) hotbarBar.anchoredPosition = new Vector2(0, 26 - 140 * intro);
        if (flashBox != null) ((RectTransform)flashBox.transform).anchoredPosition = new Vector2(-24 + 320 * intro, 40);

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
        // 손에 든 아이템 이름: 바뀌면 아래에서 떠오르며 나타난다
        itemNameFade = Mathf.Min(1f, itemNameFade + dt / 0.25f);
        float f = Ease(itemNameFade);
        itemNameText.color = new Color(Text.r, Text.g, Text.b, f);
        itemNameText.rectTransform.anchoredPosition = new Vector2(0, 112 - 10 * (1 - f) - 140 * intro);
    }
}
