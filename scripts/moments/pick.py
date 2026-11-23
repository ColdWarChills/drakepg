"""Record kept piece edits in moments.json: pick.py <piece>@<moment>_s<seed> [...] (a piece edit's file name)."""
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import REDO  # noqa: E402

p = json.loads((REDO / "moments.json").read_text())
for n in sys.argv[1:]:
    piece, rest = n.split("@")
    moment = rest.split("_s")[0]
    p.setdefault("pieces", {}).setdefault(piece, {})[moment] = n
(REDO / "moments.json").write_text(json.dumps(p, indent=1))
print(json.dumps(p["pieces"], indent=1))
