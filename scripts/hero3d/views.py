"""Qwen edits of the base hero into views for image-to-3D (front A-pose, side profile, back)."""
import io, os, sys, time
from pathlib import Path
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from qwen_batch import Comfy

OUT = Path(sys.argv[1]); OUT.mkdir(parents=True, exist_ok=True)
names = sys.argv[2].split(",")
seeds = [int(s) for s in sys.argv[3].split(",")]
KEEP = ("Keep his face, messy dark brown hair, red headband with its two tails, cream shirt with rolled-up sleeves, "
        "brown belt with a square buckle, dark green trousers and tall brown boots exactly the same, in exactly the same "
        "clean cartoon art style, outlines and colours. Full body from the top of his hair to the soles of his boots, "
        "centred, the same size. Plain pure white background, even flat light, no shadow on the ground.")
P = {
 "front": "Edit <image1>: turn the same young man to face the viewer straight on, a front view. He stands upright in a "
          "relaxed A-pose: both arms straight and held out about 35 degrees away from his body, both hands closed in "
          "fists, feet a little apart with the toes pointing forward, face looking straight at the viewer. " + KEEP,
 "side":  "Edit <image1>: show the same young man from exactly his side, a clean side profile facing right. He stands "
          "upright, both arms hanging straight down a little in front of and behind his body, hands closed in fists, "
          "feet a little apart, looking straight ahead to the right. " + KEEP,
 "back":  "Edit <image1>: show the same young man from straight behind, a back view: we see the back of his head, "
          "hair and the knot of his red headband, the back of his shirt and trousers and the heels of his boots. He "
          "stands upright in a relaxed A-pose: both arms straight and held out about 35 degrees away from his body, "
          "hands closed in fists, feet a little apart. " + KEEP,
}
img = Image.open(Path(__file__).resolve().parents[2] / "art/raw/char_hero_bare.png").convert("RGBA")
bg = Image.new("RGBA", img.size, (255, 255, 255, 255)); bg.alpha_composite(img)
buf = io.BytesIO(); bg.convert("RGB").save(buf, "PNG")
c = Comfy("http://127.0.0.1:8188"); c.check()
ref = c.upload("drakepg_hero3d_base.png", buf.getvalue())
for n in names:
    for s in seeds:
        t = time.time()
        data = c.generate(P[n], [ref], None, s, 30)
        (OUT / f"{n}_s{s}.png").write_bytes(data)
        print(n, s, round(time.time() - t), "s", flush=True)
