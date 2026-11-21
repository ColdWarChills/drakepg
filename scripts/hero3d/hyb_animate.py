"""Hybrid test: the 3D hero's slash (Blender render) drives Wan-Animate-2, the drawn hero is the reference.
Uses the Animation Workflow runner r2_animate.py with the hero's own prompts.
<venv python> -I hyb_animate.py <driver.mp4> <reference.png> <out.mp4> [--outfit "words"] [--seed N] [--length 41] ...
(run from the Animation Workflow folder with its .venv python; ComfyUI must be running; one take ~130 s, 14.8 GB)
--outfit replaces "a loose cream linen shirt with rolled sleeves" in the character text (the reference's clothes)."""
import sys

sys.path.insert(0, r"C:\Users\Epic\Desktop\Animation Workflow\work\odd_bodies\round2\tools")
import r2_animate as R  # noqa: E402

args = sys.argv[1:]
OUTFIT = "a loose cream linen shirt with rolled sleeves"
if "--outfit" in args:
    i = args.index("--outfit")
    OUTFIT = args[i + 1]
    del args[i:i + 2]
R.CHARACTER = ("Character appearance description: A young cartoon swordsman with spiky dark brown hair and a red "
               "headband, " + OUTFIT + ", a brown belt, dark teal trousers and brown "
               "leather boots. He holds one steel arming sword in his right hand, the hand nearest the camera, the whole "
               "time. Clean 2D hand-painted game art, bold dark outlines, flat cel shading.\n"
               "Background description: plain light grey backdrop.")
R.MOTION = ("A reference video of a figure doing one fast overhead sword slash: he winds the sword back over his "
            "shoulder, lunges forward into a deep low stance and cuts down, holds, then stands back up.")
R.NEG += ", second sword, sword in left hand, sword switching hands, two swords, 3d render, plastic"
sys.argv = [sys.argv[0]] + args
R.main()
