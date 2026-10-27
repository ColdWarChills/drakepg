"""Render the UI style rounds (art/ui/styles.html) with headless Edge at 2x and put four styles of a screen side by
side: art/tests/sheets/ui_<sheet>_<screen>.webp (2x2, each style at 1280x720 with its name). Sheets: `styles` (round 1,
A-D), `toon` (round 2, more of C), `bold` (round 2, more of D). One style at full size: open the page itself
(styles.html?screen=bag&style=paper).

    python scripts/ui_styles.py             # every sheet and screen
    python scripts/ui_styles.py toon bag    # some
"""
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
PAGE = ROOT / "art" / "ui" / "styles.html"
SHEETS = ROOT / "art" / "tests" / "sheets"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
FONT = ROOT / "unity" / "Assets" / "Resources" / "Fonts" / "AlegreyaSans-ExtraBold.ttf"
SCREENS = ["bag", "battle"]
SHEETS_OF = {
    "styles": {"paper": "A  Cut paper", "felt": "B  Felt, calmer", "toon": "C  Cartoon card", "bold": "D  Bold graphic"},
    "toon": {"toon": "C1  Cartoon card", "chunky": "C2  Chunky mobile", "bubble": "C3  Soft bubble", "sticker": "C4  Sticker"},
    "bold": {"bold": "D1  Bold graphic", "poster": "D2  Poster, light", "night": "D3  Night and gold", "collage": "D4  Tilted collage"},
}


def render(screen: str, style: str) -> Image.Image:
    with tempfile.TemporaryDirectory() as t:
        shot = Path(t) / "shot.png"
        url = f"{PAGE.as_uri()}?screen={screen}&style={style}"
        subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--allow-file-access-from-files",
                        "--force-device-scale-factor=2", "--window-size=1280,720", "--virtual-time-budget=6000",
                        f"--screenshot={shot}", url], check=True, capture_output=True, timeout=120)
        return Image.open(shot).convert("RGB")


def sheet(name: str, screen: str, shots: dict[str, Image.Image]) -> Path:
    out = Image.new("RGB", (2560, 1440), "#111")
    font = ImageFont.truetype(str(FONT), 30)
    for i, (style, im) in enumerate(shots.items()):
        x, y = (i % 2) * 1280, (i // 2) * 720
        out.paste(im.resize((1280, 720), Image.LANCZOS), (x, y))
        d = ImageDraw.Draw(out)
        label = SHEETS_OF[name][style]
        w = d.textlength(label, font=font)
        d.rectangle([x, y + 720 - 50, x + w + 36, y + 720], fill="#111")
        d.text((x + 18, y + 720 - 44), label, font=font, fill="#fff")
    SHEETS.mkdir(parents=True, exist_ok=True)
    path = SHEETS / f"ui_{name}_{screen}.webp"
    out.save(path, quality=90)
    print(f"  {path.relative_to(ROOT)}")
    return path


def main() -> None:
    args = sys.argv[1:]
    screens = [a for a in args if a in SCREENS] or SCREENS
    names = [a for a in args if a in SHEETS_OF] or list(SHEETS_OF)
    shots: dict[tuple[str, str], Image.Image] = {}
    for name in names:
        for screen in screens:
            for style in SHEETS_OF[name]:
                if (screen, style) not in shots:
                    shots[screen, style] = render(screen, style)
            sheet(name, screen, {s: shots[screen, s] for s in SHEETS_OF[name]})


if __name__ == "__main__":
    main()
