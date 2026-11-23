"""Save a finished ComfyUI run whose Python caller was stopped: its picture from ComfyUI's history.

<venv python> -I -B fetch.py <prompt id> <name>     -> art/redo/moments/<name>.png (never overwrites), runs.jsonl
"""
import json
import sys
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import HOST, REDO  # noqa: E402

pid, name = sys.argv[1], sys.argv[2]
out = REDO / f"{name}.png"
if out.exists():
    sys.exit(f"{out.name} exists")
hist = json.load(urllib.request.urlopen(f"{HOST}/history/{pid}"))
if pid not in hist:
    sys.exit("not finished yet")
imgs = [im for o in hist[pid]["outputs"].values() for im in o.get("images", [])]
q = urllib.parse.urlencode({k: imgs[0][k] for k in ("filename", "subfolder", "type")})
out.write_bytes(urllib.request.urlopen(f"{HOST}/view?{q}").read())
prompt = [v["inputs"]["prompt"] for v in hist[pid]["prompt"][2].values() if "prompt" in v.get("inputs", {})]
with open(REDO / "runs.jsonl", "a") as f:
    f.write(json.dumps({"file": out.name, "sec": None, "utc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
                        "prompt": prompt[0] if prompt else "", "fetched_from": pid}) + "\n")
print("saved", out)
