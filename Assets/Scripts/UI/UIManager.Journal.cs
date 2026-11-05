using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 탐색 결과: 방공호로 돌아오면 낡은 양피지 한 장에 그날의 탐색 결과를 손으로 적어 내려간다 (일기가 아니라 결과 보고서).
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
        AddJournalLine($"탐색 결과  —  {r.Turn}번째 날", 40, Ink, FontBold, 6, -1.2f);
        string back = r.RedNight ? $"시간을 {Mathf.CeilToInt(r.RedNightSeconds)}초나 넘기고 겨우 해치로 돌아왔다."
            : $"{(int)r.TimeLeft / 60}분 {(int)r.TimeLeft % 60}초를 남기고 살아 돌아왔다.";
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
            if (r.DogsKilled > 0) kills.Add($"좀비개 {r.DogsKilled}마리");
            AddJournalLine("  쓰러뜨린 것:  " + string.Join(", ", kills), 23, Ink, Font, 4, -0.4f);
        }
        float lost = r.HealthBefore - r.HealthAfter;
        AddJournalLine($"  몸 상태:  체력 {r.HealthBefore:0} → {r.HealthAfter:0}" + (lost > 0.5f ? $"  (-{lost:0})" : "  (다치지 않았다)"), 23, lost > 30 ? InkRed : Ink, Font, 4, 0.5f);
        AddJournalRule();

        // 마무리 한 줄
        string closing;
        if (r.RedNight) closing = "다시는 이렇게 늦지 말자. 밖은 시간이 갈수록 위험해진다.";
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
        journalHint.text = "— 아무 키나 눌러 계속";
    }

    public void CloseJournal()
    {
        if (!IsJournalOpen) return;
        journalRoot.SetActive(false);
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.5f, 0.8f, SoundCategory.UI);
    }

    // 매 프레임: 종이가 살짝 떠오르며 나타나고, 한 글자씩 적히며 연필 사각사각 소리
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
        // 적는 동안 연필 긁는 소리를 한 획씩 끊어 낸다 (획 길이·세기·높낮이가 조금씩 다르게)
        journalScratchTimer -= dt;
        if (journalLine < journalTexts.Count && journalScratchTimer <= 0 && journalFull[journalLine].Length > 0)
        {
            journalScratchTimer = Random.Range(0.09f, 0.16f);
            ProceduralAudio.Play2D(PencilScratch(), Random.Range(0.3f, 0.45f), Random.Range(0.9f, 1.2f), SoundCategory.UI);
        }
        if (journalLine >= journalTexts.Count) FinishJournal();
    }

    // ---------- 연필 소리 ----------
    // 연필이 종이를 사각사각 긁는 소리 한 획 (0.17초): 흑연 알갱이가 종이 결에 걸리는 잔 소리(3.4kHz 띠)와 손이 눌러 미는 종이 마찰(800Hz 띠),
    // 아주 옅은 높은 쉿 소리. 획은 10ms 만에 눌리고 끝 40%에서 들리며 손 떨림(45Hz)으로 세기가 조금씩 흔들린다.
    // 펜 소리(ProceduralAudio.PenScratch)보다 낮고 부드럽고 거칠다. 한 번 만들어 두고 다시 쓴다
    static AudioClip pencilClip;

    static AudioClip PencilScratch()
    {
        if (pencilClip != null) return pencilClip;
        const int sr = 44100;
        const float seconds = 0.17f;
        int n = (int)(sr * seconds);
        var data = new float[n];
        var rng = new System.Random(4242);
        var grain = new BandPass2(3400f / sr, 0.9f);
        var paper = new BandPass2(800f / sr, 1.3f);
        var hiss = new BandPass2(6500f / sr, 1.0f);
        float g = 0, pressure = 0.7f, max = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float u = t / seconds;
            float env = Mathf.Min(1f, t / 0.01f) * (u < 0.6f ? 1f : Mathf.Clamp01((1f - u) / 0.4f));
            env *= 0.82f + 0.18f * Mathf.Sin(t * 2 * Mathf.PI * 45f);
            // 종이 결에 걸리는 알갱이: 짧게 터졌다 가라앉는다
            if (rng.NextDouble() < 0.03) g = 0.4f + (float)rng.NextDouble() * 0.6f;
            g *= 0.93f;
            if (rng.NextDouble() < 0.002) pressure = 0.5f + (float)rng.NextDouble() * 0.5f;
            float w1 = (float)(rng.NextDouble() * 2 - 1), w2 = (float)(rng.NextDouble() * 2 - 1), w3 = (float)(rng.NextDouble() * 2 - 1);
            float s = grain.Process(w1) * (0.5f + g) * 0.9f + paper.Process(w2) * 0.6f * pressure + hiss.Process(w3) * 0.12f * g;
            data[i] = s * env;
            max = Mathf.Max(max, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < n; i++) data[i] = data[i] / max * 0.5f;
        pencilClip = AudioClip.Create("pencilscratch", n, 1, sr, false);
        pencilClip.SetData(data, 0);
        return pencilClip;
    }

    // 2차 띠통과 필터 (RBJ 공식). f는 샘플레이트에 대한 비율
    class BandPass2
    {
        readonly float b0, b2, a1, a2;
        float x1, x2, y1, y2;

        public BandPass2(float f, float q)
        {
            float w = 2 * Mathf.PI * f, cos = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2 * q);
            float a0 = 1 + alpha;
            b0 = alpha / a0;
            b2 = -alpha / a0;
            a1 = -2 * cos / a0;
            a2 = (1 - alpha) / a0;
        }

        public float Process(float x)
        {
            float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    // 낡은 두루마리 양피지 (참고 사진처럼): 누런 바탕에 섬유 결과 구김, 번진 갈색 얼룩과 물 자국 고리,
    // 위·아래는 말린 봉(원통 그늘), 양옆은 그을려 검게 타들어 간 들쭉날쭉한 가장자리와 작은 구멍.
    // 글이 놓이는 가운데는 밝게 두어 읽기 쉽다
    static Sprite Parchment
    {
        get
        {
            if (parchment != null) return parchment;
            const int w = 512, h = 600;
            const float RollH = 34f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Parchment" };
            var px = new Color[w * h];
            var rng = new System.Random(77);
            // 번진 얼룩 (x, y, 반지름, 세기)과 물 자국 고리 (x, y, 반지름)
            var blots = new Vector4[7];
            for (int i = 0; i < blots.Length; i++)
                blots[i] = new Vector4(0.08f + 0.84f * (float)rng.NextDouble(), 0.08f + 0.84f * (float)rng.NextDouble(), 0.05f + 0.1f * (float)rng.NextDouble(), 0.18f + 0.22f * (float)rng.NextDouble());
            var rings = new Vector3[3];
            for (int i = 0; i < rings.Length; i++) rings[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), 0.06f + 0.08f * (float)rng.NextDouble());
            var ink = new Color(0.1f, 0.06f, 0.03f);
            var burnt = new Color(0.3f, 0.17f, 0.07f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float big = Mathf.PerlinNoise(u * 2.6f + 11, v * 2.2f + 7);
                    float mid = Mathf.PerlinNoise(u * 9f + 3, v * 9f + 9);
                    float fine = Mathf.PerlinNoise(u * 40f + 1, v * 40f + 2);
                    float fiber = Mathf.PerlinNoise(u * 190f, v * 7f + 5) * 0.5f + Mathf.PerlinNoise(u * 7f + 2, v * 170f) * 0.5f;
                    var c = Color.Lerp(new Color(0.74f, 0.62f, 0.42f), new Color(0.91f, 0.84f, 0.67f), big * 0.55f + mid * 0.3f + fine * 0.15f);
                    c *= 0.93f + fiber * 0.12f;
                    // 번진 얼룩: 가장자리가 더 진한 갈색, 모양은 노이즈로 일그러뜨린다
                    foreach (var b in blots)
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(b.x, b.y)) + (Mathf.PerlinNoise(u * 18f + b.x * 10, v * 18f + b.y * 10) - 0.5f) * 0.05f;
                        float inside = Mathf.Clamp01((b.z - d) / (b.z * 0.45f));
                        float rim = Mathf.Exp(-Mathf.Pow((d - b.z) / 0.009f, 2));
                        c = Color.Lerp(c, new Color(0.5f, 0.36f, 0.19f), inside * b.w);
                        c = Color.Lerp(c, new Color(0.4f, 0.27f, 0.13f), rim * b.w * 0.8f);
                    }
                    foreach (var st in rings)
                    {
                        float d = Mathf.Sqrt((u - st.x) * (u - st.x) + (v - st.y) * (v - st.y) * 0.8f);
                        float ring = Mathf.Exp(-Mathf.Pow((d - st.z) / 0.006f, 2)) * 0.22f + (d < st.z ? 0.04f : 0);
                        c = Color.Lerp(c, new Color(0.55f, 0.4f, 0.22f), ring);
                    }
                    // 구김: 가는 주름 선 (밝은 선 옆에 어두운 선)
                    float crease = Mathf.Abs(Mathf.PerlinNoise(u * 6f + 20, v * 6f + 30) - 0.5f);
                    if (crease < 0.01f) c *= 1.07f; else if (crease < 0.02f) c *= 0.92f;
                    float crease2 = Mathf.Abs(Mathf.PerlinNoise(u * 12f + 40, v * 4f + 50) - 0.5f);
                    if (crease2 < 0.007f) c *= 1.05f; else if (crease2 < 0.014f) c *= 0.94f;

                    // 양옆: 들쭉날쭉하게 타들어 간 가장자리 (바깥 20px쯤은 진한 갈색→검정, 끝은 투명)
                    float edgeLR = Mathf.Min(u, 1 - u) * w;
                    float rag = (Mathf.PerlinNoise(u * 16f + 4, v * 16f + 1) - 0.5f) * 24f + (Mathf.PerlinNoise(u * 60f + 9, v * 60f + 3) - 0.5f) * 7f;
                    float e = edgeLR + rag;
                    float burn = Mathf.Clamp01(1 - e / 26f);
                    c = Color.Lerp(c, burnt, burn * burn * 0.8f);
                    c = Color.Lerp(c, ink, Mathf.Clamp01(1 - e / 7f) * 0.9f);
                    float alpha = Mathf.Clamp01(e / 1.5f);
                    // 가장자리 근처의 작은 구멍
                    float hole = Mathf.PerlinNoise(u * 34f + 70, v * 34f + 80);
                    if (hole > 0.84f && edgeLR < 80f) alpha *= Mathf.Clamp01((0.9f - hole) / 0.06f);

                    // 위·아래: 말린 봉 (원통 그늘, 안쪽 끝에 그림자 선)
                    float edgeTB = Mathf.Min(v, 1 - v) * h;
                    if (edgeTB < RollH)
                    {
                        float s = edgeTB / RollH;
                        float shade = 0.5f + 0.62f * Mathf.Pow(Mathf.Sin(s * Mathf.PI), 0.8f);
                        c = new Color(c.r * shade * 0.92f, c.g * shade * 0.86f, c.b * shade * 0.78f);
                        c = Color.Lerp(c, ink, Mathf.Clamp01(1 - edgeTB / 2.5f) * 0.8f);
                    }
                    else if (edgeTB < RollH + 6f) c *= 0.62f + 0.38f * ((edgeTB - RollH) / 6f);

                    // 가운데가 밝게 (글이 놓이는 자리)
                    float vig = 1 - Mathf.Pow(Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.52f)) * 1.25f), 2) * 0.2f;
                    c = new Color(Mathf.Clamp01(c.r * vig), Mathf.Clamp01(c.g * vig), Mathf.Clamp01(c.b * vig), alpha);
                    px[y * w + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return parchment = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
    }
}
