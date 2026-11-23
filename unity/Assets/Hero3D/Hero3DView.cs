using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakepg.Hero3D
{
    /// <summary>
    /// A 3D skinned hero shown flat in the 2D fight. No second camera: every frame (LateUpdate) the model's skinned
    /// meshes are baked and drawn by a command buffer into a RenderTexture a few times sharper than the screen (toon pass
    /// and inverted-hull outline from Hero3DToon.shader, with a depth buffer of its own for self-occlusion), mipmapped,
    /// and shown on a quad with the game's FxSprite shader. So the picture sorts like any 2D figure
    /// (one sorting order), the HUD and effects are untouched, and the edges are supersampled.
    /// Free of game types (Assembly-CSharp): it follows a Transform (the hidden 2D figure's root, which the battle
    /// moves, squashes and flips) and takes the design scale as numbers.
    /// </summary>
    public class Hero3DView : MonoBehaviour
    {
        /// <summary>Design px per world unit (Design.PixelsPerUnit).</summary>
        public float PixelsPerUnit = 100;
        /// <summary>How tall the model is drawn at scale 1, in design px (Figure.ArtHeight).</summary>
        public float DrawnHeight = 240;
        /// <summary>The picture's box around the feet, design px at scale 1: width, height, and room below the feet.</summary>
        public float BoxWidth = 360, BoxHeight = 340, BoxBelow = 40;
        /// <summary>RenderTexture pixels per screen pixel (2 = 4 samples per pixel, 4 = 16); the mips average them down.</summary>
        public int Supersample = 4;
        /// <summary>Largest RenderTexture side.</summary>
        public int MaxTexture = 2048;
        /// <summary>The camera whose pixels the texture is sized for (default: Camera.main).</summary>
        public Camera ScreenCamera;
        /// <summary>The Transform to stand on (position, z tilt, scale; a negative x scale faces left). Null: stays put.</summary>
        public Transform Follow;
        /// <summary>The model turned this far from facing the viewer toward its facing side (degrees): a three-quarter view.</summary>
        public float Turn = 50;
        /// <summary>Outline and the colour the texture is cleared to (so the averaged edges keep the outline's colour).</summary>
        public Color OutlineColor = new Color(0.13f, 0.1f, 0.14f, 1);
        /// <summary>The model's front as a turn from +Z (180: a model that faces -Z, like a Blender export facing -Y).</summary>
        public float FrontYaw;
        /// <summary>The camera tilted down this far (degrees), like Blender's game view. 0: level.</summary>
        public float Pitch;
        /// <summary>Model metres drawn DrawnHeight px tall. 0: the model's own height, measured from its pose at Init.</summary>
        public float ModelHeight;
        /// <summary>Height of the ground under the model's origin, model units (GroundToLowest sets it from the pose).</summary>
        public float GroundY;
        /// <summary>Face left by mirroring the picture (the light and his features mirror too) instead of turning the model round.</summary>
        public bool FlipPicture;
        /// <summary>
        /// Draw the skinned renderers straight from their skin (cmd.DrawRenderer, run as rendering starts) instead of baking
        /// meshes on the CPU: the same picture (measured: at most 3 px differ on a moving pose) for 0.02 ms instead of 0.3.
        /// </summary>
        public bool UseDrawRenderer = true;
        /// <summary>A thin pale edge outside the outline: a 2D dilate of the picture's alpha (off by default).</summary>
        public bool Rim;
        public Color RimColor = new Color(1, 0.95f, 0.8f, 1);
        /// <summary>Rim width in screen px.</summary>
        public float RimWidthPx = 1;

        public MeshRenderer Picture { get; private set; }
        public SpriteRenderer Shadow { get; private set; }
        public RenderTexture Texture { get; private set; }
        /// <summary>What the quad shows: Texture, or the rim pass's copy of it.</summary>
        public RenderTexture Shown => Rim && rimTexture != null ? rimTexture : Texture;
        /// <summary>The view and projection matrices of the last render (world space around the hidden model).</summary>
        public Matrix4x4 LastView { get; private set; }
        public Matrix4x4 LastProj { get; private set; }
        /// <summary>The hidden model (bones are under it).</summary>
        public Transform Model => model;
        public int Facing => facing;
        /// <summary>Real time spent baking and queueing the draw last frame, ms.</summary>
        public float LastCpuMs { get; private set; }

        Transform model;
        readonly List<SkinnedMeshRenderer> parts = new List<SkinnedMeshRenderer>();
        readonly List<Mesh> baked = new List<Mesh>();
        CommandBuffer cmd;
        MaterialPropertyBlock block;
        Material pictureMaterial;
        Sprite soft;
        Mesh quad;
        float modelHeight = 1.75f;
        int facing = 1;
        RenderTexture rimTexture;
        Material rimMaterial;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int PixelId = Shader.PropertyToID("_Hero3DPixel");
        static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");
        static readonly int FlipId = Shader.PropertyToID("_Flip");
        static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        static readonly int RimTexelsId = Shader.PropertyToID("_RimTexels");

        /// <summary>
        /// A view under `parent` (the screen's world) showing `modelRoot` (its local origin at the feet, facing +Z; it is
        /// moved out of the way and never drawn by a camera), sorted at `order`.
        /// </summary>
        public static Hero3DView Create(Transform parent, Transform modelRoot, int order)
        {
            var go = new GameObject("Hero3DView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<Hero3DView>();
            view.Init(modelRoot, order);
            return view;
        }

        void Init(Transform modelRoot, int order)
        {
            model = modelRoot;
            // The model lives far from the stage and is only drawn by our command buffer.
            model.SetParent(null, false);
            model.position = new Vector3(0, -1000, 0);
            model.gameObject.name = "Hero3D model";
            parts.AddRange(model.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            foreach (var p in parts)
            {
                p.forceRenderingOff = true;
                p.updateWhenOffscreen = true;
                baked.Add(new Mesh { name = p.name + " baked" });
            }
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            modelHeight = MeasureHeight();

            Shadow = new GameObject("shadow").AddComponent<SpriteRenderer>();
            Shadow.transform.SetParent(transform, false);
            soft = Sprite.Create(SoftTexture(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64, 0, SpriteMeshType.FullRect);
            Shadow.sprite = soft;
            Shadow.color = new Color(0, 0, 0, 0.45f);
            Shadow.sortingOrder = order - 1;
            Shadow.transform.localScale = new Vector3(DrawnHeight * 0.48f / PixelsPerUnit, 26 / PixelsPerUnit, 1);

            // A plain quad, not a SpriteRenderer: on a SpriteRenderer our _MainTex in the property block was lost after a
            // while in the fight (the picture vanished; the cause was not pinned down), a MeshRenderer keeps our block.
            var pic = new GameObject("picture");
            pic.transform.SetParent(transform, false);
            pic.AddComponent<MeshFilter>().sharedMesh = quad = Quad();
            Picture = pic.AddComponent<MeshRenderer>();
            var shader = Resources.Load<Shader>("Fx/FxSprite");
            pictureMaterial = new Material(shader) { name = "Hero3D picture" };
            Picture.sharedMaterial = pictureMaterial;
            Picture.sortingOrder = order;
            Picture.shadowCastingMode = ShadowCastingMode.Off;
            Picture.receiveShadows = false;
            block = new MaterialPropertyBlock();
            // FxSprite is a sprite shader: on a plain mesh it needs the renderer colour and flip the SpriteRenderer would give.
            block.SetColor(RendererColorId, Color.white);
            block.SetVector(FlipId, Vector4.one);
            Picture.SetPropertyBlock(block);
            cmd = new CommandBuffer { name = "Hero3D" };
            SetFacing(1);
        }

        /// <summary>Sorting order of the picture (its shadow goes one under).</summary>
        public int Order
        {
            get => Picture.sortingOrder;
            set { Picture.sortingOrder = value; Shadow.sortingOrder = value - 1; }
        }

        /// <summary>Face right (1) or left (-1): the model turns round (it is not mirrored, so his right hand stays his right).</summary>
        public void SetFacing(int f)
        {
            facing = f >= 0 ? 1 : -1;
            // +Z is the model's front; turn it toward the facing side, keeping `Turn` degrees of it toward the viewer.
            // FlipPicture: the model always faces right and the quad is mirrored instead.
            var right = FlipPicture || facing > 0;
            model.rotation = Quaternion.Euler(0, (right ? 180 - Turn : 180 + Turn) + FrontYaw, 0);
        }

        /// <summary>Tint the picture (white = normal), alpha too.</summary>
        public void SetTint(Color c)
        {
            block.SetColor(RendererColorId, c);
            Picture.SetPropertyBlock(block);
        }

        /// <summary>Paint the whole figure one colour (the tint's colour) by `amount` 0-1, like Fx.WhiteFlash's solid fill.</summary>
        public void SetFlash(float amount)
        {
            block.SetFloat(FillId, Mathf.Clamp01(amount));
            Picture.SetPropertyBlock(block);
        }

        /// <summary>A 1x1 unit quad, centred, UVs 0..1, white vertex colours.</summary>
        static Mesh Quad()
        {
            var m = new Mesh { name = "Hero3D quad" };
            m.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            m.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            m.RecalculateBounds();
            return m;
        }

        void LateUpdate()
        {
            var t0 = Time.realtimeSinceStartup;
            if (Follow != null)
            {
                transform.position = new Vector3(Follow.position.x, Follow.position.y, transform.position.z);
                transform.rotation = Follow.rotation;
                var s = Follow.lossyScale;
                transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 1);
                var f = s.x < 0 ? -1 : 1;
                if (f != facing) SetFacing(f);
            }
            Render();
            LastCpuMs = (Time.realtimeSinceStartup - t0) * 1000;
        }

        void Render()
        {
            var cam = ScreenCamera != null ? ScreenCamera : Camera.main;
            // Screen px per world unit, and the box's size on screen.
            var pxPerUnit = cam != null && cam.orthographic ? cam.pixelHeight / (2 * cam.orthographicSize) : PixelsPerUnit;
            var scale = Mathf.Abs(transform.lossyScale.y);
            var boxW = BoxWidth / PixelsPerUnit;
            var boxH = BoxHeight / PixelsPerUnit;
            var screenW = boxW * scale * pxPerUnit;
            var screenH = boxH * scale * pxPerUnit;
            var ss = Supersample;
            // Sized in steps of 32 px so zooms don't reallocate every frame.
            int texW = Mathf.Min(MaxTexture, Mathf.CeilToInt(screenW * ss / 32) * 32);
            int texH = Mathf.Min(MaxTexture, Mathf.CeilToInt(screenH * ss / 32) * 32);
            texW = Mathf.Max(texW, 64);
            texH = Mathf.Max(texH, 64);
            if (Texture == null || Texture.width != texW || Texture.height != texH) MakeTexture(texW, texH);

            if (Rim && (rimTexture == null || rimTexture.width != texW || rimTexture.height != texH)) MakeRimTexture(texW, texH);

            // The picture: a 1-unit square scaled to the box, its bottom BoxBelow under the feet (mirrored: facing left).
            var mirror = FlipPicture && facing < 0 ? -1 : 1;
            Picture.transform.localScale = new Vector3(boxW * mirror, boxH, 1);
            Picture.transform.localPosition = new Vector3(0, (BoxHeight / 2 - BoxBelow) / PixelsPerUnit, 0);
            block.SetTexture(MainTexId, Shown);
            Picture.SetPropertyBlock(block);

            // Model units per design px, then the box in model units around the feet; the camera tilted down by Pitch,
            // the feet kept at the same spot of the box.
            var k = (ModelHeight > 0 ? ModelHeight : modelHeight) / DrawnHeight;
            float w = BoxWidth * k, h = BoxHeight * k, below = BoxBelow * k;
            var feet = model.position + new Vector3(0, GroundY, 0);
            var rot = Quaternion.Euler(Pitch, 0, 0);
            var eye = feet + rot * new Vector3(0, h / 2 - below, -10);
            var view = Matrix4x4.TRS(eye, rot, new Vector3(1, 1, -1)).inverse;
            var proj = Matrix4x4.Ortho(-w / 2, w / 2, -h / 2, h / 2, 0.1f, 20);
            LastView = view;
            LastProj = proj;

            if (!UseDrawRenderer)
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i].gameObject.activeInHierarchy) parts[i].BakeMesh(baked[i], true);
            cmd.Clear();
            cmd.SetRenderTarget(Texture);
            cmd.ClearRenderTarget(true, true, new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, 0));
            cmd.SetViewProjectionMatrices(view, proj);
            // Clip-space size of one screen pixel in this texture: the outline stays the same width on screen.
            var texPerScreen = texW / screenW;
            cmd.SetGlobalVector(PixelId, new Vector4(2f / texW * texPerScreen, 2f / texH * (texH / screenH), 0, 0));
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (!p.gameObject.activeInHierarchy) continue;
                var m = Matrix4x4.TRS(p.transform.position, p.transform.rotation, Vector3.one);
                var mats = p.sharedMaterials;
                var subs = UseDrawRenderer ? p.sharedMesh.subMeshCount : baked[i].subMeshCount;
                for (int sub = 0; sub < subs && sub < mats.Length; sub++)
                {
                    // A slot the look file doesn't know (no material) is not drawn.
                    if (mats[sub] == null) continue;
                    if (UseDrawRenderer)
                    {
                        cmd.DrawRenderer(p, mats[sub], sub, 1);
                        cmd.DrawRenderer(p, mats[sub], sub, 0);
                    }
                    else
                    {
                        cmd.DrawMesh(baked[i], m, mats[sub], sub, 1);
                        cmd.DrawMesh(baked[i], m, mats[sub], sub, 0);
                    }
                }
            }
            cmd.SetGlobalVector(PixelId, Vector4.zero);
            if (Rim)
            {
                // The rim: wherever the picture is clear but a solid pixel lies within the rim's width, the rim colour.
                cmd.SetGlobalColor(RimColorId, RimColor);
                cmd.SetGlobalFloat(RimTexelsId, Mathf.Max(0.5f, RimWidthPx * texW / screenW));
                cmd.Blit(Texture, rimTexture, rimMaterial);
                cmd.GenerateMips(rimTexture);
            }
            else cmd.GenerateMips(Texture);
            // DrawRenderer reads the renderers' GPU skin, which Unity updates after LateUpdate: run it when rendering
            // starts (this frame's skin), not now (last frame's). Baked meshes are current already.
            if (UseDrawRenderer) pending = true;
            else Graphics.ExecuteCommandBuffer(cmd);
        }

        void MakeRimTexture(int w, int h)
        {
            if (rimTexture != null) { rimTexture.Release(); Destroy(rimTexture); }
            if (rimMaterial == null) rimMaterial = new Material(Resources.Load<Shader>("Hero3D/Hero3DRim")) { name = "Hero3D rim" };
            rimTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                name = "Hero3D picture + rim", useMipMap = true, autoGenerateMips = false,
                filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp,
            };
            rimTexture.Create();
        }

        /// <summary>Stand the current pose on the ground: GroundY = the lowest point of the posed meshes (a CPU bake, not per frame).</summary>
        public void GroundToLowest() => GroundY = LowestPoint();

        /// <summary>Lowest point of the posed meshes above the model's origin, model units.</summary>
        public float LowestPoint()
        {
            float low = float.MaxValue;
            for (int i = 0; i < parts.Count; i++)
            {
                if (!parts[i].gameObject.activeInHierarchy) continue;
                parts[i].BakeMesh(baked[i], true);
                var m = Matrix4x4.TRS(parts[i].transform.position - model.position, parts[i].transform.rotation, Vector3.one);
                foreach (var v in baked[i].vertices) low = Mathf.Min(low, m.MultiplyPoint3x4(v).y);
            }
            return low == float.MaxValue ? 0 : low;
        }

        /// <summary>Distance of a world point in front of the last render's eye (smaller = nearer the viewer).</summary>
        public float Depth(Vector3 worldPoint) => -LastView.MultiplyPoint3x4(worldPoint).z;

        /// <summary>Settings from an imported model's look data: turn, tilt, size, rim.</summary>
        public void Apply(Hero3DModel m)
        {
            if (m == null) return;
            Turn = m.gameYaw;
            FrontYaw = m.frontYaw;
            // Facing left by mirroring: turning him round would put his weapon (right) hand on the far side.
            FlipPicture = true;
            Pitch = m.gamePitch;
            ModelHeight = m.heightM;
            Rim = m.rim;
            RimColor = m.rimColor;
            RimWidthPx = m.rimWidthPx;
            SetFacing(facing);
        }

        void MakeTexture(int w, int h)
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
            Texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "Hero3D picture",
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
            };
            Texture.Create();
        }

        /// <summary>Height of the model's top above its feet (model units), from a bake of its current pose.</summary>
        float MeasureHeight()
        {
            float top = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].BakeMesh(baked[i], true);
                var verts = baked[i].vertices;
                var m = Matrix4x4.TRS(parts[i].transform.position - model.position, parts[i].transform.rotation, Vector3.one);
                foreach (var v in verts) top = Mathf.Max(top, m.MultiplyPoint3x4(v).y);
            }
            return top > 0.1f ? top : 1.75f;
        }

        static Texture2D SoftTexture()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Hero3D shadow", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude / (n / 2f);
                    var a = Mathf.Clamp01(1 - d);
                    px[y * n + x] = new Color(1, 1, 1, a * a * (3 - 2 * a));
                }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        bool pending;

        void OnEnable() => RenderPipelineManager.beginContextRendering += BeginRendering;

        void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= BeginRendering;
            pending = false;
        }

        void BeginRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!pending || cmd == null) return;
            pending = false;
            context.ExecuteCommandBuffer(cmd);
        }

        void OnDestroy()
        {
            cmd?.Release();
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
            if (rimTexture != null) { rimTexture.Release(); Destroy(rimTexture); }
            if (rimMaterial != null) Destroy(rimMaterial);
            foreach (var m in baked) if (m != null) Destroy(m);
            if (pictureMaterial != null) Destroy(pictureMaterial);
            if (quad != null) Destroy(quad);
            if (soft != null) { Destroy(soft.texture); Destroy(soft); }
            if (model != null) Destroy(model.gameObject);
        }
    }
}
