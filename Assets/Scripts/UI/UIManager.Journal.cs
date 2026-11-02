using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 탐험 결과 일지: 방공호로 돌아오면 낡은 양피지 수첩 한 장에 그날의 탐험을 손으로 적어 내려간다.
// 종이를 펼치는 소리 → 한 글자씩 적히며 펜이 사각사각 긁히는 소리. 다 적히면 클릭·Esc·스페이스로 덮는다
// (적는 도중 누르면 남은 글을 한꺼번에 적는다)
public partial class UIManager
{
    // 탐험 하나의 기록 (GameManager가 채운다)
    public class ExpeditionReport
    {
        public int Turn, TurnsSpent;
        public float HealthBefore, HealthAfter;
        public int ZombiesKilled, DogsKilled;
        public bool RedNight;
        public float TimeLeft, RedNightSeconds;
        public readonly List<KeyValuePair<string, int>> Gained = new List<KeyValuePair<string, int>>();
        public readonly List<KeyValuePair<string, int>> Spent = new List<KeyValuePair<string, int>>();
    }

    static readonly Color Ink = new Color(0.19f, 0.12f, 0.07f);
    static readonly Color InkFaded = new Color(0.33f, 0.24f, 0.15f);
    static readonly Color InkRed = new Color(0.5f, 0.1f, 0.06f);
    const float JournalCharsPerSecond = 38f;

    GameObject journalRoot;
    RectTransform journalPage, journalLines;
    CanvasGroup journalGroup;
    UnityEngine.UI.Text journalHint;
    readonly List<UnityEngine.UI.Text> journalTexts = new List<UnityEngine.UI.Text>();
    readonly List<string> journalFull = new List<string>();
    int journalLine;
    float journalChars, journalScratchTimer, journalOpenT;
    bool journalDone;
    static Sprite parchment;

    public bool IsJournalOpen => journalRoot != null && journalRoot.activeSelf;

    public void ShowExpeditionJournal(ExpeditionReport r)
    {
        if (journalRoot == null) BuildJournal();
        ClosePanel();
        ClearChildren(journalLines);
        journalTexts.Clear();
        journalFull.Clear();

        // 제목·날짜
        AddJournalLine($"탐험 일지  —  {r.Turn}번째 날", 40, Ink, FontBold, 6, -1.2f);
        string back = r.RedNight ? $"붉은 밤 속을 {Mathf.CeilToInt(r.RedNightSeconds)}초나 헤매다 겨우 해치로 돌아왔다."
            : $"붉은 밤까지 {(int)r.TimeLeft / 60}분 {(int)r.TimeLeft % 60}초를 남기고 해치로 돌아왔다.";
        AddJournalLine(back, 23, r.RedNight ? InkRed : InkFaded, Font, 18, 0.6f);
        AddJournalRule();

        // 챙겨 온 것
        AddJournalLine("챙겨 온 것", 27, Ink, FontSemiBold, 8, -0.5f);
        if (r.Gained.Count == 0) AddJournalLine("  아무것도 챙기지 못했다. 빈손이다...", 23, InkFaded, Font, 4, 0.4f);
        else
            for (int i = 0; i < r.Gained.Count; i++)
            {
                var kv = r.Gained[i];
                AddJournalLine($"  •  {ItemDatabase.Name(kv.Key)}  x{kv.Value}", 23, Ink, Font, 2, Random.Range(-0.8f, 0.8f));
            }
        if (r.Spent.Count > 0)
        {
            var parts = new List<string>();
            foreach (var kv in r.Spent) parts.Add($"{ItemDatabase.Name(kv.Key)} {kv.Value}");
            AddJournalLine("  써 버린 것:  " + string.Join(", ", parts), 20, InkFaded, Font, 2, 0.3f);
        }

        // 만난 놈들·몸 상태
        AddJournalLine("", 10, Ink, Font, 4, 0);
        AddJournalLine("놈들", 27, Ink, FontSemiBold, 8, 0.5f);
        if (r.ZombiesKilled + r.DogsKilled == 0) AddJournalLine("  싸우지 않고 숨어 다녔다.", 23, InkFaded, Font, 4, -0.3f);
        else
        {
            var kills = new List<string>();
            if (r.ZombiesKilled > 0) kills.Add($"좀비 {r.ZombiesKilled}마리");
            if (r.DogsKilled > 0) kills.Add($"들개 {r.DogsKilled}마리");
            AddJournalLine("  쓰러뜨린 것:  " + string.Join(", ", kills), 23, Ink, Font, 4, -0.4f);
        }
        float lost = r.HealthBefore - r.HealthAfter;
        AddJournalLine($"  몸 상태:  체력 {r.HealthBefore:0} → {r.HealthAfter:0}" + (lost > 0.5f ? $"  (-{lost:0})" : "  (다치지 않았다)"), 23, lost > 30 ? InkRed : Ink, Font, 4, 0.5f);
        AddJournalRule();

        // 마무리 한 줄
        string closing;
        if (r.RedNight) closing = "다시는 이렇게 늦지 말자. 붉은 밤은 사람을 갉아먹는다.";
        else if (r.HealthAfter < 30) closing = "죽을 뻔했다. 상처부터 돌보자.";
        else if (r.Gained.Count == 0) closing = "내일은 더 멀리 가 봐야겠다.";
        else if (r.ZombiesKilled + r.DogsKilled >= 5) closing = "놈들이 점점 늘어난다. 무기를 손봐 두자.";
        else closing = "오늘도 살아서 돌아왔다. 창고에 물건을 정리하자.";
        AddJournalLine(closing, 24, Ink, Font, 6, -0.8f);
        AddJournalLine($"(턴 {r.Turn} / {GameState.MaxTurns}  ·  {r.TurnsSpent}턴 지남)", 18, InkFaded, Font, 0, 0.2f);

        journalLine = 0;
        journalChars = 0;
        journalDone = false;
        journalOpenT = 0;
        journalHint.text = "";
        journalPage.localRotation = Quaternion.Euler(0, 0, Random.Range(-1.6f, 1.6f));
        journalRoot.SetActive(true);
        journalRoot.transform.SetAsLastSibling();
        ProceduralAudio.Play2D(ProceduralAudio.PaperUnfold(), 0.75f, Random.Range(0.95f, 1.05f), SoundCategory.UI);
    }

    void AddJournalLine(string text, int size, Color color, Font font, float spaceBefore, float tilt)
    {
        var holder = NewRect("Line", journalLines);
        var le = holder.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = size * 1.5f + spaceBefore;
        var t = NewText(holder, "", size, TextAnchor.LowerLeft, color, font);
        t.fontStyle = FontStyle.Italic; // 손글씨처럼 비스듬히
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(t.rectTransform, Random.Range(0f, 6f), 0);
        t.rectTransform.localRotation = Quaternion.Euler(0, 0, tilt);
        journalTexts.Add(t);
        journalFull.Add(text);
    }

    // 펜으로 그은 가로줄
    void AddJournalRule()
    {
        var holder = NewRect("Rule", journalLines);
        var le = holder.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 22;
        var line = NewRect("Ink", holder);
        line.anchorMin = new Vector2(0, 0.5f);
        line.anchorMax = new Vector2(0.92f, 0.5f);
        line.sizeDelta = new Vector2(0, 2.4f);
        line.localRotation = Quaternion.Euler(0, 0, Random.Range(-0.6f, 0.6f));
        var img = line.gameObject.AddComponent<Image>();
        img.color = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
        img.raycastTarget = false;
    }

    void BuildJournal()
    {
        var r = NewRect("Journal", root);
        Stretch(r);
        journalRoot = r.gameObject;
        journalGroup = r.gameObject.AddComponent<CanvasGroup>();
        var dim = r.gameObject.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.62f);
        // 아무 곳이나 누르면 (다 적혔으면) 덮는다
        var click = r.gameObject.AddComponent<Button>();
        click.transition = Selectable.Transition.None;
        click.onClick.AddListener(JournalClick);

        journalPage = NewRect("Page", r);
        Place(journalPage, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(860, 1000));
        var page = journalPage.gameObject.AddComponent<Image>();
        page.sprite = Parchment;
        page.raycastTarget = false;
        var shadow = journalPage.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.6f);
        shadow.effectDistance = new Vector2(10, -12);

        journalLines = NewRect("Lines", journalPage);
        journalLines.anchorMin = Vector2.zero;
        journalLines.anchorMax = Vector2.one;
        journalLines.offsetMin = new Vector2(96, 120);
        journalLines.offsetMax = new Vector2(-80, -92);
        var vl = journalLines.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childControlHeight = true;
        vl.childControlWidth = true;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;
        vl.spacing = 0;

        journalHint = NewText(journalPage, "", 19, TextAnchor.LowerRight, InkFaded, Font);
        journalHint.fontStyle = FontStyle.Italic;
        Place(journalHint.rectTransform, new Vector2(1, 0), new Vector2(-80, 64), new Vector2(520, 30));
        journalRoot.SetActive(false);
    }

    void JournalClick()
    {
        if (!journalDone)
        {
            // 남은 글을 한꺼번에 적는다
            for (int i = 0; i < journalTexts.Count; i++) journalTexts[i].text = journalFull[i];
            journalLine = journalTexts.Count;
            FinishJournal();
            return;
        }
        CloseJournal();
    }

    void FinishJournal()
    {
        journalDone = true;
        journalHint.text = "— 일지를 덮으려면 클릭하거나 Esc";
    }

    public void CloseJournal()
    {
        if (!IsJournalOpen) return;
        journalRoot.SetActive(false);
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.5f, 0.8f, SoundCategory.UI);
    }

    // 매 프레임: 종이가 살짝 떠오르며 나타나고, 한 글자씩 적히며 사각사각 소리
    void UpdateJournal()
    {
        if (!IsJournalOpen) return;
        float dt = Time.unscaledDeltaTime;
        journalOpenT = Mathf.Min(1f, journalOpenT + dt / 0.35f);
        float e = 1 - (1 - journalOpenT) * (1 - journalOpenT);
        journalGroup.alpha = e;
        journalPage.anchoredPosition = new Vector2(0, -6 - 40 * (1 - e));
        if (journalDone || journalOpenT < 0.6f) return;

        journalChars += dt * JournalCharsPerSecond;
        while (journalLine < journalTexts.Count)
        {
            string full = journalFull[journalLine];
            int n = Mathf.Min(full.Length, Mathf.FloorToInt(journalChars));
            journalTexts[journalLine].text = full.Substring(0, n);
            if (n < full.Length) break;
            journalChars -= full.Length;
            journalLine++;
        }
        // 적는 동안 펜 소리를 짧게 끊어 낸다 (글자 사이 간격이 조금씩 다르게)
        journalScratchTimer -= dt;
        if (journalLine < journalTexts.Count && journalScratchTimer <= 0 && journalFull[journalLine].Length > 0)
        {
            journalScratchTimer = Random.Range(0.07f, 0.13f);
            ProceduralAudio.Play2D(ProceduralAudio.PenScratch(), Random.Range(0.32f, 0.5f), Random.Range(0.85f, 1.25f), SoundCategory.UI);
        }
        if (journalLine >= journalTexts.Count) FinishJournal();
    }

    // 낡은 양피지: 누런 바탕 + 섬유 결 + 얼룩·커피 자국 + 가장자리가 그을려 들쭉날쭉하게 투명해진다
    static Sprite Parchment
    {
        get
        {
            if (parchment != null) return parchment;
            const int w = 430, h = 500;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Parchment" };
            var px = new Color[w * h];
            var rng = new System.Random(77);
            var stains = new Vector3[5];
            for (int i = 0; i < stains.Length; i++) stains[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), 0.05f + 0.08f * (float)rng.NextDouble());
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float big = Mathf.PerlinNoise(u * 3.1f + 11, v * 3.1f + 7);
                    float mid = Mathf.PerlinNoise(u * 14f + 3, v * 14f + 9);
                    float fiber = Mathf.PerlinNoise(u * 180f, v * 9f + 5) * 0.5f + Mathf.PerlinNoise(u * 9f + 2, v * 160f) * 0.5f;
                    var c = Color.Lerp(new Color(0.72f, 0.6f, 0.4f), new Color(0.9f, 0.82f, 0.64f), big * 0.7f + mid * 0.3f);
                    c *= 0.94f + fiber * 0.1f;
                    // 커피·물 얼룩 (고리 모양)
                    foreach (var st in stains)
                    {
                        float d = Mathf.Sqrt((u - st.x) * (u - st.x) + (v - st.y) * (v - st.y) * 0.8f);
                        float ring = Mathf.Exp(-Mathf.Pow((d - st.z) / 0.006f, 2)) * 0.25f + (d < st.z ? 0.05f : 0);
                        c = Color.Lerp(c, new Color(0.55f, 0.4f, 0.22f), ring);
                    }
                    // 가장자리: 안쪽으로 갈색 그을음, 바깥은 들쭉날쭉하게 찢겨 투명
                    float edge = Mathf.Min(Mathf.Min(u, 1 - u) * w, Mathf.Min(v, 1 - v) * h) / 34f;
                    float ragged = edge + (Mathf.PerlinNoise(u * 22f + 4, v * 22f + 1) - 0.5f) * 0.9f;
                    float burn = Mathf.Clamp01(1 - ragged);
                    c = Color.Lerp(c, new Color(0.32f, 0.2f, 0.1f), burn * 0.85f);
                    c.a = Mathf.Clamp01((ragged - 0.12f) * 6f);
                    // 가운데가 조금 밝게 (빛이 비친 듯)
                    float vig = 1 - Mathf.Pow(Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.55f)) * 1.3f), 2) * 0.22f;
                    c.r *= vig; c.g *= vig; c.b *= vig;
                    px[y * w + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return parchment = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
    }
}
