using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UI용 둥근 사각형(채우기)과 테두리 스프라이트를 코드로 만든다. 9-slice로 늘려 쓴다.
public static class UIStyle
{
    static Sprite fill, frame;

    static Sprite fade;

    public static Sprite Fill => fill != null ? fill : fill = Make(false);
    public static Sprite Frame => frame != null ? frame : frame = Make(true);

    // 왼쪽은 진하고 오른쪽으로 갈수록 투명해지는 그라데이션 (메뉴 배경 가림막·메뉴 버튼 빛)
    public static Sprite HorizontalFade
    {
        get
        {
            if (fade != null) return fade;
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UIFade" };
            for (int x = 0; x < 256; x++)
            {
                float a = 1f - Mathf.SmoothStep(0f, 1f, x / 255f);
                tex.SetPixel(x, 0, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return fade = Sprite.Create(tex, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
        }
    }

    static Sprite ring, dot, vignette;

    // 가운데는 투명하고 가장자리로 갈수록 짙어지는 화면 테두리 (붉은 밤)
    public static Sprite Vignette
    {
        get
        {
            if (vignette != null) return vignette;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UIVignette" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                    float a = Mathf.Clamp01((d - 0.45f) / 0.75f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }

    // 원 조준점: 가는 고리 (64px, 가장자리를 부드럽게)
    public static Sprite Ring => ring != null ? ring : ring = Circle("UIRing", 64, 3.2f);
    // 점 조준점: 꽉 찬 원
    public static Sprite Dot => dot != null ? dot : dot = Circle("UIDot", 32, 0f);

    // thickness가 0이면 꽉 찬 원, 아니면 그 두께의 고리
    static Sprite Circle(string name, int size, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
        var pixels = new Color32[size * size];
        float half = size / 2f, outer = half - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                float a = Mathf.Clamp01(outer - r + 0.5f);
                if (thickness > 0) a *= Mathf.Clamp01(r - (outer - thickness) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    static Sprite Make(bool outline)
    {
        const int size = 32;
        const float radius = 7f, width = 1.6f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = outline ? "UIFrame" : "UIFill",
        };
        var pixels = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 둥근 사각형까지의 거리 (안쪽이 음수)
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float a = outline
                    ? Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + width + 0.5f)
                    : Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
    }
}

// 마우스를 올리면 정해 둔 그래픽들의 색(과 위치·크기)을 부드럽게 바꾼다. 유리 카드·메뉴 버튼이 쓴다
// 코드에서 AddComponent로만 붙인다. 마우스 진입 이벤트는 부모에게도 가므로 안쪽 버튼 위에 있어도 카드는 밝게 남는다
public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    struct Entry
    {
        public Graphic Graphic;
        public Color Normal, Hover;
        public Color Shown; // 마지막으로 칠한 색. 다른 코드가 색을 바꿨는지 알아볼 때 쓴다
    }

    readonly List<Entry> entries = new List<Entry>(2);

    // 누를 수 없는 버튼이면 반응하지 않는다
    public Selectable Gate;
    // 마우스를 올리면 옮길 것 (예: 메뉴 글자를 오른쪽으로 살짝)
    public RectTransform Move;
    public Vector2 MoveFrom, MoveTo;
    // 마우스를 올리면 크기를 바꿀 것 (예: 메뉴 왼쪽 강조 막대)
    public RectTransform Grow;
    public Vector2 GrowFrom, GrowTo;

    float t, target;

    public UIHover Add(Graphic g, Color normal, Color hover)
    {
        var c = Color.Lerp(normal, hover, t);
        g.color = c;
        entries.Add(new Entry { Graphic = g, Normal = normal, Hover = hover, Shown = c });
        return this;
    }

    // 버튼 상태가 바뀌었을 때 색을 다시 정한다 (예: 이어하기 버튼이 켜지고 꺼질 때). 이 그래픽은 다시 이 컴포넌트가 칠한다
    public void SetColors(int index, Color normal, Color hover)
    {
        if (index < 0 || index >= entries.Count) return;
        var e = entries[index];
        e.Normal = normal;
        e.Hover = hover;
        if (e.Graphic != null) e.Shown = e.Graphic.color;
        entries[index] = e;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Gate == null || Gate.IsInteractable()) target = 1;
    }

    public void OnPointerExit(PointerEventData eventData) => target = 0;

    void OnDisable()
    {
        if (t == 0 && target == 0) return;
        t = target = 0;
        Apply();
    }

    void Update()
    {
        if (Gate != null && !Gate.IsInteractable()) target = 0;
        if (t == target) return;
        t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 10f);
        Apply();
    }

    void Apply()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            // 다른 코드가 색을 직접 칠하는 그래픽은 건드리지 않는다 (예: 컷씬 '다음' 버튼 테두리의 깜빡임)
            if (e.Graphic == null || e.Graphic.color != e.Shown) continue;
            e.Shown = Color.Lerp(e.Normal, e.Hover, t);
            e.Graphic.color = e.Shown;
            entries[i] = e;
        }
        float k = Mathf.SmoothStep(0, 1, t);
        if (Move != null) Move.anchoredPosition = Vector2.Lerp(MoveFrom, MoveTo, k);
        if (Grow != null) Grow.sizeDelta = Vector2.Lerp(GrowFrom, GrowTo, k);
    }
}

// 버튼을 만질 때의 움직임: 마우스를 올리면 살짝 떠오르고(1.02배), 누르는 동안 눌려 작아졌다가(0.94배)
// 놓으면 스프링처럼 통 튀며 제자리로 돌아온다. 누르는 순간 바탕에 밝은 빛이 번졌다 사라지고 작은 딸깍 소리가 난다.
// UIManager가 버튼·카드를 만들 때 UIPress.Attach로 붙인다 (누를 수 없는 버튼이면 반응하지 않는다).
// 새로 만든 버튼은 첫 프레임 끝에 제 색을 바로 칠한다: 그러지 않으면 기본색(흰색)에서 0.08초 동안 바뀌며
// 목록을 다시 그릴 때마다(제작대에서 아이템을 누를 때 등) 하얗게 번쩍인다
public class UIPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    const float Stiffness = 600f, Damping = 24f, FlashTime = 0.28f;

    public Selectable Gate;
    public float HoverScale = 1.02f;

    RectTransform rt;
    Image flash;
    Vector3 baseScale;
    float scale = 1f, velocity, flashT = 1f;
    bool down, over, snapped;

    public static UIPress Attach(Selectable s, float hoverScale = 1.02f)
    {
        var p = s.GetComponent<UIPress>();
        if (p == null) p = s.gameObject.AddComponent<UIPress>();
        p.Gate = s;
        p.HoverScale = hoverScale;
        return p;
    }

    void Awake()
    {
        rt = (RectTransform)transform;
        baseScale = rt.localScale;
        pending.Add(this);
        if (!hooked)
        {
            Canvas.willRenderCanvases += SnapPending;
            hooked = true;
        }
        // 누를 때 번지는 빛 (글자·테두리 위에 얇게 덮는다)
        var go = new GameObject("PressFlash", typeof(RectTransform));
        var frt = (RectTransform)go.transform;
        frt.SetParent(rt, false);
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        flash = go.AddComponent<Image>();
        flash.sprite = UIStyle.Fill;
        flash.type = Image.Type.Sliced;
        flash.raycastTarget = false;
        flash.color = new Color(1f, 0.86f, 0.55f, 0f);
    }

    bool Live => Gate == null || Gate.IsInteractable();

    public void OnPointerEnter(PointerEventData e) => over = true;

    public void OnPointerExit(PointerEventData e)
    {
        over = false;
        down = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!Live || e.button != PointerEventData.InputButton.Left) return;
        down = true;
        flashT = 0;
        ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.18f, 1.35f, SoundCategory.UI);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!down) return;
        down = false;
        velocity += 2.4f; // 놓는 순간 위로 튕긴다
    }

    // 만들어진 프레임에 화면을 그리기 직전(Canvas.willRenderCanvases) 버튼 바탕색을
    // 지금 상태(누를 수 없음·마우스 위·보통)의 색으로 바로 칠한다. 새로 만든 버튼의 Start·LateUpdate 시점과 상관없이 첫 화면부터 제 색이다
    static readonly System.Collections.Generic.List<UIPress> pending = new System.Collections.Generic.List<UIPress>();
    static bool hooked;

    static void SnapPending()
    {
        for (int i = 0; i < pending.Count; i++)
            if (pending[i] != null) pending[i].Snap();
        pending.Clear();
    }

    void Snap()
    {
        if (snapped) return;
        snapped = true;
        if (Gate == null || Gate.targetGraphic == null || Gate.transition != Selectable.Transition.ColorTint) return;
        var c = Gate.colors;
        bool hovered = false;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && Gate.IsInteractable())
        {
            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            hovered = RectTransformUtility.RectangleContainsScreenPoint(rt, mouse.position.ReadValue(), cam);
        }
        var target = !Gate.IsInteractable() ? c.disabledColor : hovered ? c.highlightedColor : c.normalColor;
        Gate.targetGraphic.CrossFadeColor(target * c.colorMultiplier, 0f, true, true);
    }

    void OnDisable()
    {
        down = over = false;
        scale = 1f;
        velocity = 0;
        flashT = 1f;
        if (rt != null) rt.localScale = baseScale;
        if (flash != null) flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 0f);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float target = !Live ? 1f : down ? 0.94f : over ? HoverScale : 1f;
        velocity += ((target - scale) * Stiffness - velocity * Damping) * dt;
        scale += velocity * dt;
        if (Mathf.Abs(scale - target) < 0.0005f && Mathf.Abs(velocity) < 0.001f) { scale = target; velocity = 0; }
        rt.localScale = baseScale * scale;

        if (flashT < 1f)
        {
            flashT = Mathf.Min(1f, flashT + dt / FlashTime);
            float a = (1 - flashT) * (1 - flashT) * 0.32f;
            flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, a);
        }
    }
}

// 창이 열릴 때 살짝 작은 크기에서 커지며 서서히 나타난다 (0.16초, 일시정지 중에도 움직인다)
[RequireComponent(typeof(CanvasGroup))]
public class UIPopIn : MonoBehaviour
{
    const float Duration = 0.16f;
    public float FromScale = 0.96f;

    RectTransform rt;
    CanvasGroup group;
    float t;

    void Awake()
    {
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        t = 0;
        Apply();
    }

    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Duration);
        Apply();
    }

    void Apply()
    {
        float k = 1 - (1 - t) * (1 - t) * (1 - t); // 빠르게 시작해 부드럽게 멈춘다
        rt.localScale = Vector3.one * Mathf.Lerp(FromScale, 1f, k);
        group.alpha = k;
    }
}
