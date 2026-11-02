using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 새 게임 도입 컷씬: 사진 3장을 천천히 확대·이동(켄 번스)하며 겹쳐 넘기고, 아래에 자막을 한 글자씩 띄운다.
// 사진: Resources/Cutscene/cut1~3 (Tools > 핵폭발 생존 > 컷씬 이미지 생성). 없으면 어두운 그라데이션으로 대신한다.
// Space·Enter·'다음' 버튼으로 넘기고(자막이 나오는 중이면 먼저 자막을 끝까지 보여 준다), Esc·'건너뛰기'로 바로 끝낸다.
public partial class UIManager
{
    const float CutFade = 0.8f;            // 사진 겹침 전환 시간
    const float CutCharsPerSecond = 17f;   // 자막 타자 속도
    const float CutCharFade = 0.12f;       // 글자 하나가 떠오르는 시간
    const float CutMotionTime = 7f;        // 켄 번스 움직임이 느려지는 정도 (클수록 오래 움직인다)
    const float CutBoomTime = 0.4f;        // 2번 사진에서 폭음·섬광이 오는 때
    const float CutInputDelay = 0.5f;      // 시작 직후(화면이 밝아지는 동안)는 입력을 받지 않는다

    readonly struct CutShot
    {
        public readonly string Image, Stamp, Caption;
        public readonly float ZoomFrom, ZoomTo;
        public readonly Vector2 FocusFrom, FocusTo; // 사진 안에서 화면 가운데가 향할 곳 (0~1, 아래가 0)
        public readonly Color Top, Bottom;          // 사진이 없을 때 쓰는 그라데이션 색

        public CutShot(string image, string stamp, string caption, float zoomFrom, float zoomTo, Vector2 focusFrom, Vector2 focusTo, Color top, Color bottom)
        {
            Image = image; Stamp = stamp; Caption = caption;
            ZoomFrom = zoomFrom; ZoomTo = zoomTo;
            FocusFrom = focusFrom; FocusTo = focusTo;
            Top = top; Bottom = bottom;
        }
    }

    static readonly CutShot[] CutShots =
    {
        new CutShot("Cutscene/cut1", "오후 2시 47분", "그날도 평범한 오후였다. 창밖 하늘은 유난히 맑았다.",
            1.04f, 1.13f, new Vector2(0.52f, 0.5f), new Vector2(0.42f, 0.52f), new Color(0.3f, 0.45f, 0.62f), new Color(0.07f, 0.08f, 0.1f)),
        new CutShot("Cutscene/cut2", "오후 2시 52분", "「속보입니다. 조금 전 서울 상공에서 핵폭발이…」",
            1.02f, 1.12f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.56f), new Color(0.12f, 0.1f, 0.16f), new Color(0.5f, 0.2f, 0.07f)),
        new CutShot("Cutscene/cut3", "오후 2시 53분", "생각할 겨를도 없었다. 방공호로 달렸다.",
            1.1f, 1.15f, new Vector2(0.56f, 0.5f), new Vector2(0.44f, 0.5f), new Color(0.04f, 0.02f, 0.02f), new Color(0.36f, 0.05f, 0.03f)),
    };

    static Texture2D cutShade, cutDotTex;
    static Sprite cutDot;
    static readonly Texture2D[] cutFallbacks = new Texture2D[3];

    Action cutsceneFinished;
    RectTransform cutRoot;
    CanvasGroup cutChrome, cutCaptionGroup;
    readonly RawImage[] cutImages = new RawImage[2];
    readonly int[] cutImageShot = { -1, -1 };
    readonly float[] cutImageTime = new float[2];
    int cutFront;
    Image cutFlash, cutNextFrame;
    UnityEngine.UI.Text cutCaption, cutStamp;
    readonly Image[] cutDots = new Image[3];
    AudioSource cutAmbience, cutSiren;
    readonly Texture2D[] cutLoaded = new Texture2D[3];
    readonly Texture[] cutTextures = new Texture[3];
    float[] cutCharTimes;
    string cutPrevCaption, cutPrevStamp;
    int cutShot;
    float cutTime, cutShotTime;
    bool cutEnded = true, cutTypeForced, cutBoomed;

    // 컷씬 화면이 떠 있는 동안 true. 마지막 장에서 '다음'을 눌러도 화면은 남아 있다가
    // (씬을 불러오는 동안 검게 가려진다) HideCutscene에서 false가 된다.
    public bool IsCutscenePlaying { get; private set; }

    // 컷씬을 보여 주고, 마지막 장에서 '다음'(또는 건너뛰기)을 누르면 onFinished를 한 번 부른다.
    // 컷씬 화면은 다음 씬이 열릴 때(OnSceneLoaded → HideCutscene) 사라진다.
    public void PlayCutscene(Action onFinished)
    {
        if (cutRoot == null) BuildCutscene();
        cutsceneFinished = onFinished;
        IsCutscenePlaying = true;
        cutEnded = false;
        cutTime = 0;
        cutShot = -1;
        cutFront = 0;
        cutPrevCaption = cutPrevStamp = null;

        for (int i = 0; i < CutShots.Length; i++)
        {
            if (cutLoaded[i] == null) cutLoaded[i] = Resources.Load<Texture2D>(CutShots[i].Image);
            cutTextures[i] = cutLoaded[i] != null ? (Texture)cutLoaded[i] : CutFallback(i);
        }
        for (int i = 0; i < 2; i++)
        {
            cutImageShot[i] = -1;
            cutImages[i].texture = null;
            cutImages[i].color = new Color(1, 1, 1, 0);
        }
        cutCaption.text = cutStamp.text = "";
        cutCaptionGroup.alpha = 0;
        cutFlash.color = new Color(cutFlash.color.r, cutFlash.color.g, cutFlash.color.b, 0);
        cutChrome.alpha = 1;
        cutChrome.interactable = cutChrome.blocksRaycasts = true;

        // 메인 메뉴 위, 검은 페이드 막 아래에 둔다
        cutRoot.gameObject.SetActive(true);
        cutRoot.SetAsLastSibling();
        fadeOverlay.transform.SetAsLastSibling();
        ClearCutSelection();

        // 소리는 화면이 검은 지금 미리 만들어 둔다 (전환 중에 끊기지 않게)
        ProceduralAudio.Bang();
        cutSiren.clip = ProceduralAudio.Siren();
        cutSiren.volume = 0;
        cutSiren.pitch = 0.9f;
        cutSiren.Stop();
        cutAmbience.clip = ProceduralAudio.Wind();
        cutAmbience.volume = 0;
        cutAmbience.pitch = 1f;
        cutAmbience.Play();

        ShowCutShot(0);
    }

    void FinishCutscene()
    {
        if (cutEnded) return;
        cutEnded = true;
        if (cutChrome != null) cutChrome.interactable = cutChrome.blocksRaycasts = false;
        var done = cutsceneFinished;
        cutsceneFinished = null;
        done?.Invoke();
    }

    public void HideCutscene()
    {
        IsCutscenePlaying = false;
        cutsceneFinished = null;
        cutEnded = true;
        if (cutRoot == null) return;
        cutRoot.gameObject.SetActive(false);
        cutAmbience.Stop();
        cutSiren.Stop();
        for (int i = 0; i < 2; i++)
        {
            cutImages[i].texture = null;
            cutImageShot[i] = -1;
        }
        // 큰 사진은 메모리에서 내린다 (다음 새 게임 때 다시 불러온다)
        for (int i = 0; i < cutLoaded.Length; i++)
        {
            if (cutLoaded[i] != null) Resources.UnloadAsset(cutLoaded[i]);
            cutLoaded[i] = null;
            cutTextures[i] = null;
        }
    }

    // ---------- 화면 만들기 ----------

    void BuildCutscene()
    {
        cutRoot = NewRect("Cutscene", root);
        Stretch(cutRoot);
        var bg = cutRoot.gameObject.AddComponent<Image>();
        bg.color = Color.black; // 뒤의 메인 메뉴를 완전히 가리고 클릭도 막는다

        // 사진 두 장을 번갈아 쓴다 (새 사진이 위에서 서서히 나타난다)
        for (int i = 0; i < 2; i++)
        {
            var img = NewRect("Shot" + i, cutRoot).gameObject.AddComponent<RawImage>();
            Stretch(img.rectTransform);
            img.raycastTarget = false;
            img.color = new Color(1, 1, 1, 0);
            cutImages[i] = img;
        }

        cutFlash = NewRect("Flash", cutRoot).gameObject.AddComponent<Image>();
        Stretch(cutFlash.rectTransform);
        cutFlash.color = new Color(1f, 0.86f, 0.64f, 0);
        cutFlash.raycastTarget = false;

        // 글자가 어떤 사진 위에서도 읽히도록 아래·위를 어둡게 깐다
        var bottom = NewRect("BottomShade", cutRoot).gameObject.AddComponent<RawImage>();
        bottom.texture = CutShadeTexture();
        bottom.color = new Color(0, 0, 0, 0.88f);
        bottom.raycastTarget = false;
        var brt = bottom.rectTransform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = new Vector2(1, 0);
        brt.pivot = new Vector2(0.5f, 0);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(0, 430);

        var top = NewRect("TopShade", cutRoot).gameObject.AddComponent<RawImage>();
        top.texture = CutShadeTexture();
        top.uvRect = new Rect(0, 1, 1, -1); // 위아래 뒤집기
        top.color = new Color(0, 0, 0, 0.5f);
        top.raycastTarget = false;
        var trt = top.rectTransform;
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = Vector2.one;
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = new Vector2(0, 170);

        // 자막: 위에 작은 시각, 아래에 한 줄 대사
        var caption = NewRect("Caption", cutRoot);
        Place(caption, new Vector2(0.5f, 0), new Vector2(0, 112), new Vector2(1560, 140));
        cutCaptionGroup = caption.gameObject.AddComponent<CanvasGroup>();
        cutCaptionGroup.interactable = cutCaptionGroup.blocksRaycasts = false;
        cutStamp = NewText(caption, "", 20, TextAnchor.UpperCenter, new Color(Accent.r, Accent.g, Accent.b, 0.85f), FontSemiBold);
        Place(cutStamp.rectTransform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(1560, 30));
        cutStamp.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        cutCaption = NewText(caption, "", 36, TextAnchor.UpperCenter, Text);
        Place(cutCaption.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(1560, 96));
        cutCaption.lineSpacing = 1.2f;
        var cs = cutCaption.gameObject.AddComponent<Shadow>();
        cs.effectColor = new Color(0, 0, 0, 0.85f);
        cs.effectDistance = new Vector2(2, -2);

        // 버튼·쪽 표시·건너뛰기 (끝날 때 함께 사라진다)
        var chrome = NewRect("Chrome", cutRoot);
        Stretch(chrome);
        cutChrome = chrome.gameObject.AddComponent<CanvasGroup>();

        var next = MakeButton(chrome, "다음", () => { ClearCutSelection(); CutsceneNext(); }, true, 24);
        Place((RectTransform)next.transform, new Vector2(1, 0), new Vector2(-64, 50), new Vector2(200, 58));
        cutNextFrame = next.transform.GetChild(0).GetComponent<Image>();
        var hint = NewText(chrome, "Space / Enter", 16, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.4f));
        Place(hint.rectTransform, new Vector2(1, 0), new Vector2(-282, 50), new Vector2(220, 58));

        for (int i = 0; i < cutDots.Length; i++)
        {
            var d = NewRect("Dot" + i, chrome).gameObject.AddComponent<Image>();
            d.sprite = CutDotSprite();
            d.raycastTarget = false;
            d.color = new Color(1, 1, 1, 0.22f);
            Place(d.rectTransform, new Vector2(0.5f, 0), new Vector2((i - 1) * 26, 48), new Vector2(11, 11));
            d.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cutDots[i] = d;
        }

        var skip = NewRect("Skip", chrome);
        Place(skip, new Vector2(1, 1), new Vector2(-36, -28), new Vector2(190, 42));
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
        var skipText = NewText(skip, "건너뛰기 (Esc)", 18, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.62f));
        Stretch(skipText.rectTransform);
        skipText.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

        // 배경 소리 (바람 → 2번에서 낮게 울리는 굉음, 3번에서 먼 사이렌)
        cutAmbience = cutRoot.gameObject.AddComponent<AudioSource>();
        cutSiren = cutRoot.gameObject.AddComponent<AudioSource>();
        foreach (var a in new[] { cutAmbience, cutSiren })
        {
            a.playOnAwake = false;
            a.loop = true;
            a.spatialBlend = 0;
            a.volume = 0;
        }

        cutRoot.gameObject.SetActive(false);
    }

    void ShowCutShot(int shot)
    {
        if (cutShot >= 0)
        {
            cutPrevCaption = CutShots[cutShot].Caption;
            cutPrevStamp = CutShots[cutShot].Stamp;
        }
        cutShot = shot;
        cutShotTime = 0;
        cutTypeForced = false;
        cutBoomed = false;
        cutCharTimes = CutCharTimes(CutShots[shot].Caption, shot == 0 ? 1.0f : 0.75f);

        // 앞 사진은 그대로 두고 다른 칸에 새 사진을 올린다. 첫 장은 페이드 막이 걷히며 보이므로 바로 띄운다
        int slot = cutImageShot[cutFront] < 0 ? cutFront : 1 - cutFront;
        if (slot != cutFront) cutImages[cutFront].color = Color.white; // 전환 도중에 넘겨도 아래 사진이 비치지 않게
        var img = cutImages[slot];
        img.texture = cutTextures[shot];
        img.color = new Color(1, 1, 1, shot == 0 ? 1 : 0);
        img.transform.SetSiblingIndex(1); // 두 사진 칸 가운데 위쪽 (0·1번 자식이 사진 칸)
        cutImageShot[slot] = shot;
        cutImageTime[slot] = 0;
        cutFront = slot;
    }

    // UIManager.Update에서 컷씬이 떠 있는 동안 매 프레임 부른다
    void UpdateCutscene()
    {
        if (cutRoot == null || cutShot < 0) return;
        float dt = Time.unscaledDeltaTime;
        cutTime += dt;
        cutShotTime += dt;

        if (!cutEnded) HandleCutsceneKeys();

        // 사진: 겹침 전환 + 켄 번스
        float aspect = cutRoot.rect.height > 1 ? cutRoot.rect.width / cutRoot.rect.height : 16f / 9f;
        for (int i = 0; i < 2; i++)
        {
            int s = cutImageShot[i];
            if (s < 0) continue;
            cutImageTime[i] += dt;
            var img = cutImages[i];
            if (i == cutFront)
            {
                if (s > 0) img.color = new Color(1, 1, 1, Mathf.SmoothStep(0, 1, cutImageTime[i] / CutFade));
            }
            else if (cutImageTime[cutFront] >= CutFade)
            {
                // 새 사진이 다 나타나면 아래 사진은 치운다
                cutImageShot[i] = -1;
                img.texture = null;
                img.color = new Color(1, 1, 1, 0);
                continue;
            }
            CutKenBurns(img, s, cutImageTime[i], aspect);
        }

        // 2번 사진: 먼 폭음과 짧은 섬광
        float flash = 0;
        if (cutShot == 1 && cutShotTime >= CutBoomTime)
        {
            if (!cutBoomed && !cutEnded)
            {
                cutBoomed = true;
                ProceduralAudio.Play2D(ProceduralAudio.Bang(), 0.55f, 0.42f);
                ProceduralAudio.Play2D(ProceduralAudio.Bang(), 0.35f, 0.3f);
            }
            flash = 0.32f * Mathf.Exp(-(cutShotTime - CutBoomTime) * 3.5f);
        }
        var fc = cutFlash.color;
        cutFlash.color = new Color(fc.r, fc.g, fc.b, flash);

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

        UpdateCutAudio(dt);
    }

    void HandleCutsceneKeys()
    {
        var kb = Keyboard.current;
        if (kb == null || cutTime < CutInputDelay) return;
        if (kb.escapeKey.wasPressedThisFrame) FinishCutscene();
        else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) CutsceneNext();
    }

    // 자막이 나오는 중이면 끝까지 보여 주고, 다 나왔으면 다음 장으로 (마지막 장이면 끝낸다)
    void CutsceneNext()
    {
        if (cutEnded || cutShot < 0 || cutTime < CutInputDelay) return;
        if (!CutCaptionDone)
        {
            cutTypeForced = true;
            return;
        }
        if (cutShot + 1 < CutShots.Length) ShowCutShot(cutShot + 1);
        else FinishCutscene();
    }

    bool CutCaptionDone => cutTypeForced || cutCharTimes == null || cutCharTimes.Length == 0
        || cutShotTime >= cutCharTimes[cutCharTimes.Length - 1] + CutCharFade;

    void UpdateCutCaption()
    {
        var shot = CutShots[cutShot];
        float start = cutCharTimes.Length > 0 ? cutCharTimes[0] : 0;
        if (!cutTypeForced && cutShotTime < start)
        {
            // 새 자막이 나오기 전: 앞 장 자막을 지운다
            if (cutPrevCaption != null)
            {
                cutCaption.text = cutPrevCaption;
                cutStamp.text = cutPrevStamp;
                cutCaptionGroup.alpha = Mathf.Clamp01(1 - cutShotTime / 0.35f);
            }
            else cutCaptionGroup.alpha = 0;
            return;
        }

        cutCaptionGroup.alpha = 1;
        cutStamp.text = shot.Stamp;
        float stampAlpha = cutTypeForced ? 1 : Mathf.Clamp01((cutShotTime - start) / 0.5f);
        cutStamp.color = new Color(Accent.r, Accent.g, Accent.b, 0.85f * stampAlpha);

        if (CutCaptionDone)
        {
            if (cutCaption.text != shot.Caption) cutCaption.text = shot.Caption;
        }
        else cutCaption.text = CutTyped(shot.Caption, cutCharTimes, cutShotTime);
    }

    void UpdateCutAudio(float dt)
    {
        float amb = cutEnded ? 0 : cutShot == 0 ? 0.12f : cutShot == 1 ? 0.3f : 0.2f;
        float pitch = cutShot == 0 ? 1f : cutShot == 1 ? 0.55f : 0.72f;
        float siren = !cutEnded && cutShot == 2 ? 0.13f : 0;
        float rate = cutEnded ? 0.6f : 0.25f; // 끝나면 화면이 검어지는 동안 빠르게 줄인다
        cutAmbience.volume = Mathf.MoveTowards(cutAmbience.volume, amb, dt * rate);
        cutAmbience.pitch = Mathf.MoveTowards(cutAmbience.pitch, pitch, dt * 0.4f);
        if (siren > 0 && !cutSiren.isPlaying) cutSiren.Play();
        cutSiren.volume = Mathf.MoveTowards(cutSiren.volume, siren, dt * (cutEnded ? 0.4f : 0.1f));
    }

    // 사진을 화면 비율에 맞게 꽉 채워 자르고(uvRect), 시간에 따라 천천히 확대·이동한다
    void CutKenBurns(RawImage img, int shot, float t, float screenAspect)
    {
        var s = CutShots[shot];
        float p = 1 - Mathf.Exp(-t / CutMotionTime);
        float zoom = Mathf.Max(1f, Mathf.Lerp(s.ZoomFrom, s.ZoomTo, p));
        Vector2 focus = Vector2.Lerp(s.FocusFrom, s.FocusTo, p);
        if (shot == 1 && t > CutBoomTime)
        {
            // 폭음과 함께 화면이 짧게 흔들린다
            float shake = 0.006f * Mathf.Exp(-(t - CutBoomTime) * 3f);
            focus += new Vector2(Mathf.PerlinNoise(t * 14f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 14f) - 0.5f) * (2 * shake);
        }

        var tex = cutLoaded[shot];
        float texAspect = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 16f / 9f;
        float w = 1, h = 1;
        if (screenAspect > texAspect) h = texAspect / screenAspect;
        else w = screenAspect / texAspect;
        w /= zoom;
        h /= zoom;
        float x = Mathf.Clamp(focus.x - w / 2, 0, 1 - w);
        float y = Mathf.Clamp(focus.y - h / 2, 0, 1 - h);
        img.uvRect = new Rect(x, y, w, h);
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

    // ---------- 코드로 만드는 텍스처 ----------

    // 아래가 진하고 위로 갈수록 투명해지는 세로 그라데이션 (흰색, 색은 RawImage color로 준다)
    static Texture2D CutShadeTexture()
    {
        if (cutShade != null) return cutShade;
        const int h = 128;
        cutShade = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CutShade" };
        for (int y = 0; y < h; y++)
        {
            float a = Mathf.Pow(1f - y / (h - 1f), 1.7f);
            cutShade.SetPixel(0, y, new Color(1, 1, 1, a));
            cutShade.SetPixel(1, y, new Color(1, 1, 1, a));
        }
        cutShade.Apply();
        return cutShade;
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

    // 사진 파일이 없을 때 대신 보여 줄 어두운 그라데이션
    static Texture2D CutFallback(int shot)
    {
        if (cutFallbacks[shot] != null) return cutFallbacks[shot];
        const int h = 64;
        var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CutFallback" + shot };
        for (int y = 0; y < h; y++)
        {
            var c = Color.Lerp(CutShots[shot].Bottom, CutShots[shot].Top, y / (h - 1f));
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();
        return cutFallbacks[shot] = tex;
    }
}
