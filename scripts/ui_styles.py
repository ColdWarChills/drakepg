"""Render the UI style round (art/ui/styles.html) with headless Edge at 2x and put the styles of each screen side by
side: art/tests/sheets/ui_styles_<screen>.webp (2x2, each style at 1280x720 with its name). One style at full size: open
the page itself (styles.html?screen=bag&style=paper).

    python scripts/ui_styles.py                 # every screen and style
    python scripts/ui_styles.py bag paper toon  # some
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
STYLES = {"paper": "A  Cut paper", "felt": "B  Felt, calmer", "toon": "C  Cartoon card", "bold": "D  Bold graphic"}


def render(screen: str, style: str) -> Image.Image:
    with tempfile.TemporaryDirectory() as t:
        shot = Path(t) / "shot.png"
        url = f"{PAGE.as_uri()}?screen={screen}&style={style}"
        subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--allow-file-access-from-files",
                        "--force-device-scale-factor=2", "--window-size=1280,720", "--virtual-time-budget=6000",
                        f"--screenshot={shot}", url], check=True, capture_output=True, timeout=120)
        return Image.open(shot).convert("RGB")


def sheet(screen: str, shots: dict[str, Image.Image]) -> Path:
    out = Image.new("RGB", (2560, 1440), "#111")
    font = ImageFont.truetype(str(FONT), 30)
    for i, (style, im) in enumerate(shots.items()):
        x, y = (i % 2) * 1280, (i // 2) * 720
        out.paste(im.resize((1280, 720), Image.LANCZOS), (x, y))
        d = ImageDraw.Draw(out)
        label = STYLES[style]
        w = d.textlength(label, font=font)
        d.rectangle([x, y + 720 - 50, x + w + 36, y + 720], fill="#111")
        d.text((x + 18, y + 720 - 44), label, font=font, fill="#fff")
    SHEETS.mkdir(parents=True, exist_ok=True)
    path = SHEETS / f"ui_styles_{screen}.webp"
    out.save(path, quality=90)
    print(f"  {path.relative_to(ROOT)}")
    return path


def main() -> None:
    args = sys.argv[1:]
    screens = [a for a in args if a in SCREENS] or SCREENS
    styles = [a for a in args if a in STYLES] or list(STYLES)
    for screen in screens:
        sheet(screen, {s: render(screen, s) for s in styles})


if __name__ == "__main__":
    main()
