"""Render the hand-made UI mocks (art/ui/battle_*.html) to pictures with headless Edge, at 2x (2560x1440):
art/tests/sheets/ui_<name>.webp.

    python scripts/ui_mocks.py              # all mocks
    python scripts/ui_mocks.py felt layers  # some
"""
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
UI = ROOT / "art" / "ui"
OUT = ROOT / "art" / "tests" / "sheets"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"


def render(name: str) -> Path:
    html = UI / f"battle_{name}.html"
    with tempfile.TemporaryDirectory() as t:
        shot = Path(t) / "shot.png"
        subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--allow-file-access-from-files",
                        "--force-device-scale-factor=2", "--window-size=1280,720", "--virtual-time-budget=4000",
                        f"--screenshot={shot}", html.as_uri()], check=True, capture_output=True, timeout=120)
        OUT.mkdir(parents=True, exist_ok=True)
        out = OUT / f"ui_{name}.webp"
        Image.open(shot).convert("RGB").save(out, quality=90)
    print(f"  {out.relative_to(ROOT)}")
    return out


def main() -> None:
    names = sys.argv[1:] or [p.stem.removeprefix("battle_") for p in sorted(UI.glob("battle_*.html"))]
    for n in names:
        render(n)


if __name__ == "__main__":
    main()
