using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using Unity.Collections;

namespace Drakepg
{
    /// <summary>
    /// A figure's picture bent by three soft bones up its middle (legs, body, head; the math is Fx/Bend.cginc): it
    /// breathes, shifts its weight, its head lags behind the body, and hair and headbands trail when it moves. Screens
    /// pose it with `Pose` (lean into a blow, crouch before a leap) and springs carry it there and back with some
    /// follow-through. No new art: the body gets a grid mesh, the weapons ride along on their hands. Figure.EnableBend adds it.
    /// </summary>
    public class Bend : MonoBehaviour
    {
        /// <summary>Testing switch (cheat menu): figures made while it's off stay stiff.</summary>
        public static bool On = true;

        /// <summary>Joints as fractions of the figure's height: the hips and the shoulders; and how soft they bend.</summary>
        public const float HipAt = 0.5f, ShoulderAt = 0.78f, Softness = 0.07f;
        const int GridColumns = 8, GridRows = 14;

        static readonly int XfId = Shader.PropertyToID("_BendXf"), JId = Shader.PropertyToID("_BendJ");
        static readonly int A0Id = Shader.PropertyToID("_BendA0"), A1Id = Shader.PropertyToID("_BendA1"), A2Id = Shader.PropertyToID("_BendA2");
        static readonly int T01Id = Shader.PropertyToID("_BendT01"), T2Id = Shader.PropertyToID("_BendT2"), WaveId = Shader.PropertyToID("_BendWave");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        /// <summary>Degrees the top tips toward the figure's front (negative: back).</summary>
        public float Lean;
        /// <summary>0 standing, 1 a deep crouch.</summary>
        public float Crouch;
        /// <summary>Breathing and the slow weight shift.</summary>
        public bool Idle = true;
        /// <summary>Degrees of shaking with strain (charging up a big move).</summary>
        public float Tremble;
        /// <summary>0-1: badly hurt (hunched, breathing hard and fast).</summary>
        public float Hurt;

        struct Spring
        {
            public float X, V;

            public void Step(float target, float omega, float damping, float dt)
            {
                V += (omega * omega * (target - X) - 2 * damping * omega * V) * dt;
                X += V * dt;
            }
        }

        struct Hand
        {
            public Transform Pivot, Held;
            public Vector3 Rest;
        }

        Transform art;
        float height, halfWidth;
        readonly List<SpriteRenderer> bent = new List<SpriteRenderer>();
        readonly List<Hand> hands = new List<Hand>();
        MaterialPropertyBlock block;
        Spring lean, crouch, head, dragX, dragY;
        float time, restIn = -1;
        bool frozen;
        Vector3 lastPos;
        bool hasLast;
        /// <summary>This frame's bones (figure space) and the loose parts' offset and wave.</summary>
        Vector4 a0, a1, a2, t01, t2, wave;
        float th0, th1, th2;

        /// <summary>Set up on a figure's art root. `parts`: the sprites to bend (the body pictures among them: the bare art
        /// and any key poses); `held`: each weapon's hand pivot and the child under it that tilts with the body.</summary>
        public void Init(Transform artRoot, float drawnHeight, float drawnWidth, ICollection<SpriteRenderer> bodies, IEnumerable<SpriteRenderer> parts,
            IEnumerable<(Transform pivot, Transform held)> held)
        {
            art = artRoot;
            height = drawnHeight / Design.PixelsPerUnit;
            halfWidth = Mathf.Max(0.01f, drawnWidth / 2 / Design.PixelsPerUnit);
            block = new MaterialPropertyBlock();
            time = Random.value * 10;
            foreach (var r in parts)
            {
                if (r == null || r.sprite == null) continue;
                // Small parts (fingers, fists) are near-rigid and keep their quad.
                if (bodies.Contains(r)) r.sprite = Grid(r.sprite);
                // The default sprite material can't bend; FxSprite (alpha) and HeldSprite can.
                if (r.sharedMaterial == null || r.sharedMaterial.shader == null || !r.sharedMaterial.shader.name.StartsWith("Drakepg/")) r.sharedMaterial = Fx.AlphaMaterial;
                bent.Add(r);
            }
            foreach (var (pivot, h) in held) hands.Add(new Hand { Pivot = pivot, Held = h, Rest = pivot.localPosition });
            Solve();
            Push();
        }

        /// <summary>A weapon hand moved to `rest` (art space, unbent): a drawn key pose swapped in (Figure.SetPose).</summary>
        public void SetRest(Transform pivot, Vector3 rest)
        {
            for (int i = 0; i < hands.Count; i++)
                if (hands[i].Pivot == pivot) hands[i] = new Hand { Pivot = pivot, Held = hands[i].Held, Rest = rest };
            if (!frozen) Push();
        }

        /// <summary>Lean (degrees, + = forward) and crouch (0-1). `hold` > 0: back to standing after that many seconds.</summary>
        public void Pose(float leanTo, float crouchTo, float hold = -1)
        {
            Lean = leanTo;
            Crouch = crouchTo;
            restIn = hold;
        }

        /// <summary>Take another figure's pose this frame and keep it (afterimages).</summary>
        public void Freeze(Bend from)
        {
            if (from == null) return;
            a0 = from.a0; a1 = from.a1; a2 = from.a2; t01 = from.t01; t2 = from.t2; wave = from.wave;
            th0 = from.th0; th1 = from.th1; th2 = from.th2;
            frozen = true;
            Push();
        }

        void LateUpdate()
        {
            if (art == null) return;
            if (frozen) return;
            var dt = Mathf.Min(Time.deltaTime, 0.1f) * Anim.TimeScale;
            if (restIn > 0 && (restIn -= dt) <= 0)
            {
                Lean = Crouch = 0;
                restIn = -1;
            }
            // Movement in the figure's own space, for the loose parts' drag.
            var pos = transform.position;
            var vel = Vector3.zero;
            if (hasLast && dt > 0) vel = art.InverseTransformVector(pos - lastPos) / Mathf.Max(Time.deltaTime, 0.001f);
            lastPos = pos;
            hasLast = true;
            // Small steps keep the springs steady at low frame rates.
            for (float left = dt; left > 0; left -= 1 / 60f) Step(Mathf.Min(left, 1 / 60f), vel);
            Solve();
            Push();
        }

        float breath, shift, breathPhase, hurt, tremble;

        void Step(float dt, Vector3 vel)
        {
            time += dt;
            hurt = Mathf.MoveTowards(hurt, Hurt, dt * 1.5f);
            tremble = Mathf.MoveTowards(tremble, Tremble, dt * 30);
            // Breathing (2.6 s, 1.4 s and deeper when badly hurt) and a slow weight shift from foot to foot (4.3 s); the
            // knees soften as it breathes out.
            breathPhase += dt * 2 * Mathf.PI / Mathf.Lerp(2.6f, 1.4f, hurt);
            breath = Idle ? (0.5f - 0.5f * Mathf.Cos(breathPhase)) * (1 + 0.7f * hurt) : 0;
            shift = Idle ? Mathf.Sin(time * 2 * Mathf.PI / 4.3f) * (1 - 0.4f * hurt) : 0;
            var settle = Idle ? 0.04f + 0.06f * (1 - breath) + 0.15f * hurt : 0;
            crouch.Step(Crouch + settle, 22, 0.7f, dt);
            lean.Step(Lean + (Idle ? 6 * hurt : 0), 17, 0.5f, dt);
            // Hair and cloth trail behind the motion and swing past when it stops.
            var limit = height * 0.07f;
            dragX.Step(Mathf.Clamp(-vel.x * 0.022f, -limit, limit), 12, 0.32f, dt);
            dragY.Step(Mathf.Clamp(-vel.y * 0.012f, -limit, limit), 12, 0.32f, dt);
            // The head follows the body's tilt late.
            head.Step(BodyTilt(shift, lean.X), 10, 0.38f, dt);
        }

        /// <summary>Forward-lean rotation times a scale (bone frame), as the shader's (m00, m01, m10, m11).</summary>
        static Vector4 Mat(float forwardDegrees, float sx, float sy)
        {
            var a = forwardDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector4(c * sx, s * sy, -s * sx, c * sy);
        }

        /// <summary>The body's tilt (degrees forward) for a weight shift and a lean.</summary>
        static float BodyTilt(float shift, float lean) => shift * 0.5f + lean;

        static Vector2 Mul(Vector4 m, Vector2 p) => new Vector2(m.x * p.x + m.y * p.y, m.z * p.x + m.w * p.y);

        void Solve()
        {
            if (frozen) return;
            var c = Mathf.Max(0, crouch.X);
            // The hips sway over the feet and the body leans back over them; leans tip a little from the ankles.
            th0 = shift * 1.8f + lean.X * 0.35f;
            th1 = BodyTilt(shift, lean.X) - th0;
            if (tremble > 0)
            {
                // Two fast wobbles that never line up read as straining, not as a loop.
                var shake = tremble * (0.6f * Mathf.Sin(time * 2 * Mathf.PI * 13) + 0.4f * Mathf.Sin(time * 2 * Mathf.PI * 19.7f));
                th0 += shake * 0.4f;
                th1 += shake;
            }
            th2 = head.X - (th0 + th1);
            Vector2 j1 = new Vector2(0, height * HipAt), j2 = new Vector2(0, height * ShoulderAt);
            // Legs: tip from the ankles, squat shorter and a little wider. Feet stay put.
            a0 = Mat(th0, 1 + 0.07f * c, 1 - 0.16f * c);
            var j1b = Mul(a0, j1);
            // Body: fills out and rises with each breath.
            a1 = Mat(th0 + th1, 1 + 0.025f * breath, 1 + 0.045f * breath);
            var t1 = j1b - Mul(a1, j1);
            var j2b = Mul(a1, j2) + t1;
            a2 = Mat(th0 + th1 + th2, 1, 1);
            var t2b = j2b - Mul(a2, j2);
            t01 = new Vector4(0, 0, t1.x, t1.y);
            t2 = new Vector4(t2b.x, t2b.y, dragX.X, dragY.X);
            // A ripple up through the loose parts: always a little, more while they're swinging.
            var amp = height * (0.004f + 0.25f * Mathf.Min(0.05f, Mathf.Abs(dragX.V) * 0.02f));
            wave = new Vector4(amp, time * 2.4f, 2 * Mathf.PI / (height * 0.35f), halfWidth);
        }

        /// <summary>Where a point of the picture (figure space, at rest) is drawn now: BendPoint in Bend.cginc.</summary>
        public Vector2 At(Vector2 p, out float degrees)
        {
            float b = height * Softness, hip = height * HipAt, shoulder = height * ShoulderAt;
            float w0 = 1 - Smooth(hip - b, hip + b, p.y);
            float w2 = Smooth(shoulder - b, shoulder + b, p.y);
            float w1 = Mathf.Clamp01(1 - w0 - w2);
            var q = w0 * (Mul(a0, p) + new Vector2(t01.x, t01.y)) + w1 * (Mul(a1, p) + new Vector2(t01.z, t01.w)) + w2 * (Mul(a2, p) + new Vector2(t2.x, t2.y));
            var loose = Smooth(shoulder - 2 * b, height, p.y) * (0.5f + 0.5f * Mathf.Clamp01(Mathf.Abs(p.x) / wave.w));
            q += (new Vector2(t2.z, t2.w) + new Vector2(wave.x * Mathf.Sin(wave.y + p.y * wave.z), 0)) * loose;
            degrees = w0 * th0 + w1 * (th0 + th1) + w2 * (th0 + th1 + th2);
            return q;
        }

        static float Smooth(float e0, float e1, float x)
        {
            var t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        void Push()
        {
            var j = new Vector4(height * HipAt, height * ShoulderAt, height * Softness, height);
            foreach (var r in bent)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                if (r.sprite != null) block.SetTexture(MainTexId, r.sprite.texture);
                var t = r.transform;
                block.SetVector(XfId, new Vector4(t.localScale.x, t.localScale.y, t.localPosition.x, t.localPosition.y));
                block.SetVector(JId, j);
                block.SetVector(A0Id, a0);
                block.SetVector(A1Id, a1);
                block.SetVector(A2Id, a2);
                block.SetVector(T01Id, t01);
                block.SetVector(T2Id, t2);
                block.SetVector(WaveId, wave);
                r.SetPropertyBlock(block);
            }
            // Weapons stay rigid: their hand moves and turns with the body around it.
            foreach (var h in hands)
            {
                if (h.Pivot == null) continue;
                var at = At(h.Rest, out var degrees);
                h.Pivot.localPosition = new Vector3(at.x, at.y, h.Rest.z);
                if (h.Held != null) h.Held.localRotation = Quaternion.Euler(0, 0, -degrees);
            }
        }

        static readonly Dictionary<Sprite, Sprite> grids = new Dictionary<Sprite, Sprite>();

        /// <summary>The same picture on a grid of quads (a sprite's own mesh is too coarse to bend), cached per sprite.</summary>
        public static Sprite Grid(Sprite src)
        {
            if (src == null) return null;
            if (grids.TryGetValue(src, out var cached) && cached != null) return cached;
            var rect = src.rect;
            var tex = src.texture;
            var s = Sprite.Create(tex, rect, new Vector2(src.pivot.x / rect.width, src.pivot.y / rect.height), src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.name = src.name;
            int n = (GridColumns + 1) * (GridRows + 1);
            var pos = new NativeArray<Vector3>(n, Allocator.Temp);
            var uv = new NativeArray<Vector2>(n, Allocator.Temp);
            var idx = new NativeArray<ushort>(GridColumns * GridRows * 6, Allocator.Temp);
            for (int y = 0; y <= GridRows; y++)
            for (int x = 0; x <= GridColumns; x++)
            {
                var i = y * (GridColumns + 1) + x;
                float tx = rect.width * x / GridColumns, ty = rect.height * y / GridRows;
                pos[i] = new Vector3((tx - src.pivot.x) / src.pixelsPerUnit, (ty - src.pivot.y) / src.pixelsPerUnit, 0);
                uv[i] = new Vector2((rect.x + tx) / tex.width, (rect.y + ty) / tex.height);
            }
            int k = 0;
            for (int y = 0; y < GridRows; y++)
            for (int x = 0; x < GridColumns; x++)
            {
                var i = (ushort)(y * (GridColumns + 1) + x);
                var up = (ushort)(i + GridColumns + 1);
                idx[k++] = i; idx[k++] = up; idx[k++] = (ushort)(i + 1);
                idx[k++] = (ushort)(i + 1); idx[k++] = up; idx[k++] = (ushort)(up + 1);
            }
            s.SetVertexCount(n);
            s.SetVertexAttribute(VertexAttribute.Position, pos);
            s.SetVertexAttribute(VertexAttribute.TexCoord0, uv);
            s.SetIndices(idx);
            pos.Dispose();
            uv.Dispose();
            idx.Dispose();
            grids[src] = s;
            return s;
        }
    }
}
