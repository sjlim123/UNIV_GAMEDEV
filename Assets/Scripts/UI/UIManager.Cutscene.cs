using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 새 게임 도입 컷씬의 화면 위 층. 실시간 3D 장면은 Cutscene 씬의 CutsceneDirector가 돌리고,
// 여기서는 위아래 검은 띠(레터박스), 한 글자씩 적히는 자막과 시각, 섬광·검은 막, 쪽 표시, '다음'·'건너뛰기' 버튼을 그린다.
// 장면은 저절로 넘어가고, Space·Enter·'다음'으로 바로 넘길 수 있다 (자막이 나오는 중이면 먼저 자막을 끝까지 보여 준다).
// Esc·'건너뛰기'로 바로 끝낸다. 끝나면 GameManager가 방공호 씬을 연다
public partial class UIManager
{
    const float CutCharsPerSecond = 17f;   // 자막 타자 속도
    const float CutCharFade = 0.12f;       // 글자 하나가 떠오르는 시간
    const float CutInputDelay = 0.8f;      // 시작 직후(화면이 밝아지는 동안)는 입력을 받지 않는다
    const float CutBarRatio = 0.115f;      // 위아래 검은 띠 높이 (화면 높이 비율)

    readonly struct CutShot
    {
        public readonly string Stamp, Caption;
        public readonly float Delay; // 장면이 시작되고 자막이 나오기까지
        public CutShot(string stamp, string caption, float delay) { Stamp = stamp; Caption = caption; Delay = delay; }
    }

    static readonly CutShot[] CutShots =
    {
        new CutShot("오후 2시 47분", "그날도 평범한 오후였다. 창밖 하늘은 유난히 맑았다.", 1.4f),
        new CutShot("오후 2시 52분", "「긴급 속보입니다. 수도권을 향한 핵 공격이 감지됐습니다. 즉시 가까운 대피소로…」", 1.5f),
        new CutShot("오후 2시 53분", "창밖이 하얗게 타올랐다. 그리고, 땅이 울렸다.", 1.3f),
        new CutShot("오후 2시 55분", "생각할 겨를도 없었다. 방공호로 달렸다.", 1.4f),
    };

    static Texture2D cutDotTex;
    static Sprite cutDot;

    // 가벼운 레터박스 (방공호 출입 연출): 위아래 검은 띠가 닫혀 들어왔다 열린다. 컷씬 화면과 따로 논다
    RectTransform boxTop, boxBottom;
    float boxTarget, boxAmount;

    public void SetLetterbox(bool on)
    {
        if (boxTop == null)
        {
            boxTop = NewRect("LetterboxTop", root);
            var it = boxTop.gameObject.AddComponent<Image>();
            it.color = Color.black;
            it.raycastTarget = false;
            boxTop.anchorMin = new Vector2(0, 1);
            boxTop.anchorMax = Vector2.one;
            boxTop.pivot = new Vector2(0.5f, 1);
            boxTop.anchoredPosition = Vector2.zero;
            boxBottom = NewRect("LetterboxBottom", root);
            var ib = boxBottom.gameObject.AddComponent<Image>();
            ib.color = Color.black;
            ib.raycastTarget = false;
            boxBottom.anchorMin = Vector2.zero;
            boxBottom.anchorMax = new Vector2(1, 0);
            boxBottom.pivot = new Vector2(0.5f, 0);
            boxBottom.anchoredPosition = Vector2.zero;
            boxTop.sizeDelta = boxBottom.sizeDelta = new Vector2(0, 0);
        }
        boxTarget = on ? 1f : 0f;
        boxTop.SetAsLastSibling();
        boxBottom.SetAsLastSibling();
        if (fadeOverlay != null) fadeOverlay.transform.SetAsLastSibling();
    }

    void UpdateLetterbox()
    {
        if (boxTop == null) return;
        boxAmount = Mathf.MoveTowards(boxAmount, boxTarget, Time.unscaledDeltaTime / 0.7f);
        float h = ((RectTransform)root.transform).rect.height * CutBarRatio * Mathf.SmoothStep(0, 1, boxAmount);
        boxTop.sizeDelta = new Vector2(0, h);
        boxBottom.sizeDelta = new Vector2(0, h);
        bool show = h > 0.5f;
        if (boxTop.gameObject.activeSelf != show) { boxTop.gameObject.SetActive(show); boxBottom.gameObject.SetActive(show); }
    }

    Action cutsceneFinished;
    CutsceneDirector cutDirector;
    RectTransform cutRoot, cutBarTop, cutBarBottom;
    CanvasGroup cutChrome, cutCaptionGroup;
    Image cutFlash, cutDark, cutNextFrame;
    UnityEngine.UI.Text cutCaption, cutStamp;
    readonly Image[] cutDots = new Image[CutShots.Length];
    float[] cutCharTimes;
    string cutPrevCaption, cutPrevStamp;
    int cutShot = -1;
    float cutTime, cutShotTime;
    bool cutEnded = true, cutTypeForced;

    // 컷씬 화면이 떠 있는 동안 true. 끝나도 다음 씬이 열릴 때(OnSceneLoaded → HideCutscene)까지 남아 있다
    public bool IsCutscenePlaying { get; private set; }

    // Cutscene 씬이 열린 뒤 부른다. 장면이 모두 끝나거나 건너뛰면 onFinished를 한 번 부른다
    public void PlayCutscene(Action onFinished)
    {
        cutDirector = CutsceneDirector.Instance;
        if (cutDirector == null)
        {
            Debug.LogWarning("[UIManager] Cutscene 씬에 CutsceneDirector가 없어 컷씬을 건너뜁니다.");
            onFinished?.Invoke();
            return;
        }
        if (cutRoot == null) BuildCutscene();
        cutsceneFinished = onFinished;
        IsCutscenePlaying = true;
        cutEnded = false;
        cutTime = 0;
        cutShot = -1;
        cutPrevCaption = cutPrevStamp = null;
        cutCaption.text = cutStamp.text = "";
        cutCaptionGroup.alpha = 0;
        cutFlash.color = new Color(cutFlash.color.r, cutFlash.color.g, cutFlash.color.b, 0);
        cutDark.color = new Color(0, 0, 0, 0);
        cutChrome.alpha = 1;
        cutChrome.interactable = cutChrome.blocksRaycasts = true;
        cutRoot.gameObject.SetActive(true);
        cutRoot.SetAsLastSibling();
        fadeOverlay.transform.SetAsLastSibling(); // 검은 페이드 막은 늘 맨 위
        ClearCutSelection();

        cutDirector.OnFinished += FinishCutscene;
        cutDirector.Begin();
        SyncCutShot();
    }

    void FinishCutscene()
    {
        if (cutEnded) return;
        cutEnded = true;
        if (cutChrome != null) cutChrome.interactable = cutChrome.blocksRaycasts = false;
        if (cutDirector != null)
        {
            cutDirector.OnFinished -= FinishCutscene;
            cutDirector.Skip(); // 저절로 끝난 경우엔 아무것도 하지 않는다
        }
        var done = cutsceneFinished;
        cutsceneFinished = null;
        done?.Invoke();
    }

    public void HideCutscene()
    {
        IsCutscenePlaying = false;
        cutsceneFinished = null;
        cutEnded = true;
        if (cutDirector != null) cutDirector.OnFinished -= FinishCutscene;
        cutDirector = null;
        if (cutRoot != null) cutRoot.gameObject.SetActive(false);
    }

    // ---------- 화면 만들기 ----------

    void BuildCutscene()
    {
        cutRoot = NewRect("Cutscene", root);
        Stretch(cutRoot);

        // 섬광(하얀 막)과 검은 막: 3D 장면 위, 자막 아래
        cutFlash = NewRect("Flash", cutRoot).gameObject.AddComponent<Image>();
        Stretch(cutFlash.rectTransform);
        cutFlash.color = new Color(1f, 0.93f, 0.8f, 0);
        cutFlash.raycastTarget = false;
        cutDark = NewRect("Dark", cutRoot).gameObject.AddComponent<Image>();
        Stretch(cutDark.rectTransform);
        cutDark.color = new Color(0, 0, 0, 0);
        cutDark.raycastTarget = false;

        // 레터박스
        cutBarTop = NewRect("BarTop", cutRoot);
        var bt = cutBarTop.gameObject.AddComponent<Image>();
        bt.color = Color.black;
        bt.raycastTarget = false;
        cutBarTop.anchorMin = new Vector2(0, 1);
        cutBarTop.anchorMax = Vector2.one;
        cutBarTop.pivot = new Vector2(0.5f, 1);
        cutBarTop.anchoredPosition = Vector2.zero;
        cutBarBottom = NewRect("BarBottom", cutRoot);
        var bb = cutBarBottom.gameObject.AddComponent<Image>();
        bb.color = Color.black;
        bb.raycastTarget = false;
        cutBarBottom.anchorMin = Vector2.zero;
        cutBarBottom.anchorMax = new Vector2(1, 0);
        cutBarBottom.pivot = new Vector2(0.5f, 0);
        cutBarBottom.anchoredPosition = Vector2.zero;

        // 자막: 아래 띠 안에 작은 시각과 한 줄 대사
        var caption = NewRect("Caption", cutRoot);
        Place(caption, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(1600, 110));
        cutCaptionGroup = caption.gameObject.AddComponent<CanvasGroup>();
        cutCaptionGroup.interactable = cutCaptionGroup.blocksRaycasts = false;
        cutStamp = NewText(caption, "", 19, TextAnchor.UpperCenter, new Color(Accent.r, Accent.g, Accent.b, 0.85f), FontTitle);
        Place(cutStamp.rectTransform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(1600, 28));
        cutStamp.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        cutCaption = NewText(caption, "", 30, TextAnchor.UpperCenter, Text);
        Place(cutCaption.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(1600, 76));
        cutCaption.lineSpacing = 1.2f;
        var cs = cutCaption.gameObject.AddComponent<Shadow>();
        cs.effectColor = new Color(0, 0, 0, 0.85f);
        cs.effectDistance = new Vector2(2, -2);

        // 버튼·쪽 표시·건너뛰기 (끝날 때 함께 사라진다)
        var chrome = NewRect("Chrome", cutRoot);
        Stretch(chrome);
        cutChrome = chrome.gameObject.AddComponent<CanvasGroup>();

        var next = MakeButton(chrome, "다음", () => { ClearCutSelection(); CutsceneNext(); }, true, 22);
        Place((RectTransform)next.transform, new Vector2(1, 0), new Vector2(-56, 36), new Vector2(180, 50));
        cutNextFrame = next.transform.GetChild(0).GetComponent<Image>();
        var hint = NewText(chrome, "Space / Enter", 15, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.4f));
        Place(hint.rectTransform, new Vector2(1, 0), new Vector2(-250, 36), new Vector2(220, 50));

        for (int i = 0; i < cutDots.Length; i++)
        {
            var d = NewRect("Dot" + i, chrome).gameObject.AddComponent<Image>();
            d.sprite = CutDotSprite();
            d.raycastTarget = false;
            d.color = new Color(1, 1, 1, 0.22f);
            Place(d.rectTransform, new Vector2(0, 0), new Vector2(64 + i * 24, 58), new Vector2(10, 10));
            d.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cutDots[i] = d;
        }

        var skip = NewRect("Skip", chrome);
        Place(skip, new Vector2(1, 1), new Vector2(-36, -30), new Vector2(190, 40));
        var skipImg = skip.gameObject.AddComponent<Image>();
        skipImg.sprite = UIStyle.Fill;
        skipImg.type = Image.Type.Sliced;
        skipImg.color = Color.white;
        var skipButton = skip.gameObject.AddComponent<Button>();
        UIPress.Attach(skipButton);
        skipButton.targetGraphic = skipImg;
        var colors = skipButton.colors;
        colors.normalColor = new Color(1, 1, 1, 0);
        colors.highlightedColor = new Color(1, 1, 1, 0.1f);
        colors.pressedColor = new Color(1, 1, 1, 0.18f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.08f;
        skipButton.colors = colors;
        skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
        skipButton.onClick.AddListener(() => { ClearCutSelection(); FinishCutscene(); });
        var skipText = NewText(skip, "건너뛰기 (Esc)", 17, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.62f));
        Stretch(skipText.rectTransform);
        skipText.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

        cutRoot.gameObject.SetActive(false);
    }

    // 연출기의 장면 번호가 바뀌면 자막을 새로 시작한다
    void SyncCutShot()
    {
        if (cutDirector == null) return;
        int shot = Mathf.Clamp(cutDirector.Shot, 0, CutShots.Length - 1);
        if (shot == cutShot) return;
        if (cutShot >= 0)
        {
            cutPrevCaption = CutShots[cutShot].Caption;
            cutPrevStamp = CutShots[cutShot].Stamp;
        }
        cutShot = shot;
        cutTypeForced = false;
        cutCharTimes = CutCharTimes(CutShots[shot].Caption, CutShots[shot].Delay);
    }

    // UIManager.Update에서 컷씬이 떠 있는 동안 매 프레임 부른다
    void UpdateCutscene()
    {
        if (cutRoot == null || cutDirector == null) return;
        float dt = Time.unscaledDeltaTime;
        cutTime += dt;
        SyncCutShot();
        cutShotTime = cutDirector.ShotTime;

        if (!cutEnded) HandleCutsceneKeys();

        // 레터박스는 시작하며 위아래에서 닫혀 들어온다
        float barH = cutRoot.rect.height * CutBarRatio * Mathf.SmoothStep(0, 1, cutTime / 1.4f);
        cutBarTop.sizeDelta = new Vector2(0, barH);
        cutBarBottom.sizeDelta = new Vector2(0, barH);

        // 섬광·검은 막
        var fc = cutFlash.color;
        cutFlash.color = new Color(fc.r, fc.g, fc.b, Mathf.Clamp01(cutDirector.Flash));
        cutDark.color = new Color(0, 0, 0, Mathf.Clamp01(cutDirector.Dark));

        UpdateCutCaption();

        // 쪽 표시: 지금 장은 크고 밝게
        for (int i = 0; i < cutDots.Length; i++)
        {
            var target = i == cutShot ? Accent : i < cutShot ? new Color(1, 1, 1, 0.55f) : new Color(1, 1, 1, 0.22f);
            cutDots[i].color = Color.Lerp(cutDots[i].color, target, Mathf.Clamp01(dt * 8));
            float sc = Mathf.Lerp(cutDots[i].rectTransform.localScale.x, i == cutShot ? 1.35f : 1f, Mathf.Clamp01(dt * 8));
            cutDots[i].rectTransform.localScale = new Vector3(sc, sc, 1);
        }

        // 자막을 다 보여 주면 '다음' 버튼 테두리가 숨 쉬듯 빛난다
        float pulse = CutCaptionDone ? 0.5f + 0.5f * Mathf.Sin(cutTime * 4f) : 0;
        cutNextFrame.color = Color.Lerp(new Color(0.9f, 0.8f, 0.55f, 0.4f), new Color(Accent.r, Accent.g, Accent.b, 0.95f), pulse);

        if (cutEnded) cutChrome.alpha = Mathf.MoveTowards(cutChrome.alpha, 0, dt * 4);
    }

    void HandleCutsceneKeys()
    {
        var kb = Keyboard.current;
        if (kb == null || cutTime < CutInputDelay) return;
        if (kb.escapeKey.wasPressedThisFrame) FinishCutscene();
        else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) CutsceneNext();
    }

    // 자막이 나오는 중이면 끝까지 보여 주고, 다 나왔으면 다음 장면으로 (마지막 장면이면 끝낸다)
    void CutsceneNext()
    {
        if (cutEnded || cutShot < 0 || cutTime < CutInputDelay || cutDirector == null) return;
        if (!CutCaptionDone)
        {
            cutTypeForced = true;
            return;
        }
        if (cutShot + 1 < CutShots.Length) cutDirector.Advance();
        else FinishCutscene();
    }

    bool CutCaptionDone => cutTypeForced || cutCharTimes == null || cutCharTimes.Length == 0
        || cutShotTime >= cutCharTimes[cutCharTimes.Length - 1] + CutCharFade;

    void UpdateCutCaption()
    {
        if (cutShot < 0) return;
        var shot = CutShots[cutShot];
        float start = cutCharTimes.Length > 0 ? cutCharTimes[0] : 0;
        if (!cutTypeForced && cutShotTime < start)
        {
            // 새 자막이 나오기 전: 앞 장면 자막을 지운다
            if (cutPrevCaption != null)
            {
                cutCaption.text = cutPrevCaption;
                cutStamp.text = cutPrevStamp;
                cutCaptionGroup.alpha = Mathf.Clamp01(1 - cutShotTime / 0.35f);
            }
            else cutCaptionGroup.alpha = 0;
            return;
        }

        cutCaptionGroup.alpha = 1 - Mathf.Clamp01(cutDirector.Dark * 1.5f - 0.3f);
        cutStamp.text = shot.Stamp;
        float stampAlpha = cutTypeForced ? 1 : Mathf.Clamp01((cutShotTime - start) / 0.5f);
        cutStamp.color = new Color(Accent.r, Accent.g, Accent.b, 0.85f * stampAlpha);

        if (CutCaptionDone)
        {
            if (cutCaption.text != shot.Caption) cutCaption.text = shot.Caption;
        }
        else cutCaption.text = CutTyped(shot.Caption, cutCharTimes, cutShotTime);
    }

    // 글자마다 나타나는 시각. 문장 사이(마침표 뒤 띄어쓰기)에서는 잠깐 쉰다
    static float[] CutCharTimes(string s, float delay)
    {
        var times = new float[s.Length];
        float time = delay;
        for (int i = 0; i < s.Length; i++)
        {
            times[i] = time;
            char c = s[i];
            time += c == ' ' ? 0.03f : 1f / CutCharsPerSecond;
            bool pauseNext = i + 1 < s.Length && s[i + 1] == ' ';
            if (pauseNext && (c == '.' || c == '…')) time += 0.35f;
            else if (pauseNext && c == ',') time += 0.15f;
        }
        return times;
    }

    // 아직 안 나온 글자는 투명하게 남겨 둔다 (가운데 정렬이 흔들리지 않게)
    static string CutTyped(string s, float[] times, float t)
    {
        var sb = new StringBuilder(s.Length + 64);
        int i = 0;
        for (; i < s.Length; i++)
        {
            float a = Mathf.Clamp01((t - times[i]) / CutCharFade);
            if (a <= 0) break;
            if (a >= 1) sb.Append(s[i]);
            else sb.Append("<color=#EBE6D6").Append(((int)(a * 255)).ToString("X2")).Append('>').Append(s[i]).Append("</color>");
        }
        if (i < s.Length) sb.Append("<color=#00000000>").Append(s, i, s.Length - i).Append("</color>");
        return sb.ToString();
    }

    static void ClearCutSelection()
    {
        // 눌린 버튼이 선택된 채로 남으면 Space·Enter가 버튼을 한 번 더 누른다
        var es = EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
    }

    static Sprite CutDotSprite()
    {
        if (cutDot != null) return cutDot;
        const int size = 32;
        cutDotTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CutDot" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(size / 2f - 1f - d) * 255));
            }
        cutDotTex.SetPixels32(px);
        cutDotTex.Apply();
        return cutDot = Sprite.Create(cutDotTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
