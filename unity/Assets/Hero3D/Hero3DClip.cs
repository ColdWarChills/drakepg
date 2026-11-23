using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Drakepg.Hero3D
{
    /// <summary>
    /// Plays one animation clip on a hero model at a time the caller sets: the graph never advances on its own
    /// (manual update), so the clip follows whatever clock drives SetTime (in the fight: an Anim tween, which the
    /// battle speed, slow motion and hit-stops already scale). Works with Generic and Humanoid imports.
    /// </summary>
    public class Hero3DClip : System.IDisposable
    {
        PlayableGraph graph;
        AnimationClipPlayable playable;
        public AnimationClip Clip { get; private set; }
        public float Time { get; private set; }
        public float Length => Clip != null ? Clip.length : 0;

        public Hero3DClip(Animator animator, AnimationClip clip)
        {
            Clip = clip;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Hero3DClip " + clip.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "pose", animator);
            playable = AnimationClipPlayable.Create(graph, clip);
            // Humanoid: keep the clip's feet where they are (no foot IK), like Blender.
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            output.SetSourcePlayable(playable);
            graph.Play();
            SetTime(0);
        }

        /// <summary>Pose the model at `t` seconds of the clip (clamped to it), right now.</summary>
        public void SetTime(float t)
        {
            if (!graph.IsValid()) return;
            Time = Mathf.Clamp(t, 0, Length);
            playable.SetTime(Time);
            graph.Evaluate(0);
        }

        /// <summary>Pose at clip frame `frame` (the clip's own frame rate).</summary>
        public void SetFrame(float frame) => SetTime(frame / Mathf.Max(1, Clip.frameRate));

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
