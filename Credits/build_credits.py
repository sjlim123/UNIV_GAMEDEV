# 외부_에셋_목록.json(원본) → 외부_에셋_목록.md / 외부_에셋_목록.pdf / Assets/ThirdParty/CREDITS.md
# 사용: 리포지토리 루트에서  python Credits/build_credits.py
# PDF는 reportlab이 필요하다 (pip install reportlab). 한글은 Assets/Resources/Fonts/Pretendard-Regular.ttf로 그린다.
import io, json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, "외부_에셋_목록.json")
MD = os.path.join(HERE, "외부_에셋_목록.md")
PDF = os.path.join(HERE, "외부_에셋_목록.pdf")
ASSETS_MD = os.path.join(ROOT, "Assets", "ThirdParty", "CREDITS.md")
FONT = os.path.join(ROOT, "Assets", "Resources", "Fonts", "Pretendard-Regular.ttf")
FONT_BOLD = os.path.join(ROOT, "Assets", "Resources", "Fonts", "Pretendard-Bold.ttf")

with io.open(SRC, encoding="utf-8") as f:
    data = json.load(f)

licenses = data["licenses"]
assets = data["assets"]


def lic_name(key):
    return licenses.get(key, {}).get("name", key)


# ---------- Markdown ----------
def build_md():
    out = []
    out.append("# %s\n" % data["title"])
    out.append("갱신: %s · 항목 %d개\n" % (data["updated"], len(assets)))
    out.append("> %s\n" % data["note"])
    out.append("## 라이선스 요약\n")
    out.append("| 약칭 | 이름 | 뜻 | 원문 |")
    out.append("|---|---|---|---|")
    for k, v in licenses.items():
        out.append("| %s | %s | %s | %s |" % (k, v["name"], v["summary"], v["url"]))
    out.append("")
    out.append("## 에셋 목록\n")
    out.append("| # | 종류 | 에셋 | 만든 곳 / 사람 | 라이선스 | 위치 | 쓰는 곳 | 추가일 | 상태 |")
    out.append("|---|---|---|---|---|---|---|---|---|")
    for i, a in enumerate(assets, 1):
        name = a["name"]
        if a.get("items"):
            name += "<br><small>" + ", ".join(a["items"]) + "</small>"
        out.append("| %d | %s | %s | %s | %s | `%s` | %s | %s | %s |" % (
            i, a["kind"], name, a["author"], a["license"], a["location"], a["used_for"], a["added"], a["status"]))
    out.append("")
    out.append("## 출처 링크\n")
    for a in assets:
        out.append("- %s — %s" % (a["name"], a["source"]))
    out.append("")
    out.append("## 메모\n")
    out.append("- 빗소리·바람·발소리 등 나머지 효과음과 UI 스프라이트·캐릭터·아이템 모델은 전부 코드로 만든다 (`Scripts/Audio/ProceduralAudio.cs`, `Scripts/UI/UIStyle.cs`, `Scripts/Characters/ProcMesh.cs` 등).")
    out.append("- 좀비 소리는 위 원본을 잘라 크기를 맞춘 것이다 (메뉴 Tools → 핵폭발 생존 → 좀비 소리 만들기).")
    out.append("- 같은 내용의 JSON·PDF: `Credits/외부_에셋_목록.json`, `Credits/외부_에셋_목록.pdf`")
    return "\n".join(out) + "\n"


md = build_md()
for path in (MD, ASSETS_MD):
    with io.open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(md)
print("md:", MD, "and", ASSETS_MD)

# ---------- PDF ----------
try:
    from reportlab.lib import colors
    from reportlab.lib.pagesizes import A4, landscape
    from reportlab.lib.styles import ParagraphStyle
    from reportlab.lib.units import mm
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle
except ImportError:
    print("reportlab이 없어 PDF는 건너뜀 (pip install reportlab)")
    sys.exit(0)

pdfmetrics.registerFont(TTFont("Pretendard", FONT))
pdfmetrics.registerFont(TTFont("PretendardB", FONT_BOLD))
body = ParagraphStyle("body", fontName="Pretendard", fontSize=8.2, leading=11, wordWrap="CJK")
small = ParagraphStyle("small", parent=body, fontSize=7, leading=9, textColor=colors.HexColor("#555555"))
h1 = ParagraphStyle("h1", fontName="PretendardB", fontSize=16, leading=20, spaceAfter=4)
h2 = ParagraphStyle("h2", fontName="PretendardB", fontSize=11, leading=14, spaceBefore=8, spaceAfter=4)
head = ParagraphStyle("head", fontName="PretendardB", fontSize=8.2, leading=11, textColor=colors.white)


def P(text, style=body):
    return Paragraph(text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"), style)


doc = SimpleDocTemplate(PDF, pagesize=landscape(A4), leftMargin=12 * mm, rightMargin=12 * mm, topMargin=12 * mm, bottomMargin=12 * mm,
                        title=data["title"], author="Team_31")
story = [P(data["title"], h1), P("갱신 %s · 항목 %d개" % (data["updated"], len(assets)), small), Spacer(1, 4), P(data["note"], small), Spacer(1, 6)]

story.append(P("라이선스 요약", h2))
rows = [[P("약칭", head), P("이름", head), P("뜻", head), P("원문", head)]]
for k, v in licenses.items():
    rows.append([P(k), P(v["name"]), P(v["summary"]), P(v["url"], small)])
t = Table(rows, colWidths=[18 * mm, 70 * mm, 110 * mm, 70 * mm], repeatRows=1)
t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#2A2622")), ("GRID", (0, 0), (-1, -1), 0.3, colors.HexColor("#999999")),
                       ("VALIGN", (0, 0), (-1, -1), "TOP"), ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F3EFE6")])]))
story.append(t)

story.append(P("에셋 목록", h2))
rows = [[P(x, head) for x in ("#", "종류", "에셋", "만든 곳 / 사람", "라이선스", "위치", "쓰는 곳", "추가일", "상태")]]
for i, a in enumerate(assets, 1):
    name = a["name"]
    if a.get("items"):
        name += "<br/><font size='6.5' color='#555555'>" + ", ".join(a["items"]) + "</font>"
    rows.append([P(str(i)), P(a["kind"]), Paragraph(name.replace("&", "&amp;"), body), P(a["author"]), P(lic_name(a["license"])),
                 P(a["location"], small), P(a["used_for"]), P(a["added"]), P(a["status"])])
t = Table(rows, colWidths=[7 * mm, 18 * mm, 62 * mm, 36 * mm, 34 * mm, 52 * mm, 34 * mm, 17 * mm, 13 * mm], repeatRows=1)
t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#2A2622")), ("GRID", (0, 0), (-1, -1), 0.3, colors.HexColor("#999999")),
                       ("VALIGN", (0, 0), (-1, -1), "TOP"), ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F3EFE6")])]))
story.append(t)

story.append(P("출처 링크", h2))
for a in assets:
    story.append(P("• %s — %s" % (a["name"], a["source"]), small))
story.append(P("메모", h2))
story.append(P("• 빗소리·바람·발소리 등 나머지 효과음과 UI 스프라이트·캐릭터·아이템 모델은 전부 코드로 만든다.", small))
story.append(P("• 같은 내용: Credits/외부_에셋_목록.json (원본), Credits/외부_에셋_목록.md, Assets/ThirdParty/CREDITS.md", small))
doc.build(story)
print("pdf:", PDF)
