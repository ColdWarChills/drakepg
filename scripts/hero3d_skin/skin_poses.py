"""Poses for the drawn-skin test, built from scripts/hero3d_v2/poses.py's vocabulary (imported, never edited).
Sword hand = his right (socket_hand_R), the near side at the game view (yaw 40)."""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "hero3d_v2"))
import poses as PZ  # noqa: E402

HEAD_TO_VIEWER = -22          # extra head/neck turn toward the viewer so both eyes show, as in the drawn hero


def stance():
    PZ.pose_stance()
    PZ.twist("Neck", HEAD_TO_VIEWER * 0.4)
    PZ.twist("Head", HEAD_TO_VIEWER * 0.6)


def wind():
    """After char_hero_bare_sword_wind: weight back, shoulders turned away (back toward us), sword fist (his right, the
    near arm) high behind the head, the far arm forward and low."""
    PZ.twist("Hips", 35)
    PZ.leg_out("Left", 8)
    PZ.hip_flex("Left", 28)
    PZ.knee("Left", 18)
    PZ.leg_out("Right", 8)
    PZ.hip_flex("Right", -18)
    PZ.knee("Right", 30)
    PZ.feet_flat({"Right": 0})
    PZ.twist("Spine", 6)
    PZ.twist("Chest", 8)
    PZ.bend("Spine", -4)
    PZ.twist("Neck", -14)
    PZ.twist("Head", -16)
    PZ.clav_up("Right", 20)
    PZ.arm_down("Right", -140)            # searched numerically: fist above and behind the head (arm_fwd + = back here)
    PZ.arm_fwd("Right", 40)
    PZ.elbow("Right", 60)
    PZ.wrist("Right", 30)
    PZ.arm_down("Left", 30)
    PZ.arm_fwd("Left", 60)
    PZ.elbow("Left", 30)


def strike():
    """After char_hero_bare_sword_strike: deep lunge, torso bent forward, sword arm (his right) swung down in front of
    the front knee, the far arm flung up and back."""
    PZ.twist("Hips", 30)
    PZ.hip_flex("Left", 70)
    PZ.knee("Left", 80)
    PZ.leg_out("Left", 6)
    PZ.hip_flex("Right", -38)
    PZ.knee("Right", 12)
    PZ.leg_out("Right", 6)
    PZ.feet_flat()
    PZ.bend("Spine", 24)
    PZ.bend("Chest", 16)
    PZ.twist("Spine", 10)
    PZ.twist("Chest", 12)
    PZ.bend("Neck", -14)
    PZ.bend("Head", -10)
    PZ.twist("Head", -12)
    PZ.arm_down("Right", 30)
    PZ.arm_fwd("Right", 70)
    PZ.elbow("Right", 8)
    PZ.wrist("Right", -45)
    PZ.arm_down("Left", -60)
    PZ.arm_fwd("Left", -55)
    PZ.elbow("Left", 55)


def spread():
    """Fill view pose: limbs apart so no limb covers another (inner arms, inner legs, sides)."""
    PZ.arm_down("Right", -10)
    PZ.arm_down("Left", -10)
    PZ.arm_fwd("Right", 10)
    PZ.arm_fwd("Left", 10)
    PZ.elbow("Right", 15)
    PZ.elbow("Left", 15)
    PZ.leg_out("Right", 14)
    PZ.leg_out("Left", 14)
    PZ.feet_flat()


POSES = {"stance": stance, "wind": wind, "strike": strike, "spread": spread, "rest": lambda: None}
