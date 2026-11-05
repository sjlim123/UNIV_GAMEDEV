using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 적 이름·체력바 (2026-11-05, 1차): 시야 안 20m 이내의 좀비·약탈자·좀비개 머리 위에 작은 유리판 —
// 이름, 붉은 체력 막대(깎이면 밝은 자국이 잠깐 남았다가 따라 줄고 끝이 붉게 빛난다), 남은 체력 숫자.
// 멀수록 작고 옅어지며, 벽에 가리거나 시야 밖이면 사라진다. 죽으면 막대가 비고 곧 사라진다.
// 체력·이름은 Zombie/Animal의 공개 속성(Health·MaxHealth·DisplayName)으로 읽는다
public partial class UIManager
{
    class EnemyBar
    {
        public Component Target;
        public Transform Tr, Head;
        public float HeadOffset;
        public RectTransform Rect, Fill, Chip;
        public CanvasGroup Group;
        public UnityEngine.UI.Text Name, Value;
        public Image FillImage, Glow;
        public float Shown = -1, ChipValue, ChipDelay, Alpha, HitGlow, Hidden, BlockedFor;
        public int Number = int.MinValue;
    }

    const float EnemyBarRange = 20f, EnemyBarW = 176f, EnemyBarH = 46f;
    static readonly Color EnemyBarRed = new Color(0.72f, 0.1f, 0.08f);
    static readonly Color EnemyBarBright = new Color(0.98f, 0.45f, 0.3f);
    RectTransform enemyBarRoot;
    readonly List<EnemyBar> enemyBars = new List<EnemyBar>();
    float enemyScanTimer;

    void BuildEnemyBars(Transform h)
    {
        enemyBarRoot = NewRect("EnemyBars", h);
        Stretch(enemyBarRoot);
        enemyBarRoot.SetAsFirstSibling(); // 다른 HUD 요소 아래에 그린다
    }

    // 0.5초마다 씬의 적을 훑어 목록을 맞춘다 (막대 그림은 처음 보일 때 만든다)
    void ScanEnemies()
    {
        foreach (var z in FindObjectsByType<Zombie>(FindObjectsSortMode.None)) TrackEnemy(z, z.head, z.head != null ? 0.38f : 1.95f);
        for (int i = 0; i < Animal.All.Count; i++)
            if (Animal.All[i] != null) TrackEnemy(Animal.All[i], null, 1.15f);
        for (int i = enemyBars.Count - 1; i >= 0; i--)
            if (enemyBars[i].Target == null)
            {
                if (enemyBars[i].Rect != null) Destroy(enemyBars[i].Rect.gameObject);
                enemyBars.RemoveAt(i);
            }
    }

    void TrackEnemy(Component c, Transform head, float offset)
    {
        for (int i = 0; i < enemyBars.Count; i++)
            if (enemyBars[i].Target == c) return;
        enemyBars.Add(new EnemyBar { Target = c, Tr = c.transform, Head = head, HeadOffset = offset });
    }

    // 체력·최대 체력·이름·죽음 여부를 읽는다. 적이 아니면 false
    static bool ReadEnemy(Component c, out float hp, out float max, out string name, out bool dead)
    {
        hp = 0; max = 1; name = null; dead = true;
        var z = c as Zombie;
        if (z != null)
        {
            hp = z.Health;
            max = z.MaxHealth > 0 ? z.MaxHealth : 100f;
            name = string.IsNullOrEmpty(z.DisplayName) ? "좀비" : z.DisplayName;
            dead = z.IsDead;
            return true;
        }
        var a = c as Animal;
        if (a != null)
        {
            hp = a.Health;
            max = a.MaxHealth > 0 ? a.MaxHealth : 80f;
            name = string.IsNullOrEmpty(a.DisplayName) ? "좀비개" : a.DisplayName;
            dead = a.IsDead;
            return true;
        }
        return false;
    }

    // 막대 그림: 페더 유리판 → 이름(왼쪽 위)·숫자(오른쪽 위) → 아래 홈 안에 깎인 자국·붉은 막대·막대 끝 빛
    void MakeEnemyBarUI(EnemyBar b)
    {
        b.Rect = NewRect("EnemyBar", enemyBarRoot);
        b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        b.Rect.pivot = new Vector2(0.5f, 0f);
        b.Rect.sizeDelta = new Vector2(EnemyBarW, EnemyBarH);
        b.Group = b.Rect.gameObject.AddComponent<CanvasGroup>();
        b.Group.alpha = 0;
        b.Group.blocksRaycasts = false;
        b.Group.interactable = false;

        var glass = NewRect("Glass", b.Rect).gameObject.AddComponent<Image>();
        Stretch(glass.rectTransform, -14, -12);
        glass.sprite = UIStyle.GlassSoft;
        glass.type = Image.Type.Sliced;
        glass.pixelsPerUnitMultiplier = 3.2f; // 페더 12px (작은 판이라 페더가 넓으면 판이 거의 안 보인다)
        glass.material = GlassMaterial();
        glass.color = HudGlassTint;
        glass.raycastTarget = false;

        b.Name = NewText(b.Rect, "", 16, TextAnchor.UpperLeft, Pale, FontTitle);
        Place(b.Name.rectTransform, new Vector2(0, 1), new Vector2(10, -5), new Vector2(110, 20));
        b.Name.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
        b.Value = NewText(b.Rect, "", 13, TextAnchor.UpperRight, Pale, FontMono);
        Place(b.Value.rectTransform, new Vector2(1, 1), new Vector2(-10, -7), new Vector2(80, 18));

        var track = NewRect("Track", b.Rect);
        track.anchorMin = new Vector2(0, 0);
        track.anchorMax = new Vector2(1, 0);
        track.pivot = new Vector2(0.5f, 0);
        track.anchoredPosition = new Vector2(0, 9);
        track.sizeDelta = new Vector2(-20, 8);
        var ti = track.gameObject.AddComponent<Image>();
        ti.sprite = UIStyle.Fill;
        ti.type = Image.Type.Sliced;
        ti.pixelsPerUnitMultiplier = 2.5f;
        ti.color = new Color(0.02f, 0.02f, 0.02f, 0.8f);
        ti.raycastTarget = false;

        b.Chip = NewRect("Chip", track);
        b.Chip.anchorMin = Vector2.zero;
        b.Chip.anchorMax = new Vector2(1, 1);
        b.Chip.offsetMin = new Vector2(1, 1);
        b.Chip.offsetMax = new Vector2(-1, -1);
        var ci = b.Chip.gameObject.AddComponent<Image>();
        ci.sprite = UIStyle.Fill;
        ci.type = Image.Type.Sliced;
        ci.pixelsPerUnitMultiplier = 2.5f;
        ci.color = new Color(1f, 0.86f, 0.75f, 0.75f);
        ci.raycastTarget = false;

        b.Fill = NewRect("Fill", track);
        b.Fill.anchorMin = Vector2.zero;
        b.Fill.anchorMax = new Vector2(1, 1);
        b.Fill.offsetMin = new Vector2(1, 1);
        b.Fill.offsetMax = new Vector2(-1, -1);
        b.FillImage = b.Fill.gameObject.AddComponent<Image>();
        b.FillImage.sprite = UIStyle.Fill;
        b.FillImage.type = Image.Type.Sliced;
        b.FillImage.pixelsPerUnitMultiplier = 2.5f;
        b.FillImage.color = EnemyBarRed;
        b.FillImage.raycastTarget = false;

        b.Glow = NewRect("Glow", b.Fill).gameObject.AddComponent<Image>();
        var gr = b.Glow.rectTransform;
        gr.anchorMin = gr.anchorMax = new Vector2(1, 0.5f);
        gr.pivot = new Vector2(0.5f, 0.5f);
        gr.sizeDelta = new Vector2(22, 22);
        b.Glow.sprite = UIStyle.Glow;
        b.Glow.color = new Color(1f, 0.4f, 0.25f, 0.5f);
        b.Glow.raycastTarget = false;
    }

    void UpdateEnemyBars()
    {
        var cam = Camera.main;
        if (enemyBarRoot == null || cam == null) return;
        float dt = Time.unscaledDeltaTime;
        enemyScanTimer -= dt;
        if (enemyScanTimer <= 0)
        {
            enemyScanTimer = 0.5f;
            ScanEnemies();
        }
        Vector3 camPos = cam.transform.position;
        Vector3 camFwd = cam.transform.forward;
        for (int i = 0; i < enemyBars.Count; i++)
        {
            var b = enemyBars[i];
            bool visible = false;
            float dist = 0, hp = 0, max = 1;
            string name = null;
            bool dead = true;
            if (b.Target != null && b.Tr != null && ReadEnemy(b.Target, out hp, out max, out name, out dead) && !dead)
            {
                Vector3 anchor = (b.Head != null ? b.Head.position : b.Tr.position) + Vector3.up * b.HeadOffset;
                Vector3 to = anchor - camPos;
                dist = to.magnitude;
                if (dist < EnemyBarRange && Vector3.Dot(camFwd, to) > 0)
                {
                    var sp = cam.WorldToScreenPoint(anchor);
                    if (sp.z > 0 && sp.x > -40 && sp.x < Screen.width + 40 && sp.y > -40 && sp.y < Screen.height + 40)
                    {
                        // 벽에 가렸으면 보이지 않는다: 가슴 높이로 선을 긋고, 적 자신에 걸리거나 적 바로 앞(0.7m)에서 끊긴 것은 가림이 아니다.
                        // 문틀·기둥에 잠깐 스치는 정도로 깜빡이지 않게 0.3초 넘게 계속 가려야 사라진다
                        Vector3 chest = b.Tr.position + Vector3.up * (b.Head != null ? 1.2f : 0.6f);
                        RaycastHit hit;
                        bool hitSomething = Physics.Linecast(camPos, chest, out hit, ~0, QueryTriggerInteraction.Ignore);
                        bool blockedNow = hitSomething && !hit.transform.IsChildOf(b.Tr) && !b.Tr.IsChildOf(hit.transform) && Vector3.Distance(hit.point, chest) > 0.7f;
                        b.BlockedFor = blockedNow ? b.BlockedFor + dt : 0f;
                        if (b.BlockedFor < 0.3f)
                        {
                            visible = true;
                            if (b.Rect == null) MakeEnemyBarUI(b);
                            Vector2 local;
                            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(sp.x, sp.y), null, out local);
                            b.Rect.anchoredPosition = local + new Vector2(0, 4);
                            float near = Mathf.InverseLerp(4f, EnemyBarRange, dist);
                            b.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, near);
                        }
                    }
                }
            }
            if (b.Rect == null) continue;
            if (visible)
            {
                b.Hidden = 0;
                b.Name.text = name;
                UpdateEnemyBarFill(b, hp, max, dt);
            }
            else b.Hidden += dt;
            // 보일 때는 빨리 나타나고, 가리거나 죽으면 천천히 사라진다. 멀수록 옅다
            b.Alpha = Mathf.MoveTowards(b.Alpha, visible ? 1f : 0f, dt * (visible ? 6f : 2.5f));
            float distFade = visible ? Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(10f, EnemyBarRange, dist)) : 1f;
            b.Group.alpha = b.Alpha * distFade;
            bool active = b.Alpha > 0;
            if (b.Rect.gameObject.activeSelf != active) b.Rect.gameObject.SetActive(active);
            // 오래 안 보이면 그림을 지운다 (다시 보이면 새로 만든다)
            if (!active && b.Hidden > 8f)
            {
                Destroy(b.Rect.gameObject);
                b.Rect = null;
                b.Shown = -1;
                b.Number = int.MinValue;
            }
        }
    }

    // 막대 하나: 부드럽게 따라가고, 깎이면 밝은 자국이 잠깐 머물다 따라 줄고 끝이 번쩍이며, 25% 아래면 붉게 고동친다
    void UpdateEnemyBarFill(EnemyBar b, float hp, float max, float dt)
    {
        float target = Mathf.Clamp01(hp / Mathf.Max(1f, max));
        if (b.Shown < 0) { b.Shown = b.ChipValue = target; }
        if (target < b.Shown - 0.0001f)
        {
            b.ChipDelay = 0.4f;
            b.ChipValue = Mathf.Max(b.ChipValue, b.Shown);
            b.HitGlow = 1f;
        }
        b.Shown = Mathf.MoveTowards(b.Shown, target, dt * Mathf.Max(0.8f, Mathf.Abs(target - b.Shown) * 10f));
        if (b.ChipDelay > 0) b.ChipDelay -= dt;
        else b.ChipValue = Mathf.MoveTowards(b.ChipValue, b.Shown, dt * 0.8f);
        b.ChipValue = Mathf.Max(b.ChipValue, b.Shown);
        b.Fill.anchorMax = new Vector2(Mathf.Max(0.0001f, b.Shown), 1);
        b.Chip.anchorMax = new Vector2(Mathf.Max(0.0001f, b.ChipValue), 1);

        b.HitGlow = Mathf.MoveTowards(b.HitGlow, 0, dt * 2.2f);
        bool low = target < 0.25f;
        float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f) : 0f;
        var c = Color.Lerp(EnemyBarRed, EnemyBarBright, Mathf.Max(b.HitGlow * 0.7f, pulse * 0.45f));
        b.FillImage.color = c;
        b.Glow.color = new Color(1f, 0.4f, 0.25f, 0.35f + 0.55f * Mathf.Max(b.HitGlow, pulse * 0.6f));
        b.Glow.rectTransform.localScale = Vector3.one * (1f + b.HitGlow * 0.6f);

        int n = Mathf.CeilToInt(Mathf.Max(0, hp));
        if (n != b.Number)
        {
            b.Number = n;
            b.Value.text = n + " / " + Mathf.RoundToInt(max);
        }
    }
}
