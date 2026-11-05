using System;
using UnityEngine;
using UnityEngine.UI;

// 패치노트 데이터: Assets/Resources/PatchNotes.json
// 새 버전을 낼 때 이 파일 맨 위에 항목을 추가한다 (added 추가사항 / changed 수정사항 / patched 패치사항)
[Serializable]
public class PatchNotesData
{
    public PatchVersion[] versions;
}

[Serializable]
public class PatchVersion
{
    public string version;
    public string date;
    public string title;
    public string[] added;
    public string[] changed;
    public string[] patched;
}

// 메인 메뉴 '패치노트' 화면: 뒤의 3D 배경을 흐리게(블러) 깔고, 메인 메뉴와 같은 더러운 강판 위에 버전별 내용을 보여 준다 (2026-11-05 저녁).
// 왼쪽 버전 목록과 오른쪽 내용 모두 넘치면 휠로 스크롤한다
public partial class UIManager
{
    static readonly Color AddedColor = new Color(0.45f, 0.84f, 0.52f);
    static readonly Color ChangedColor = new Color(0.98f, 0.76f, 0.32f);
    static readonly Color PatchedColor = new Color(0.47f, 0.7f, 1f);

    static PatchNotesData patchNotesData;

    GameObject patchRoot;
    RawImage patchBlur;
    RectTransform patchVersionList, patchContent;
    ScrollRect patchScroll, patchVersionScroll;
    int patchSelected;
    RenderTexture blurCapture, blurResult;

    public bool IsPatchNotesOpen => patchRoot != null && patchRoot.activeSelf;

    public static PatchNotesData PatchNotes
    {
        get
        {
            if (patchNotesData == null)
            {
                var asset = Resources.Load<TextAsset>("PatchNotes");
                patchNotesData = asset != null ? JsonUtility.FromJson<PatchNotesData>(asset.text) : null;
                if (patchNotesData == null || patchNotesData.versions == null)
                    patchNotesData = new PatchNotesData { versions = new PatchVersion[0] };
            }
            return patchNotesData;
        }
    }

    public void OpenPatchNotes()
    {
        if (patchRoot == null) BuildPatchNotes();
        ClosePanel();
        patchSelected = 0;
        patchRoot.SetActive(true);
        patchRoot.transform.SetAsLastSibling();
        fadeOverlay.transform.SetAsLastSibling();
        RefreshGlass(); // 뒤 배경을 바로 찍는다. 열려 있는 동안은 UpdateGlass가 0.1초마다 다시 찍는다
        RefreshPatchNotes();
        patchVersionScroll.verticalNormalizedPosition = 1;
    }

    public void ClosePatchNotes()
    {
        if (patchRoot != null) patchRoot.SetActive(false);
    }

    void BuildPatchNotes()
    {
        var r = NewRect("PatchNotes", root);
        Stretch(r);
        patchRoot = r.gameObject;

        patchBlur = BlurBackdrop(r, new Color(0.02f, 0.025f, 0.03f, 0.28f));

        var glass = PlateWindow(r, "Glass");
        glass.gameObject.AddComponent<UIPopIn>();
        Place(glass, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1480, 840));

        RedTitleBar(glass, new Vector2(30, -28), 48);
        var title = NewText(glass, "패치노트", 38, TextAnchor.MiddleLeft, Text, FontTitle);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(44, -24), new Vector2(200, 56));
        var sub = NewText(glass, "버전별 추가사항 · 수정사항 · 패치사항", 19, TextAnchor.MiddleLeft, Dim);
        Place(sub.rectTransform, new Vector2(0, 1), new Vector2(214, -34), new Vector2(600, 40));
        var close = PlateButton(glass, "닫기", "ESC", ClosePatchNotes);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-36, -28), new Vector2(150, 44));
        var line = NewRect("Line", glass);
        line.anchorMin = new Vector2(0, 1);
        line.anchorMax = new Vector2(1, 1);
        line.pivot = new Vector2(0.5f, 1);
        line.anchoredPosition = new Vector2(0, -100);
        line.sizeDelta = new Vector2(-72, 2);
        line.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);

        // 왼쪽: 버전 목록 (버전이 많아 창을 넘치면 휠로 스크롤한다 — 2026-11-05 저녁, 창 밖으로 뚫고 나가던 것을 고침)
        var vview = NewRect("VersionView", glass);
        vview.anchorMin = new Vector2(0, 0);
        vview.anchorMax = new Vector2(0, 1);
        vview.pivot = new Vector2(0, 1);
        vview.offsetMin = new Vector2(36, 36);
        vview.offsetMax = new Vector2(336, -122);
        vview.gameObject.AddComponent<RectMask2D>();
        vview.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        patchVersionList = NewRect("Versions", vview);
        patchVersionList.anchorMin = new Vector2(0, 1);
        patchVersionList.anchorMax = new Vector2(1, 1);
        patchVersionList.pivot = new Vector2(0.5f, 1);
        patchVersionList.sizeDelta = Vector2.zero;
        var vl = patchVersionList.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 8;
        vl.padding = new RectOffset(0, 0, 2, 8);
        vl.childControlHeight = true;
        vl.childControlWidth = true;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;
        patchVersionList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        patchVersionScroll = vview.gameObject.AddComponent<ScrollRect>();
        patchVersionScroll.content = patchVersionList;
        patchVersionScroll.viewport = vview;
        patchVersionScroll.horizontal = false;
        patchVersionScroll.movementType = ScrollRect.MovementType.Clamped;
        patchVersionScroll.scrollSensitivity = 40;

        var vline = NewRect("VLine", glass);
        vline.anchorMin = new Vector2(0, 0);
        vline.anchorMax = new Vector2(0, 1);
        vline.offsetMin = new Vector2(360, 36);
        vline.offsetMax = new Vector2(362, -122);
        vline.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);

        // 오른쪽: 선택한 버전 내용 (휠로 스크롤)
        var view = NewRect("View", glass);
        view.anchorMin = Vector2.zero;
        view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(394, 36);
        view.offsetMax = new Vector2(-40, -118);
        view.gameObject.AddComponent<RectMask2D>();
        view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        patchContent = NewRect("Content", view);
        patchContent.anchorMin = new Vector2(0, 1);
        patchContent.anchorMax = new Vector2(1, 1);
        patchContent.pivot = new Vector2(0.5f, 1);
        patchContent.sizeDelta = Vector2.zero;
        var layout = patchContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(0, 24, 4, 24);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        patchContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        patchScroll = view.gameObject.AddComponent<ScrollRect>();
        patchScroll.content = patchContent;
        patchScroll.viewport = view;
        patchScroll.horizontal = false;
        patchScroll.movementType = ScrollRect.MovementType.Clamped;
        patchScroll.scrollSensitivity = 40;
        MakeScrollbar(glass, patchScroll, new Vector2(-30, 36), new Vector2(-24, -118));

        patchRoot.SetActive(false);
    }

    void RefreshPatchNotes()
    {
        ClearChildren(patchVersionList);
        ClearChildren(patchContent);

        var versions = PatchNotes.versions;
        for (int i = 0; i < versions.Length; i++)
        {
            int k = i;
            VersionTab(versions[i], i == patchSelected, () => { patchSelected = k; RefreshPatchNotes(); });
        }
        if (versions.Length == 0)
        {
            Line(patchContent, "패치노트가 없습니다.", 22, Dim);
            return;
        }

        var v = versions[Mathf.Clamp(patchSelected, 0, versions.Length - 1)];
        var head = NewText(patchContent, v.version, 40, TextAnchor.MiddleLeft, Text, FontBold);
        head.gameObject.AddComponent<LayoutElement>().minHeight = 54;
        if (!string.IsNullOrEmpty(v.title))
        {
            var t = NewText(patchContent, v.title, 24, TextAnchor.MiddleLeft, Accent, FontSemiBold);
            t.gameObject.AddComponent<LayoutElement>().minHeight = 34;
        }
        string meta = v.date + (IsCurrentVersion(v.version) ? "   ·   지금 플레이 중인 버전" : "");
        Line(patchContent, meta, 18, Dim);

        Section("추가사항", AddedColor, v.added);
        Section("수정사항", ChangedColor, v.changed);
        Section("패치사항", PatchedColor, v.patched);

        Canvas.ForceUpdateCanvases();
        patchScroll.verticalNormalizedPosition = 1;
    }

    // 버전 탭: 더러운 강판. 고른 탭은 흙색 판에 오른쪽 끝 붉은 막대가 켜진다 (메인 메뉴 버튼과 같은 꼴)
    // 패치노트의 버전 이름("26.0.2.0 (First BETA)")과 게임 버전("26.0.2.0b")은 꾸밈말이 달라 숫자 부분만 비교한다
    static string VersionCore(string v)
    {
        var m = System.Text.RegularExpressions.Regex.Match(v ?? "", @"\d+(\.\d+)*");
        return m.Success ? m.Value : (v ?? "");
    }

    static bool IsCurrentVersion(string v) => VersionCore(v) == VersionCore(Application.version);

    void VersionTab(PatchVersion v, bool selected, Action onClick)
    {
        var rt = NewRect("Version", patchVersionList);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UIStyle.Fill;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        var frame = DirtyPlate(rt, 0.7f);
        var b = rt.gameObject.AddComponent<Button>();
        UIPress.Attach(b, 1.01f);
        b.targetGraphic = img;
        var colors = b.colors;
        colors.normalColor = selected ? InfoFill : PlateFill;
        colors.highlightedColor = selected ? new Color(0.38f, 0.36f, 0.24f, 0.9f) : PlateHover;
        colors.pressedColor = new Color(0.24f, 0.23f, 0.22f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(() => onClick());
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 64;
        RedBar(rt, 12, new Vector2(4, 26), 40, selected ? 0.3f : 0f);
        if (!selected) rt.Find("RedBar").GetComponent<Image>().color = Color.clear;
        var hover = rt.gameObject.AddComponent<UIHover>();
        hover.Gate = b;
        hover.Add(frame, new Color(1, 1, 1, selected ? 0.5f : 0.3f), new Color(1, 1, 1, 0.75f));

        var ver = NewText(rt, v.version, 22, TextAnchor.UpperLeft, selected ? Text : new Color(Text.r, Text.g, Text.b, 0.85f), FontTitle);
        Place(ver.rectTransform, new Vector2(0, 1), new Vector2(18, -9), new Vector2(170, 30));
        var date = NewText(rt, v.date, 14, TextAnchor.UpperLeft, PaleDim);
        Place(date.rectTransform, new Vector2(0, 1), new Vector2(18, -38), new Vector2(170, 22));

        string badge = IsCurrentVersion(v.version) ? "현재" : v.date == "개발 중" ? "예정" : "";
        if (badge != "")
        {
            var c = badge == "현재" ? AddedColor : ChangedColor;
            var chip = Box(rt, "Badge", new Color(c.r, c.g, c.b, 0.16f), new Color(c.r, c.g, c.b, 0.6f));
            Place(chip, new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(62, 28));
            var bt = NewText(chip, badge, 15, TextAnchor.MiddleCenter, c, FontSemiBold);
            Stretch(bt.rectTransform);
        }
    }

    void Section(string label, Color color, string[] items)
    {
        if (items == null || items.Length == 0) return;
        Spacer(patchContent, 12);

        var row = NewRect("SectionHead", patchContent);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        var rle = row.gameObject.AddComponent<LayoutElement>();
        rle.minHeight = rle.preferredHeight = 40;
        var chip = Box(row, "Chip", new Color(color.r, color.g, color.b, 0.16f), new Color(color.r, color.g, color.b, 0.7f));
        var cle = chip.gameObject.AddComponent<LayoutElement>();
        cle.minWidth = cle.preferredWidth = 128;
        cle.minHeight = cle.preferredHeight = 38;
        var chipText = NewText(chip, label, 20, TextAnchor.MiddleCenter, color, FontBold);
        Stretch(chipText.rectTransform);
        var count = NewText(row, items.Length + "건", 18, TextAnchor.MiddleLeft, Dim);
        count.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;

        foreach (var item in items)
        {
            var line = NewRect("Item", patchContent);
            var hl = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12;
            hl.padding = new RectOffset(10, 0, 2, 2);
            hl.childAlignment = TextAnchor.UpperLeft;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            var dot = NewText(line, "•", 24, TextAnchor.UpperCenter, color, FontBold);
            dot.gameObject.AddComponent<LayoutElement>().preferredWidth = 14;
            var t = NewText(line, item, 21, TextAnchor.UpperLeft, Text);
            t.lineSpacing = 1.25f;
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }
    }

    static void Spacer(RectTransform parent, float height)
    {
        var s = NewRect("Spacer", parent);
        var le = s.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
    }

    static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
    }

    // ---------- 유리판 블러 (패치노트·제작대·트럭·일반 창·메뉴가 함께 쓴다. 패치노트·일반 창·메뉴의 갱신은 UIManager.cs의 UpdateGlass) ----------

    static readonly Color GlassFill = new Color(0.035f, 0.037f, 0.04f, 0.9f);
    static readonly Color GlassFrame = new Color(0.72f, 0.48f, 0.2f, 0.22f);

    // 화면 전체에 흐린 배경(RawImage)과 어두운 막을 깐다. 막은 뒤 화면을 누르지 못하게 막는 역할도 한다
    RawImage BlurBackdrop(RectTransform parent, Color tint)
    {
        var blur = NewRect("Blur", parent).gameObject.AddComponent<RawImage>();
        Stretch(blur.rectTransform);
        blur.raycastTarget = false;
        var t = NewRect("Tint", parent).gameObject.AddComponent<Image>();
        Stretch(t.rectTransform);
        t.color = tint;
        return blur;
    }

    // 뒤 배경 블러: 카메라 화면을 작게 그린 뒤 여러 번 줄였다가 다시 키워 부드럽게 번지게 한다 (가벼운 방식)
    // 블러 텍스처는 하나를 같이 쓴다 (블러 창은 한 번에 하나만 열린다)
    void UpdateBlur(RawImage target)
    {
        var cam = Camera.main;
        if (cam == null || target == null) return;
        int w = Mathf.Max(160, Screen.width / 3), h = Mathf.Max(90, Screen.height / 3);
        if (blurCapture == null || blurCapture.width != w || blurCapture.height != h)
        {
            ReleaseBlur();
            blurCapture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, name = "UIBlurCapture" };
            blurResult = new RenderTexture(w / 2, h / 2, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, name = "UIBlur" };
        }

        var prev = cam.targetTexture;
        cam.targetTexture = blurCapture;
        cam.Render();
        cam.targetTexture = prev;

        var a = RenderTexture.GetTemporary(w / 2, h / 2, 0, RenderTextureFormat.ARGB32);
        var b = RenderTexture.GetTemporary(w / 4, h / 4, 0, RenderTextureFormat.ARGB32);
        var c = RenderTexture.GetTemporary(w / 8, h / 8, 0, RenderTextureFormat.ARGB32);
        a.filterMode = b.filterMode = c.filterMode = FilterMode.Bilinear;
        Graphics.Blit(blurCapture, a);
        Graphics.Blit(a, b);
        Graphics.Blit(b, c);
        Graphics.Blit(c, b);
        Graphics.Blit(b, a);
        Graphics.Blit(a, blurResult);
        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);
        RenderTexture.ReleaseTemporary(c);
        target.texture = blurResult;
    }

    void ReleaseBlur()
    {
        if (blurCapture != null) { blurCapture.Release(); Destroy(blurCapture); }
        if (blurResult != null) { blurResult.Release(); Destroy(blurResult); }
        blurCapture = blurResult = null;
    }
}
