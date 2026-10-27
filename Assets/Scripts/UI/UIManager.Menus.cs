using System;
using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴, 일시정지 메뉴, 설정·조작법·제작진 창
// 배치: 왼쪽 위 제목 + 왼쪽 세로 메뉴(리썰 컴퍼니·오버워치), 오른쪽 공지 패널(CS2·오버워치), 오른쪽 아래 버전
public partial class UIManager
{
    const string GameTitle = "핵폭발 생존";
    const string GameSubtitle = "NUCLEAR  SURVIVAL   ·   가제";
    static readonly string[] UpdateNotes =
    {
        "메인 메뉴 · 일시정지(Esc) · 설정 화면 추가",
        "방공호·도시 밝기 상향, 설정에 밝기 조절 추가",
        "1인칭 손 모델과 손에 든 아이템 표시",
        "핫바(1~8·휠) 선택, 우클릭 사용, G 버리기",
        "반투명 + 테두리 UI로 가독성 개선",
        "그래픽 품질·해상도 배율·FPS 제한 등 최적화 옵션",
        "밸런스 조정: 좀비 수·속도·피해, 배고픔·갈증 소모량",
        "게임 창을 내리면 자동 일시정지",
    };

    GameObject mainMenuRoot, pauseRoot;
    int settingsTab;

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

        var shade = NewRect("Shade", m).gameObject.AddComponent<Image>();
        shade.sprite = UIStyle.HorizontalFade;
        shade.color = new Color(0, 0, 0, 0.85f);
        shade.raycastTarget = false;
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(0.62f, 1);
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

        var title = NewText(m, GameTitle, 88, TextAnchor.LowerLeft, Text, FontBold);
        title.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(104, -96), new Vector2(1000, 120));
        var sub = NewText(m, GameSubtitle, 22, TextAnchor.UpperLeft, Accent, FontSemiBold);
        Place(sub.rectTransform, new Vector2(0, 1), new Vector2(110, -222), new Vector2(900, 34));
        var line = NewRect("Line", m);
        Place(line, new Vector2(0, 1), new Vector2(110, -266), new Vector2(140, 3));
        line.gameObject.AddComponent<Image>().color = Accent;

        float y = -330, step = 66;
        MenuButton(m, "새 게임", new Vector2(92, y), () => GameManager.Instance.NewGame());
        MenuButton(m, "이어하기", new Vector2(92, y -= step), null, false, "저장 기능 준비 중");
        MenuButton(m, "설정", new Vector2(92, y -= step), () => OpenSettings());
        MenuButton(m, "조작법", new Vector2(92, y -= step), OpenControls);
        MenuButton(m, "제작진", new Vector2(92, y -= step), OpenCredits);
        MenuButton(m, "게임 종료", new Vector2(92, y -= step), () => GameManager.Instance.QuitGame());

        // 오른쪽: 업데이트 노트
        var notes = Box(m, "Notes", new Color(0.04f, 0.045f, 0.04f, 0.72f), FrameColor);
        Place(notes, new Vector2(1, 0.5f), new Vector2(-90, 20), new Vector2(560, 470));
        var head = NewText(notes, "업데이트 노트", 28, TextAnchor.UpperLeft, Accent, FontBold);
        Place(head.rectTransform, new Vector2(0, 1), new Vector2(28, -24), new Vector2(300, 40));
        var ver = NewText(notes, "Ver " + Application.version, 18, TextAnchor.UpperRight, Dim);
        Place(ver.rectTransform, new Vector2(1, 1), new Vector2(-28, -30), new Vector2(200, 30));
        var div = NewRect("Line", notes);
        Place(div, new Vector2(0.5f, 1), new Vector2(0, -74), new Vector2(504, 2));
        div.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);
        var body = NewText(notes, "· " + string.Join("\n· ", UpdateNotes), 19, TextAnchor.UpperLeft, Text);
        body.lineSpacing = 1.35f;
        Place(body.rectTransform, new Vector2(0, 1), new Vector2(28, -92), new Vector2(504, 330));
        var foot = NewText(notes, "버그와 의견은 팀 채널에 남겨 주세요.", 16, TextAnchor.LowerLeft, Dim);
        Place(foot.rectTransform, new Vector2(0, 0), new Vector2(28, 20), new Vector2(504, 26));

        var credit = NewText(m, "Team_31   ·   Made with Unity 6", 16, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.4f));
        Place(credit.rectTransform, new Vector2(0, 0), new Vector2(110, 24), new Vector2(600, 26));
        return m.gameObject;
    }

    GameObject BuildPauseMenu()
    {
        var p = NewRect("PauseMenu", root);
        Stretch(p);
        p.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        var shade = NewRect("Shade", p).gameObject.AddComponent<Image>();
        shade.sprite = UIStyle.HorizontalFade;
        shade.color = new Color(0, 0, 0, 0.8f);
        shade.raycastTarget = false;
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(0.55f, 1);
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

        var title = NewText(p, "일시정지", 64, TextAnchor.LowerLeft, Text, FontBold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(104, -150), new Vector2(800, 90));
        var line = NewRect("Line", p);
        Place(line, new Vector2(0, 1), new Vector2(110, -256), new Vector2(110, 3));
        line.gameObject.AddComponent<Image>().color = Accent;

        float y = -310, step = 66;
        MenuButton(p, "계속하기", new Vector2(92, y), () => SetPaused(false));
        MenuButton(p, "설정", new Vector2(92, y -= step), () => OpenSettings());
        MenuButton(p, "조작법", new Vector2(92, y -= step), OpenControls);
        MenuButton(p, "메인 메뉴로", new Vector2(92, y -= step), () => GameManager.Instance.GoToMainMenu());
        MenuButton(p, "게임 종료", new Vector2(92, y -= step), () => GameManager.Instance.QuitGame());
        return p.gameObject;
    }

    Button MenuButton(Transform parent, string label, Vector2 pos, Action onClick, bool interactable = true, string tag = null)
    {
        var rt = NewRect("MenuButton", parent);
        Place(rt, new Vector2(0, 1), pos, new Vector2(460, 58));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Fill;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.normalColor = new Color(1, 1, 1, 0);
        colors.highlightedColor = new Color(Accent.r, Accent.g, Accent.b, 0.18f);
        colors.pressedColor = new Color(Accent.r, Accent.g, Accent.b, 0.32f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(1, 1, 1, 0);
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.interactable = interactable;
        if (onClick != null) b.onClick.AddListener(() => onClick());

        var bar = NewRect("Bar", rt);
        bar.anchorMin = new Vector2(0, 0.2f);
        bar.anchorMax = new Vector2(0, 0.8f);
        bar.pivot = new Vector2(0, 0.5f);
        bar.anchoredPosition = new Vector2(2, 0);
        bar.sizeDelta = new Vector2(4, 0);
        var barImg = bar.gameObject.AddComponent<Image>();
        barImg.color = interactable ? new Color(Accent.r, Accent.g, Accent.b, 0.7f) : new Color(1, 1, 1, 0.15f);
        barImg.raycastTarget = false;

        var t = NewText(rt, label, 30, TextAnchor.MiddleLeft, interactable ? Text : new Color(1, 1, 1, 0.35f), FontSemiBold);
        Stretch(t.rectTransform, 24, 0);
        t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
        if (tag != null)
        {
            var tg = NewText(rt, tag, 15, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.35f));
            Stretch(tg.rectTransform, 16, 0);
        }
        return b;
    }

    public void ShowMainMenu()
    {
        mainMenuRoot.SetActive(true);
        ClosePanel();
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
    }

    // ---------- 설정 ----------

    public void OpenSettings()
    {
        OpenPanel("설정", c =>
        {
            var tabs = Row(c, false);
            string[] names = { "그래픽", "소리", "조작" };
            for (int i = 0; i < names.Length; i++)
            {
                int k = i;
                var b = RowButton(tabs, names[i], () => { settingsTab = k; RefreshPanel(); }, true, 170);
                if (k == settingsTab)
                {
                    var colors = b.colors;
                    colors.normalColor = new Color(0.5f, 0.37f, 0.14f, 0.95f);
                    b.colors = colors;
                }
            }
            Line(c, "", 8);

            if (settingsTab == 0)
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
            else if (settingsTab == 1)
            {
                Selector(c, "전체 음량", $"{GameSettings.Volume * 100:0}%", d => GameSettings.Volume = Step(GameSettings.Volume, d * 0.1f, 0f, 1f));
            }
            else
            {
                Selector(c, "마우스 감도", $"{GameSettings.Sensitivity:0.0}", d => GameSettings.Sensitivity = Step(GameSettings.Sensitivity, d * 0.1f, 0.2f, 3f));
                Selector(c, "마우스 Y축 반전", GameSettings.InvertY ? "켬" : "끔", d => GameSettings.InvertY = !GameSettings.InvertY);
            }

            Line(c, "", 8);
            var row = Row(c, false);
            RowButton(row, "기본값으로", () => { GameSettings.ResetToDefault(); ApplyAllSettings(); RefreshPanel(); }, true, 200);
        }, "settings");
    }

    static float Step(float value, float delta, float min, float max) => Mathf.Clamp(Mathf.Round((value + delta) * 10f) / 10f, min, max);

    void Selector(RectTransform c, string label, string value, Action<int> change)
    {
        var row = Row(c);
        RowLabel(row, label);
        RowButton(row, "<", () => { change(-1); ApplyAllSettings(); RefreshPanel(); }, true, 54);
        var v = NewText(row, value, 22, TextAnchor.MiddleCenter, Accent);
        var le = v.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 170;
        RowButton(row, ">", () => { change(1); ApplyAllSettings(); RefreshPanel(); }, true, 54);
    }

    static void ApplyAllSettings()
    {
        GameSettings.Save();
        GameSettings.ApplyGlobal();
        GameSettings.ApplyScene();
    }

    public void OpenControls()
    {
        OpenPanel("조작법", c =>
        {
            string[,] keys =
            {
                { "W A S D", "이동" }, { "마우스", "시점 회전" }, { "Shift", "달리기 (소리가 커진다)" }, { "C", "앉기 (조용히 숨기)" },
                { "F", "줍기 · 문 열기 · 시설 사용" }, { "Q", "손전등 켜기/끄기" }, { "1 ~ 8 / 휠", "핫바 아이템 선택" },
                { "우클릭", "선택한 아이템 사용 (먹기·치료)" }, { "G", "선택한 아이템 버리기" }, { "좌클릭", "공격 (둔기를 들면 더 세다)" },
                { "Tab", "가방 열기" }, { "Esc", "일시정지 · 창 닫기" },
            };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                var row = Row(c);
                var k = NewText(row, keys[i, 0], 22, TextAnchor.MiddleLeft, Accent);
                var le = k.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 200;
                RowLabel(row, keys[i, 1]);
            }
        }, "controls");
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
