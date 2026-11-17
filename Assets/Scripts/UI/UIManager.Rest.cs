using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 침대 창 (휴식). 제작대 창과 같은 큰 카드 화면 (2026-11-17 사용자: '제작대처럼 바꾸고 빨간 바는 다음날 진행도를 암시하는 애니메이션 추가').
// 왼쪽 날짜 카드: 오늘 → 자고 나면 며칠차인지, 100일 막대, 남은 날, 경고 줄(붉은 칸).
// 오른쪽: 큰 달 칸과 설명, 자고 나면 체력·배고픔·갈증·전력이 지금 얼마에서 얼마가 되는지 보여 주는 카드 4장
// (체력·배고픔·갈증은 HUD 상태 계기와 같은 아이콘, 전력은 번개), 큰 '잠을 잔다' 버튼. 닫기는 제목 줄의 '닫기'(ESC).
// 창은 일반 창(OpenPanel)을 그대로 쓰고 판만 제작대 창 크기로 넓힌다 — 조작 막기·ESC·GameManager.Rest의 ClosePanel이 그대로 듣는다.
// 화면이 낮아(32:9처럼 아주 넓은 화면) 다 들어가지 않으면 내용을 통째로 줄여 버튼까지 보이게 한다.
// 날짜 카드의 붉은 막대는 다음날 진행도를 암시한다: 막대 위로 빛이 왼쪽 끝에서 내일 자리까지 달려가고, 내일 몫의 칸이 막대와 같은 붉은색으로
// 한 번 빛나며 그 위로 '+1일'이 떠오른다 (하루치는 막대에서 4px도 안 된다). RestHintPeriod초마다 되풀이하고, 시계는 창을 열 때 한 번 맞춰
// 창을 다시 그려도 끊기지 않는다. 마지막 날에는 막대 끝이 붉게 부푼다.
// 숫자는 GameManager.AdvanceTurn(하루 소모·굶주림 피해)과 RestRoutine(회복)을 그대로 따라 미리 계산한다 — 그쪽 식을 바꾸면 여기도 바꾼다.
// 배고픔이나 갈증이 0이면 창을 열어 두어도 체력이 계속 준다 (GameManager.Update). 그래서 창을 그린 값이 바뀌면 다시 그리고
// (마우스를 누르고 있는 동안은 뗄 때까지 미룬다), 쓰러지는지 아닌지가 바뀐 것을 보여 주기 전에는 잠들지 않는다.
// 잠든 동안 검은 화면 가운데의 날짜(ShowDayCard)도 여기 있다
public partial class UIManager
{
    // 내용 칸 크기: 제작대 창(BuildWorkbench)과 같은 1440 × 840 판에서 일반 창의 양옆 여백(왼쪽 44 + 오른쪽 52, BuildPanel)을 뺀 1344 × 672.
    // 판 높이는 FitPanel이 내용 높이(+ 위아래 여백 6) + PanelTop + PanelBottom으로 맞춘다
    const float RestContentWidth = 1344f, RestBodyHeight = 672f, RestPanelSides = 96f;
    // 내용 칸 안의 배치: 왼쪽 날짜 카드 너비, 오른쪽 상세 칸이 시작하는 x와 그 너비
    const float RestDayCardWidth = 420f, RestDetailLeft = 476f, RestDetailWidth = RestContentWidth - RestDetailLeft;
    // 날짜 카드의 100일 막대 너비, 다음날 빛이 한 번 도는 시간(초)
    const float RestTrackW = 368f, RestHintPeriod = 2.4f;
    // 창을 연 때 (다음날 빛의 시계), 화면이 낮아 내용을 줄인 배율 (보통 1)
    float restHintStart, restFit = 1f;

    // sleep: '잠을 잔다'를 눌렀을 때 할 일 (Bed가 넘긴다. 창은 GameManager.Rest가 닫는다)
    public void OpenRest(Action sleep)
    {
        restHintStart = Time.unscaledTime;
        // 닫히면 판 너비를 다른 창들의 너비로 되돌린다 (다음 창이 넓은 판에서 재지 않게)
        OpenPanel("휴식", c => BuildRest(c, sleep), afterClose: () => { if (panelBox != null) panelBox.sizeDelta = new Vector2(PanelWidth, panelBox.sizeDelta.y); });
        WidenRestPanel();
    }

    // 일반 창은 그릴 때마다(FitPanel) 판 너비를 PanelWidth로 되돌린다. 같은 호출 안에서 바로 넓혀 좁은 창이 화면에 비치지 않게 한다
    void WidenRestPanel()
    {
        float width = RestPanelSides + RestContentWidth * restFit;
        if (IsPanelOpen && panelBox != null && panelBox.sizeDelta.x != width)
            panelBox.sizeDelta = new Vector2(width, panelBox.sizeDelta.y);
    }

    void RefreshRestPanel()
    {
        RefreshPanel();
        WidenRestPanel();
    }

    // 창이 화면에 다 들어가는 배율: FitPanel과 같은 높이 한도에서 내용 높이가 모자라면 그만큼 줄인다 (보통 화면은 1)
    float RestFitScale()
    {
        float max = Mathf.Max(PanelMinHeight, Mathf.Min(PanelMaxHeight, root.rect.height - 72));
        return Mathf.Clamp((max - PanelTop - PanelBottom - 6f) / RestBodyHeight, 0.5f, 1f);
    }

    void BuildRest(RectTransform c, Action sleep)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        bool lastDay = s.Turn >= GameState.MaxTurns; // 마지막 날에 잠들면 그 자리에서 끝나므로 '자고 난 뒤'가 없다
        var shown = new RestShown(s); // 이 창을 그린 값
        var after = new RestPreview(s); // 자고 난 뒤의 값
        bool dies = after.Dies;

        // 창 내용 칸 전체를 한 덩어리로: 세로 줄 배치에서 높이를 차지하는 자리(body)와, 언제나 1344 × 672로 그리고 화면이 낮으면 통째로 줄이는 안쪽 판(inner).
        // 안쪽 판 안은 제작대 창처럼 자리를 정해 놓는다
        restFit = RestFitScale();
        var body = NewRect("Rest", c);
        var ble = body.gameObject.AddComponent<LayoutElement>();
        ble.minHeight = ble.preferredHeight = RestBodyHeight * restFit;
        var inner = NewRect("Inner", body);
        inner.anchorMin = inner.anchorMax = inner.pivot = new Vector2(0, 1);
        inner.anchoredPosition = Vector2.zero;
        inner.sizeDelta = new Vector2(RestContentWidth, RestBodyHeight);
        inner.localScale = Vector3.one * restFit;

        var hint = RestDayCard(inner, s, after, lastDay);
        hint.Animate(Time.unscaledTime - restHintStart);

        var vline = NewRect("VLine", inner);
        vline.anchorMin = Vector2.zero;
        vline.anchorMax = new Vector2(0, 1);
        vline.offsetMin = new Vector2(RestDayCardWidth + 24, 0);
        vline.offsetMax = new Vector2(RestDayCardWidth + 26, 0);
        var vimg = vline.gameObject.AddComponent<Image>();
        vimg.color = new Color(1, 1, 1, 0.1f);
        vimg.raycastTarget = false;

        var detail = NewRect("Detail", inner);
        detail.anchorMin = Vector2.zero;
        detail.anchorMax = Vector2.one;
        detail.offsetMin = new Vector2(RestDetailLeft, 0);
        detail.offsetMax = Vector2.zero;
        RestDetail(detail, s, after, lastDay, () =>
        {
            // 창을 그린 뒤 값이 바뀌었어도 (누르는 사이의 굶주림 피해 등) 쓰러지는지 아닌지가 그대로면 잠든다.
            // 쓰러지는 쪽으로 (또는 반대로) 바뀌었으면 새 값을 먼저 보여 주고 잠들지 않는다
            if (gm.State != null && !shown.Same(gm.State) && new RestPreview(gm.State).Dies != dies) { RefreshRestPanel(); return; }
            sleep?.Invoke();
        });

        // 감시 (매 프레임): 창을 그린 값이 바뀌면 (굶주림 피해 등) 다시 그린다. 아니면 판 너비를 지키고 날짜 막대의 다음날 빛을 돌린다.
        // 창이 닫히거나 다시 그려지면 이 감시도 함께 사라진다
        body.gameObject.AddComponent<InventoryWindowTicker>().Tick = () =>
        {
            // 이 창이 아직 떠 있는 창인가: 다른 창·끝 화면이 열리면 RefreshPanel이 이 내용을 떼어 낸다 (지워지기 전 같은 프레임에 한 번 더 돌아도 그 창을 넓히지 않게)
            if (body.parent != panelContent) return;
            if (!gm.IsGameOver && !gm.IsTransitioning && gm.State != null && !shown.Same(gm.State))
            {
                // 마우스를 누르고 있는 동안(과 뗀 그 프레임)에는 다시 그리지 않는다. 누른 버튼을 지우면 그 클릭이 사라진다
                // (InputSystemUIInputModule은 누른 버튼과 뗀 자리의 버튼이 같아야 클릭으로 친다). 뗀 뒤에 다시 그린다
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null || !(mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
                {
                    RefreshRestPanel();
                    return;
                }
            }
            WidenRestPanel();
            hint.Animate(Time.unscaledTime - restHintStart);
        };
    }

    // 하룻밤 자고 난 뒤의 값: GameManager.AdvanceTurn(하루 소모 → 굶주림 피해 → 쓰러짐)과 RestRoutine(회복) 순서 그대로
    struct RestPreview
    {
        public readonly float Hunger, Thirst, Power, Health;
        public readonly bool Starving, Dies;

        public RestPreview(GameState s)
        {
            Hunger = Mathf.Max(0, s.Hunger - GameManager.HungerPerTurn);
            Thirst = Mathf.Max(0, s.Thirst - GameManager.ThirstPerTurn);
            Power = s.Power > 0 ? Mathf.Max(0, s.Power - GameManager.PowerPerTurn) : 0;
            Starving = Hunger <= 0 || Thirst <= 0;
            float hurt = Starving ? GameManager.StarveDamagePerTurn : 0;
            Dies = s.Health - hurt <= 0;
            Health = Dies ? 0 : Mathf.Min(s.MaxHealth, s.Health - hurt + GameManager.RestHeal);
        }
    }

    // 휴식 창을 그린 값 (바뀌었는지 견주는 데만 쓴다)
    struct RestShown
    {
        readonly int turn;
        readonly float health, maxHealth, hunger, thirst, power;

        public RestShown(GameState s)
        {
            turn = s.Turn;
            health = s.Health;
            maxHealth = s.MaxHealth;
            hunger = s.Hunger;
            thirst = s.Thirst;
            power = s.Power;
        }

        public bool Same(GameState s) =>
            s.Turn == turn && s.Health == health && s.MaxHealth == maxHealth && s.Hunger == hunger && s.Thirst == thirst && s.Power == power;
    }

    // 경고 줄 (사라지지 않는다): 추격 속도가 오르기 전 · 마지막 며칠, 굶주림, 정전
    static List<string> RestWarnings(GameState s, RestPreview after, bool lastDay)
    {
        var lines = new List<string>();
        string pace = EnemyPace.Warning(s.Turn);
        if (pace != null) lines.Add(pace);
        if (after.Starving)
        {
            string what = after.Hunger <= 0 && after.Thirst <= 0 ? "굶주림과 갈증" : after.Hunger <= 0 ? "굶주림" : "갈증";
            // 마지막 날에도 잠들다 쓰러지는 것은 알린다: AdvanceTurn은 날을 넘겨 끝내기 전에 굶주림 피해로 먼저 죽인다
            if (after.Dies) lines.Add($"! 이대로 잠들면 {what}을 버티지 못하고 쓰러진다. 먼저 먹고 마시자.");
            else if (!lastDay) lines.Add($"! 자는 동안 {what}으로 체력이 {GameManager.StarveDamagePerTurn:0.#} 준다. 먼저 먹고 마시자.");
        }
        if (!lastDay && s.Power > 0 && after.Power <= 0) lines.Add("! 자고 나면 전력이 바닥난다. 정전이 되면 방폭문이 열린다.");
        return lines;
    }

    // ---------- 왼쪽 날짜 카드 ----------

    // 오늘 → 자고 나면 (마지막 날은 '오늘이 마지막 날'), 100일 막대, 남은 날, 아래쪽 붉은 경고 칸. 막대에서 움직이는 부분을 돌려준다
    RestDayHint RestDayCard(RectTransform parent, GameState s, RestPreview after, bool lastDay)
    {
        int max = GameState.MaxTurns, day = s.Turn;
        var card = Box(parent, "DayCard", new Color(1, 1, 1, 0.045f), new Color(1, 1, 1, 0.12f));
        card.anchorMin = Vector2.zero;
        card.anchorMax = new Vector2(0, 1);
        card.pivot = new Vector2(0, 1);
        card.offsetMin = Vector2.zero;
        card.offsetMax = new Vector2(RestDayCardWidth, 0);
        var top = new Vector2(0, 1);
        Place(NewText(card, "오늘", 19, TextAnchor.MiddleLeft, Dim, FontSemiBold).rectTransform, top, new Vector2(26, -26), new Vector2(200, 28));
        Place(NewText(card, $"{day}일차", 60, TextAnchor.MiddleLeft, Text, FontTitle).rectTransform, top, new Vector2(24, -56), new Vector2(370, 72));
        Place(NewText(card, $"DAY {day} / {max}", 16, TextAnchor.MiddleLeft, PaleDim, FontMono).rectTransform, top, new Vector2(28, -130), new Vector2(360, 22));
        Place(NewText(card, lastDay ? "오늘이" : "자고 나면", 19, TextAnchor.MiddleLeft, Dim, FontSemiBold).rectTransform, top, new Vector2(26, -188), new Vector2(200, 28));
        Place(NewText(card, lastDay ? "마지막 날" : $"{day + 1}일차", 60, TextAnchor.MiddleLeft, lastDay ? Danger : Accent, FontTitle).rectTransform, top, new Vector2(24, -218), new Vector2(370, 72));
        Place(NewText(card, lastDay ? "LAST DAY" : $"DAY {day + 1} / {max}", 16, TextAnchor.MiddleLeft, lastDay ? Danger : PaleDim, FontMono).rectTransform, top, new Vector2(28, -292), new Vector2(360, 22));

        var hint = RestTrack(card, day, max, lastDay);

        int left = s.DaysLeft; // 할 일 판·라디오와 같은 셈 (오늘을 넣는다)
        string remain = lastDay ? $"남은 날    {left}일 (오늘)" : $"남은 날    {left}일  →  {Mathf.Max(0, left - 1)}일";
        Place(NewText(card, remain, 21, TextAnchor.MiddleLeft, Text, FontSemiBold).rectTransform, top, new Vector2(26, -388), new Vector2(370, 32));

        RestWarnBox(card, RestWarnings(s, after, lastDay));
        return hint;
    }

    // 100일 막대: 홈 + 10일 눈금(지나간 눈금은 붉게) + 오늘까지 붉은 막대 + 오늘 자리의 금과 빛 (잠든 동안 날짜 화면과 같은 꼴).
    // 그 위에 다음날 몫(내일 칸·빛·금·'+1일')을 그리고, 감시가 RestDayHint.Animate로 돌린다
    RestDayHint RestTrack(RectTransform card, int day, int max, bool lastDay)
    {
        var track = NewRect("Track", card);
        Place(track, new Vector2(0, 1), new Vector2(26, -352), new Vector2(RestTrackW, 3));
        var groove = track.gameObject.AddComponent<Image>();
        groove.color = new Color(1, 1, 1, 0.16f);
        groove.raycastTarget = false;
        float todayX = RestTrackW * Mathf.Clamp01(day / (float)max);
        float nextX = lastDay ? todayX : RestTrackW * Mathf.Clamp01((day + 1) / (float)max);
        for (int i = 1; i < 10; i++)
        {
            float x = RestTrackW * i / 10f;
            TrackPart(track, "Tick", x, new Vector2(1, 9), x <= todayX ? DayTickPassed : DayTickIdle, null);
        }
        var fill = TrackPart(track, "Fill", 0, new Vector2(todayX, 3), new Color(0.85f, 0.14f, 0.1f, 0.9f), null); // 로딩 막대와 같은 붉은색
        fill.rectTransform.pivot = new Vector2(0, 0.5f);
        // 오늘 자리의 빛과 금 (가만히 있다). 다음날 몫은 이 위에 그려 가려지지 않게 한다
        TrackPart(track, "TodayGlow", todayX, new Vector2(44, 44), new Color(MenuRed.r, MenuRed.g, MenuRed.b, 0.45f), UIStyle.Glow);
        TrackPart(track, "TodayPin", todayX, new Vector2(3, 13), new Color(1f, 0.36f, 0.24f), null);

        var hint = new RestDayHint { TargetX = lastDay ? RestTrackW : nextX, Bloom = lastDay ? Danger : MenuRed, PlusX = nextX };
        hint.Preview = TrackPart(track, "Next", todayX, new Vector2(nextX - todayX, 5), Color.clear, null);
        hint.Preview.rectTransform.pivot = new Vector2(0, 0.5f);
        hint.Sweep = TrackPart(track, "Sweep", 0, new Vector2(0, 3), Color.clear, UIStyle.HorizontalFade);
        hint.Sweep.rectTransform.pivot = new Vector2(0, 0.5f);
        hint.Sweep.rectTransform.localScale = new Vector3(-1, 1, 1); // 머리(오른쪽 끝)가 밝고 왼쪽으로 옅어진다
        hint.SweepHead = TrackPart(track, "SweepHead", 0, new Vector2(26, 26), Color.clear, UIStyle.Glow);
        hint.NextGlow = TrackPart(track, "NextGlow", hint.TargetX, new Vector2(40, 40), Color.clear, UIStyle.Glow);
        if (!lastDay)
        {
            hint.NextPin = TrackPart(track, "NextPin", nextX, new Vector2(2, 11), Color.clear, null);
            // 내일 자리 위로 떠오르는 '+1일' (하루치가 막대에서 4px도 안 돼 칸만으로는 잘 안 보인다)
            hint.Plus = NewText(track, "+1일", 16, TextAnchor.LowerCenter, Color.clear, FontSemiBold);
            var pr = hint.Plus.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0, 0.5f);
            pr.pivot = new Vector2(0.5f, 0);
            pr.sizeDelta = new Vector2(60, 20);
            hint.Plus.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
        return hint;
    }

    // 막대 위의 한 조각: 막대 가운데 줄의 x 자리에 가운데를 맞춘다 (막대·칸은 부른 쪽이 왼쪽 기준으로 바꾼다)
    static Image TrackPart(RectTransform track, string name, float x, Vector2 size, Color color, Sprite sprite)
    {
        var rt = NewRect(name, track);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 날짜 막대에서 다음날 진행도를 암시하는 부분 (RestTrack이 만들고, 창의 감시가 매 프레임 Animate를 부른다. t = 창을 연 뒤 지난 초).
    // 한 번 돌 때(RestHintPeriod초): 0~1.1초 빛이 왼쪽 끝에서 TargetX(내일 자리, 마지막 날은 막대 끝)까지 달려가고(처음과 끝이 느림),
    // 1.0~1.6초 내일 몫의 칸이 막대와 같은 붉은색으로 빛나고 금·빛이 한 번 부풀며, 1.0~2.0초 '+1일'이 떠올랐다 사라진다. 나머지는 쉰다
    class RestDayHint
    {
        public Image Preview, Sweep, SweepHead, NextGlow, NextPin;
        public UnityEngine.UI.Text Plus;
        public float TargetX, PlusX;
        public Color Bloom;

        public void Animate(float t)
        {
            float u = Mathf.Repeat(t, RestHintPeriod);
            float k = Mathf.Clamp01(u / 1.1f);
            float head = TargetX * k * k * (3f - 2f * k);
            float a = u < 1.1f ? Mathf.Clamp01(k * 5f) * (1f - Mathf.SmoothStep(0f, 1f, (k - 0.8f) / 0.2f)) : 0f;
            var sweep = Sweep.rectTransform;
            sweep.anchoredPosition = new Vector2(head, 0);
            sweep.sizeDelta = new Vector2(Mathf.Min(head, 70f), 3);
            Sweep.color = new Color(1f, 0.78f, 0.62f, 0.9f * a);
            SweepHead.rectTransform.anchoredPosition = new Vector2(head, 0);
            SweepHead.color = new Color(1f, 0.55f, 0.4f, 0.55f * a);

            float p = (u - 1.0f) / 0.6f;
            float bump = p > 0f && p < 1f ? Mathf.Sin(p * Mathf.PI) : 0f;
            // 내일 몫 칸: 평소엔 옅은 붉은색, 빛날 때는 막대와 같은 붉은색 (자고 나면 막대가 여기까지 찬다)
            Preview.color = Color.Lerp(new Color(1f, 0.42f, 0.32f, 0.35f), new Color(0.95f, 0.2f, 0.14f, 1f), bump);
            NextGlow.color = new Color(Bloom.r, Bloom.g, Bloom.b, 0.12f + 0.5f * bump);
            NextGlow.rectTransform.localScale = Vector3.one * (1f + 0.8f * bump);
            if (NextPin != null) NextPin.color = new Color(1f, 0.6f, 0.5f, 0.35f + 0.65f * bump);
            if (Plus != null)
            {
                float q = u - 1.0f;
                float alpha = q > 0f && q < 1f ? Mathf.Clamp01(q * 5f) * Mathf.Clamp01((1f - q) * 3f) : 0f;
                Plus.color = new Color(1f, 0.62f, 0.5f, alpha);
                Plus.rectTransform.anchoredPosition = new Vector2(PlusX, 8f + 4f * Mathf.Clamp01(q * 3f));
            }
        }
    }

    // 날짜 카드 아래쪽의 붉은 경고 칸 (경고가 없으면 칸도 없다). 길면 글자를 줄여 칸 안에 넣는다
    void RestWarnBox(RectTransform card, List<string> lines)
    {
        if (lines.Count == 0) return;
        const float width = RestDayCardWidth - 40, room = 216, padX = 18, padY = 12;
        var box = Box(card, "Warn", new Color(Danger.r, Danger.g, Danger.b, 0.08f), new Color(Danger.r, Danger.g, Danger.b, 0.45f));
        var t = NewText(box, string.Join("\n", lines), 20, TextAnchor.MiddleLeft, Danger);
        t.lineSpacing = 1.12f;
        Stretch(t.rectTransform, padX, padY);
        float h = 0;
        foreach (int size in new[] { 20, 18, 16 })
        {
            t.fontSize = size;
            var settings = t.GetGenerationSettings(new Vector2(width - padX * 2, 0));
            h = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit;
            if (h + padY * 2 <= room) break;
        }
        Place(box, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(width, Mathf.Min(room, h + padY * 2)));
    }

    // ---------- 오른쪽 상세 (제작대 창의 RecipeDetail과 같은 자리·크기) ----------

    // 큰 달 칸, 이름·꼬리표·날짜·설명·하루 소모, 자고 나면 값 카드 4장(마지막 날은 지금 값), 아래 한 줄 + 큰 '잠을 잔다' 버튼
    void RestDetail(RectTransform d, GameState s, RestPreview after, bool lastDay, Action onSleep)
    {
        int max = GameState.MaxTurns, day = s.Turn;
        bool dies = after.Dies;
        var top = new Vector2(0, 1);
        var tone = lastDay ? Danger : Accent;

        var moon = NewItemSlot(d, "Moon", 38);
        Place(moon.Rect, top, Vector2.zero, new Vector2(196, 196));
        moon.Fill.color = SlotItemFill;
        moon.Icon.enabled = true;
        moon.Icon.sprite = HudIcons.Get("moon");
        moon.Icon.color = lastDay ? Danger : new Color(0.98f, 0.82f, 0.5f);
        var moonGlow = NewRect("Glow", moon.Rect).gameObject.AddComponent<Image>();
        moonGlow.rectTransform.SetSiblingIndex(0); // 바탕 바로 위, 테두리·그림 아래
        Place(moonGlow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190, 190));
        moonGlow.sprite = UIStyle.Glow;
        moonGlow.color = new Color(tone.r, tone.g, tone.b, 0.16f);
        moonGlow.raycastTarget = false;

        var name = NewText(d, lastDay ? "마지막 밤" : "하룻밤 자기", 42, TextAnchor.MiddleLeft, Text, FontBold);
        Place(name.rectTransform, top, new Vector2(226, -2), new Vector2(620, 56));
        var chip = Box(d, "Chip", new Color(tone.r, tone.g, tone.b, 0.16f), new Color(tone.r, tone.g, tone.b, 0.7f));
        Place(chip, top, new Vector2(228, -66), new Vector2(84, 32));
        Stretch(NewText(chip, lastDay ? "마지막" : "하루", 17, TextAnchor.MiddleCenter, tone, FontBold).rectTransform);
        var meta = NewText(d, lastDay ? $"{day}일차 / {max}일" : $"{day}일차 → {day + 1}일차 / {max}일", 18, TextAnchor.MiddleLeft, Dim);
        Place(meta.rectTransform, top, new Vector2(328, -66), new Vector2(520, 32));
        var desc = NewText(d, lastDay ? $"잠들면 그 자리에서 {max}일이 끝난다." : "눈을 붙이면 하루가 지나고, 몸이 조금 회복된다.", 22, TextAnchor.UpperLeft, Text);
        desc.lineSpacing = 1.2f;
        Place(desc.rectTransform, top, new Vector2(228, -116), new Vector2(620, 60));
        if (!lastDay)
        {
            var cost = NewText(d, $"체력 +{GameManager.RestHeal:0.#} 회복   ·   하루 소모  배고픔 {GameManager.HungerPerTurn:0.#} · 갈증 {GameManager.ThirstPerTurn:0.#} · 전력 {GameManager.PowerPerTurn:0.#}%", 19, TextAnchor.MiddleLeft, Dim);
            Place(cost.rectTransform, top, new Vector2(228, -164), new Vector2(620, 30));
        }
        var line = NewRect("Line", d);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, -222);
        line.sizeDelta = new Vector2(0, 2);
        var lineImg = line.gameObject.AddComponent<Image>();
        lineImg.color = new Color(1, 1, 1, 0.1f);
        lineImg.raycastTarget = false;

        var head = NewText(d, lastDay ? "지금" : "자고 나면   <size=17><color=#a8a294>지금 → 자고 나면</color></size>", 22, TextAnchor.MiddleLeft, Accent, FontSemiBold);
        Place(head.rectTransform, top, new Vector2(0, -240), new Vector2(440, 34));
        const float gap = 20f;
        float cellW = (RestDetailWidth - gap * 3) / 4f;
        RestStatCard(d, 0 * (cellW + gap), cellW, "heart", "체력", s.Health, after.Health, "", dies ? "쓰러진다" : null, lastDay);
        RestStatCard(d, 1 * (cellW + gap), cellW, "meat", "배고픔", s.Hunger, after.Hunger, "", null, lastDay);
        RestStatCard(d, 2 * (cellW + gap), cellW, "bottle", "갈증", s.Thirst, after.Thirst, "", null, lastDay);
        RestStatCard(d, 3 * (cellW + gap), cellW, "bolt", "전력", s.Power, after.Power, "%", s.Power <= 0 ? "정전" : null, lastDay);

        // 아래: 자고 나면 어떻게 되는지 한 줄 + 큰 '잠을 잔다' 버튼 (제작대의 상태 줄·'제작' 버튼 자리)
        string status, sub = null;
        Color statusColor;
        if (dies) { status = "! 이대로 잠들면 쓰러진다"; statusColor = Danger; sub = "먼저 먹고 마시자"; }
        else if (lastDay) { status = $"잠들면 {max}일이 끝난다"; statusColor = Accent; }
        else
        {
            status = $"잠들면 {day + 1}일차 아침에 깬다";
            statusColor = Text;
            sub = $"체력 {s.Health:0.#} → {after.Health:0.#}   ·   전력 {s.Power:0.#}% → {after.Power:0.#}%";
        }
        var st = NewText(d, sub == null ? status : $"{status}\n<size=18><color=#a8a294>{sub}</color></size>", 22, TextAnchor.MiddleLeft, statusColor, FontSemiBold);
        st.lineSpacing = 1.15f;
        Place(st.rectTransform, Vector2.zero, new Vector2(0, 2), new Vector2(520, 72));

        var btn = MakeButton(d, "잠을 잔다", onSleep, true, 30);
        Place((RectTransform)btn.transform, new Vector2(1, 0), Vector2.zero, new Vector2(320, 76));
        if (dies || lastDay) DangerButton(btn); // 되돌릴 수 없는 끝 (쓰러지거나 100일이 끝난다)
        else
        {
            // 제작대의 '제작' 버튼과 같은 흙색
            var colors = btn.colors;
            colors.normalColor = new Color(0.5f, 0.37f, 0.14f, 0.95f);
            colors.highlightedColor = new Color(0.64f, 0.48f, 0.2f, 1f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;
        }
    }

    // 자고 나면 바뀌는 값 카드 (제작대의 재료 카드와 같은 꼴): 아이콘, 이름, '지금 → 자고 나면'(nowOnly면 지금 값만), 늘거나 주는 양 (note가 있으면 그 글을 붉게)
    void RestStatCard(RectTransform d, float x, float width, string icon, string label, float now, float after, string unit, string note, bool nowOnly)
    {
        var card = Box(d, "Stat", new Color(1, 1, 1, 0.045f), note != null ? new Color(Danger.r, Danger.g, Danger.b, 0.6f) : new Color(1, 1, 1, 0.12f));
        Place(card, new Vector2(0, 1), new Vector2(x, -288), new Vector2(width, 214));
        var slot = NewItemSlot(card, "Slot", 16);
        Place(slot.Rect, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(96, 96));
        slot.Fill.color = SlotItemFill;
        slot.Icon.enabled = true;
        slot.Icon.sprite = HudIcons.Shaded(icon); // 색을 입힌 그림이라 흰색 그대로 둔다
        var name = NewText(card, label, 20, TextAnchor.MiddleCenter, Text, FontSemiBold);
        Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(width - 14, 28));
        string value = nowOnly ? $"{now:0.#}{unit}" : $"{now:0.#}{unit} → {after:0.#}{unit}";
        var val = NewText(card, value, value.Length > 11 ? 24 : 28, TextAnchor.MiddleCenter, !nowOnly && after <= 0 ? Danger : Text, FontBold);
        val.horizontalOverflow = HorizontalWrapMode.Overflow; // 줄바꿈 없이 한 줄로
        Place(val.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(width - 8, 36));
        float diff = after - now;
        string text;
        Color color;
        if (note != null) { text = note; color = Danger; }
        else if (nowOnly) { text = ""; color = Dim; }
        else if (Mathf.Abs(diff) < 0.05f) { text = "그대로"; color = Dim; }
        else if (diff > 0) { text = $"+{diff:0.#}{unit}"; color = AddedColor; }
        else { text = $"-{-diff:0.#}{unit}"; color = after <= 0 ? Danger : Dim; }
        var delta = NewText(card, text, 18, TextAnchor.MiddleCenter, color, FontSemiBold);
        Place(delta.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(width - 14, 24));
    }

    // ---------- 잠든 동안 화면 가운데 날짜 (GameManager.RestRoutine: 눈이 감긴 뒤 ShowDayCard, DayCardSeconds 뒤 HideDayCard) ----------
    // 로딩 화면(BuildLoading)과 같은 꼴: 검은 바탕 + 옅은 긁힘 + 가장자리 그늘, 핏빛 세로 막대 + 제목(도현) + 영문 꼬리표(모노) + 가는 선,
    // 그 아래 100일 가운데 오늘까지 붉게 찬 막대 (N/100, 10일마다 눈금, 오늘 자리에 밝은 금). 떠다니거나 흔들리지 않는다.
    // 나타나면 숫자가 1일차부터 오늘까지 세어 올라가고 (꼬리표의 DAY·DAYS LEFT도 함께), 붉은 막대와 금이 같은 값으로 왼쪽에서 오늘 자리까지 달려간다.
    // 처음엔 빠르고 오늘에 가까울수록 느려지며, 오늘에 닿으면 금의 빛이 한 번 부풀고 제목이 잠깐 하얗게 빛난다.
    // 남은 날은 할 일 판·라디오와 같은 셈 (GameState.DaysLeft, 오늘을 넣는다)
    const float DayTrackW = 560f;
    const float DayCountDelay = 0.2f, DayCountTime = 1.0f, DayLandTime = 0.45f;
    // RestRoutine이 날짜 화면을 띄워 두는 시간: 세기(0.2 + 1.0초) + 닿은 뒤 빛(0.45초) + 읽을 틈
    public const float DayCardSeconds = 2.0f;
    static readonly Color DayTickIdle = new Color(1, 1, 1, 0.16f), DayTickPassed = new Color(1f, 0.5f, 0.4f, 0.5f);
    RectTransform dayCard, dayFill, dayToday, dayGlowRect, dayTrailRect;
    CanvasGroup dayCardGroup;
    UnityEngine.UI.Text dayTitle, dayTag, dayLeft;
    Image dayGlow, dayTrail;
    Image[] dayTicks;
    float dayCardTarget, dayCardClock, dayCountValue;
    int dayCardDay, dayCardMax, dayShown;

    public void ShowDayCard(GameState s)
    {
        if (s == null) return;
        if (dayCard == null) BuildDayCard();
        dayCardDay = Mathf.Max(1, s.Turn);
        dayCardMax = Mathf.Max(1, GameState.MaxTurns);
        dayCardClock = 0;
        dayCountValue = 0;
        dayShown = 0;
        SetDayCount(0, false);
        dayTitle.color = Text;
        dayTrail.color = Color.clear;
        dayGlowRect.localScale = Vector3.one;
        dayGlow.color = new Color(MenuRed.r, MenuRed.g, MenuRed.b, 0.45f);
        dayCard.SetAsLastSibling(); // 검은 막보다 위에
        dayCard.gameObject.SetActive(true);
        dayCardTarget = 1;
    }

    public void HideDayCard() => dayCardTarget = 0;

    // 막대·금을 v일(0 ~ 오늘)만큼으로 놓고, 보이는 숫자(1일차부터)가 바뀌었을 때만 글을 다시 쓴다
    void SetDayCount(float v, bool landed)
    {
        float x = DayTrackW * Mathf.Clamp01(v / dayCardMax);
        dayFill.sizeDelta = new Vector2(x, 0);
        dayToday.anchoredPosition = new Vector2(x, 0);
        int shown = landed ? dayCardDay : Mathf.Clamp(Mathf.FloorToInt(v), 1, dayCardDay);
        if (shown != dayShown)
        {
            dayShown = shown;
            int left = Mathf.Max(0, dayCardMax - shown + 1); // GameState.DaysLeft와 같은 셈
            dayTitle.text = $"{shown}일차";
            dayTag.text = $"DAY {shown} / {dayCardMax}";
            dayLeft.text = left <= 1 ? "LAST DAY" : $"{left} DAYS LEFT";
            dayLeft.color = left <= 1 ? Danger : PaleDim;
        }
        for (int i = 0; i < dayTicks.Length; i++)
            dayTicks[i].color = v >= dayCardMax * (i + 1) / 10f ? DayTickPassed : DayTickIdle;
    }

    void BuildDayCard()
    {
        dayCard = NewRect("DayCard", root);
        Stretch(dayCard);
        dayCardGroup = dayCard.gameObject.AddComponent<CanvasGroup>();
        dayCardGroup.alpha = 0;
        dayCardGroup.interactable = dayCardGroup.blocksRaycasts = false;
        var bg = dayCard.gameObject.AddComponent<Image>();
        bg.color = new Color(0.012f, 0.012f, 0.014f, 1f);
        bg.raycastTarget = false;
        var grime = NewRect("Grime", dayCard).gameObject.AddComponent<Image>();
        Stretch(grime.rectTransform);
        grime.sprite = UIStyle.Grime;
        grime.type = Image.Type.Tiled;
        grime.pixelsPerUnitMultiplier = 0.4f; // 로딩 화면과 같은 크기의 무늬
        grime.color = new Color(1, 1, 1, 0.22f);
        grime.raycastTarget = false;
        var shade = NewRect("Shade", dayCard).gameObject.AddComponent<Image>();
        Stretch(shade.rectTransform);
        shade.sprite = UIStyle.Vignette;
        shade.color = new Color(0, 0, 0, 0.7f);
        shade.raycastTarget = false;

        // 제목 묶음: 로딩 화면과 같은 자리·크기
        var group = NewRect("Group", dayCard);
        Place(group, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(620, 150));
        var mark = NewRect("TitleMark", group).gameObject.AddComponent<Image>();
        Place(mark.rectTransform, new Vector2(0, 1), new Vector2(0, -2), new Vector2(6, 60));
        mark.color = MenuRed;
        mark.raycastTarget = false;
        dayTitle = NewText(group, "", 46, TextAnchor.MiddleLeft, Text, FontTitle);
        Place(dayTitle.rectTransform, new Vector2(0, 1), new Vector2(24, 0), new Vector2(590, 64));
        dayTitle.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        dayTag = NewText(group, "", 15, TextAnchor.UpperLeft, PaleDim, FontMono);
        Place(dayTag.rectTransform, new Vector2(0, 1), new Vector2(28, -66), new Vector2(280, 22));
        dayLeft = NewText(group, "", 15, TextAnchor.UpperRight, PaleDim, FontMono); // 오른쪽 끝을 막대 끝에 맞춘다
        Place(dayLeft.rectTransform, new Vector2(0, 1), new Vector2(26 + DayTrackW - 280, -66), new Vector2(280, 22));
        var rule = NewRect("Rule", group).gameObject.AddComponent<Image>();
        Place(rule.rectTransform, new Vector2(0, 1), new Vector2(26, -96), new Vector2(DayTrackW, 1));
        rule.color = new Color(1, 1, 1, 0.14f);
        rule.raycastTarget = false;

        // 날짜 막대: 홈 + 10일마다 눈금(지나간 눈금은 붉게) + 오늘까지 붉은 막대 + 오늘 자리의 밝은 금과 빛, 달려갈 때의 꼬리
        var track = NewRect("Track", group);
        Place(track, new Vector2(0, 1), new Vector2(26, -108), new Vector2(DayTrackW, 3));
        var trackImg = track.gameObject.AddComponent<Image>();
        trackImg.color = new Color(1, 1, 1, 0.06f);
        trackImg.raycastTarget = false;
        dayTicks = new Image[9];
        for (int i = 0; i < dayTicks.Length; i++)
        {
            var tick = NewRect("Tick", track).gameObject.AddComponent<Image>();
            Place(tick.rectTransform, new Vector2(0, 0.5f), new Vector2(DayTrackW * (i + 1) / 10f, 0), new Vector2(1, 9));
            tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tick.color = DayTickIdle;
            tick.raycastTarget = false;
            dayTicks[i] = tick;
        }
        dayFill = NewRect("Fill", track);
        dayFill.anchorMin = Vector2.zero;
        dayFill.anchorMax = new Vector2(0, 1);
        dayFill.pivot = new Vector2(0, 0.5f);
        dayFill.anchoredPosition = Vector2.zero;
        var fill = dayFill.gameObject.AddComponent<Image>();
        fill.color = new Color(0.85f, 0.14f, 0.1f, 0.9f); // 로딩 막대와 같은 붉은색
        fill.raycastTarget = false;
        dayToday = NewRect("Today", track);
        dayToday.anchorMin = dayToday.anchorMax = new Vector2(0, 0.5f);
        dayToday.pivot = new Vector2(0.5f, 0.5f);
        dayToday.sizeDelta = Vector2.zero;
        // 꼬리: 금에서 왼쪽으로 뻗고 금 쪽이 밝다 (가로로 뒤집은 HorizontalFade). 빨리 달릴수록 진하다
        dayTrail = NewRect("Trail", dayToday).gameObject.AddComponent<Image>();
        dayTrailRect = dayTrail.rectTransform;
        dayTrailRect.anchorMin = dayTrailRect.anchorMax = new Vector2(0.5f, 0.5f);
        dayTrailRect.pivot = new Vector2(0, 0.5f);
        dayTrailRect.anchoredPosition = Vector2.zero;
        dayTrailRect.sizeDelta = new Vector2(0, 3);
        dayTrailRect.localScale = new Vector3(-1, 1, 1);
        dayTrail.sprite = UIStyle.HorizontalFade;
        dayTrail.color = Color.clear;
        dayTrail.raycastTarget = false;
        dayGlow = NewRect("Glow", dayToday).gameObject.AddComponent<Image>();
        dayGlowRect = dayGlow.rectTransform;
        Place(dayGlowRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
        dayGlow.sprite = UIStyle.Glow;
        dayGlow.color = new Color(MenuRed.r, MenuRed.g, MenuRed.b, 0.45f);
        dayGlow.raycastTarget = false;
        var pin = NewRect("Pin", dayToday).gameObject.AddComponent<Image>();
        Place(pin.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 13));
        pin.color = new Color(1f, 0.36f, 0.24f);
        pin.raycastTarget = false;

        // 빨리 나타나고, 눈을 뜰 때는 검은 막이 걷히기 전에 먼저 사라진다 (바탕의 긁힘 무늬가 방 안 모습과 겹쳐 보이지 않게). 다 사라지면 끈다.
        // 떠 있는 동안 숫자와 막대를 1일차부터 오늘까지 올린다
        dayCard.gameObject.AddComponent<InventoryWindowTicker>().Tick = () =>
        {
            float dt = Time.unscaledDeltaTime;
            dayCardGroup.alpha = Mathf.MoveTowards(dayCardGroup.alpha, dayCardTarget, dt * (dayCardTarget > 0 ? 2.5f : 4f));
            if (dayCardTarget <= 0)
            {
                if (dayCardGroup.alpha <= 0) dayCard.gameObject.SetActive(false);
                return;
            }
            dayCardClock += dt;
            float k = Mathf.Clamp01((dayCardClock - DayCountDelay) / DayCountTime);
            float v = dayCardDay * (1f - Mathf.Pow(1f - k, 3f)); // 처음엔 빠르고 오늘에 가까울수록 느려진다
            bool landed = k >= 1f;
            float speed = dt > 0 ? DayTrackW * (v - dayCountValue) / dayCardMax / dt : 0; // 금이 움직이는 빠르기 (px/초)
            dayCountValue = v;
            SetDayCount(v, landed);
            dayTrailRect.sizeDelta = new Vector2(Mathf.Min(dayFill.sizeDelta.x, 90f), 3);
            dayTrail.color = new Color(1f, 0.4f, 0.28f, Mathf.Clamp01(speed / 500f) * 0.8f);
            // 오늘에 닿으면: 금의 빛이 한 번 부풀고, 제목이 하얗게 빛났다가 원래 색으로
            float p = Mathf.Clamp01((dayCardClock - DayCountDelay - DayCountTime) / DayLandTime);
            float bump = landed ? Mathf.Sin(p * Mathf.PI) : 0;
            dayGlowRect.localScale = Vector3.one * (1f + 0.7f * bump);
            dayGlow.color = new Color(MenuRed.r, MenuRed.g, MenuRed.b, 0.45f + 0.45f * bump);
            dayTitle.color = landed ? Color.Lerp(Color.white, Text, p) : Text;
        };
        dayCard.gameObject.SetActive(false);
    }
}
