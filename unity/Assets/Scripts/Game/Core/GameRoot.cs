using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// One screen of the game (what a Phaser scene was): Title, Create, Room, Battle, Result. Sprites go under `World`,
    /// UI under `Root` (design pixels). Leaving a screen destroys both and stops its tweens.
    /// </summary>
    public abstract class GameScreen
    {
        public Transform World;
        public VisualElement Root;
        /// <summary>Input is ignored while false (a screen fading out).</summary>
        public bool InputEnabled = true;

        public virtual void Enter() { }
        public virtual void Tick(float dt) { }
        public virtual void Exit() { }
    }

    /// <summary>
    /// The game's one MonoBehaviour: loads the data, owns the camera, the UI document and the current screen, runs the
    /// tweens. Screens switch with `Go` (a fade, like goTo() in src/scenes/keys.ts).
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot I { get; private set; }

        public Camera Camera { get; private set; }
        /// <summary>The 1280x720 UI area, centered in the window like the letterboxed camera.</summary>
        public VisualElement DesignRoot { get; private set; }
        public GameScreen Screen { get; private set; }

        UIDocument document;
        VisualElement fade;
        Vector3 cameraHome;
        float shakeTime, shakeAmount;
        bool switching;
        bool warming = true;

        /// <summary>Called by the Boot scene's GameRoot object, or made on the fly.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            Application.targetFrameRate = 60;
            if (!Db.Loaded) Db.Load(Resources.Load<TextAsset>("Data/game").text);
            Saves.Init();
            Fx.CreateTextures();
            SetupCamera();
            SetupUi();
        }

        void Start() => Go(new TitleScreen(), 0);

        void SetupCamera()
        {
            // One camera only: URP draws the UI overlay once, with the last camera, and a second camera for the
            // letterbox bars stopped the UI from showing. The bars are black quads on this camera instead.
            Camera = Camera.main;
            if (Camera == null) Camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.rect = new Rect(0, 0, 1, 1);
            Camera.orthographicSize = Design.Height / 2 / Design.PixelsPerUnit;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Color.black;
            Camera.transform.position = cameraHome = new Vector3(0, 0, -10);
            Camera.depth = 0;
            bars = new SpriteRenderer[4];
            for (int i = 0; i < bars.Length; i++)
            {
                var bar = new GameObject("letterbox").AddComponent<SpriteRenderer>();
                bar.transform.SetParent(Camera.transform, false);
                bar.sprite = Art.White;
                bar.color = Color.black;
                bar.sortingOrder = short.MaxValue;
                bars[i] = bar;
            }
        }

        SpriteRenderer[] bars;
        /// <summary>Camera zoom (1 = the whole 1280x720 design area).</summary>
        float zoom = 1;

        void SetupUi()
        {
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            DesignRoot = new VisualElement { name = "design", pickingMode = PickingMode.Ignore };
            DesignRoot.style.position = Position.Absolute;
            DesignRoot.style.width = Design.Width;
            DesignRoot.style.height = Design.Height;
            DesignRoot.style.overflow = Overflow.Hidden;
            // Every label inherits Alegreya Sans (bold ones get its Bold cut in UiSmooth's walk).
            Fonts.Body(DesignRoot);
            root.Add(DesignRoot);
            fade = new VisualElement { name = "fade", pickingMode = PickingMode.Ignore };
            fade.style.position = Position.Absolute;
            fade.style.left = fade.style.top = 0;
            fade.style.width = Design.Width;
            fade.style.height = Design.Height;
            fade.style.backgroundColor = Color.black;
            fade.style.opacity = 0;
        }

        /// <summary>Fade out, swap screens, fade in.</summary>
        public async void Go(GameScreen next, float duration = 0.25f)
        {
            // Mid-switch (a fade still running): go there right after, so a request is never lost.
            if (switching)
            {
                queued = (next, duration);
                return;
            }
            switching = true;
            try
            {
                if (Screen != null)
                {
                    Screen.InputEnabled = false;
                    if (duration > 0) await FadeTo(1, duration);
                    var old = Screen;
                    Screen = null;
                    try { old.Exit(); } catch (Exception e) { Debug.LogException(e); }
                    Anim.Clear(old);
                    if (old.World != null) Destroy(old.World.gameObject);
                    old.Root?.RemoveFromHierarchy();
                }
                Anim.TimeScale = 1;
                ResetView();
                shakeTime = 0;
                next.World = new GameObject(next.GetType().Name).transform;
                next.Root = new VisualElement { name = next.GetType().Name, pickingMode = PickingMode.Ignore };
                next.Root.style.position = Position.Absolute;
                next.Root.style.left = next.Root.style.top = 0;
                next.Root.style.width = Design.Width;
                next.Root.style.height = Design.Height;
                DesignRoot.Add(next.Root);
                Screen = next;
                Anim.DefaultOwner = next;
                try { next.Enter(); } catch (Exception e) { Debug.LogException(e); }
                DesignRoot.Add(fade);
                fade.BringToFront();
                await FadeTo(0, duration > 0 ? duration : 0.25f);
            }
            finally
            {
                switching = false;
            }
            if (queued.next != null)
            {
                var (n, d) = queued;
                queued = default;
                Go(n, d);
            }
        }

        (GameScreen next, float duration) queued;

        Task FadeTo(float to, float duration)
        {
            var from = fade.resolvedStyle.opacity;
            if (fade.parent == null) DesignRoot.Add(fade);
            fade.BringToFront();
            fade.pickingMode = to > 0 ? PickingMode.Position : PickingMode.Ignore;
            return Anim.Float(from, to, duration, v => fade.style.opacity = v, Ease.Linear, owner: this).Task;
        }

        /// <summary>The camera size that fits the 1280x720 design area in the window (bars fill the rest).</summary>
        float FitSize
        {
            get
            {
                var aspect = (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height);
                const float target = Design.Width / Design.Height;
                var half = Design.Height / 2 / Design.PixelsPerUnit;
                return aspect >= target ? half : half * target / aspect;
            }
        }

        /// <summary>The camera back at the middle, zoom 1, level.</summary>
        public void ResetView()
        {
            viewTween?.Stop();
            cameraHome = new Vector3(0, 0, -10);
            Camera.transform.position = cameraHome;
            Camera.transform.rotation = Quaternion.identity;
            zoom = 1;
            Camera.orthographicSize = FitSize;
        }

        Anim.Handle viewTween;

        /// <summary>Pan the camera's center to (x, y) design px, zoom and tilt it (cam.pan + zoomTo + rotateTo).</summary>
        public Anim.Handle View(float x, float y, float zoom, float duration, Ease ease = Ease.CubicOut, float rotateRadians = 0)
        {
            viewTween?.Stop();
            var fromPos = cameraHome;
            var toPos = Design.World(x, y, -10);
            var fromZoom = this.zoom;
            var fromRot = Camera.transform.eulerAngles.z;
            if (fromRot > 180) fromRot -= 360;
            var toRot = -rotateRadians * Mathf.Rad2Deg;
            viewTween = Anim.Float(0, 1, duration, t =>
            {
                cameraHome = Vector3.LerpUnclamped(fromPos, toPos, t);
                if (shakeTime <= 0) Camera.transform.position = cameraHome;
                this.zoom = Mathf.LerpUnclamped(fromZoom, zoom, t);
                Camera.orthographicSize = FitSize / this.zoom;
                Camera.transform.rotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(fromRot, toRot, t));
            }, ease, owner: Screen);
            return viewTween;
        }

        /// <summary>A full-screen flash of color that fades out (cam.flash).</summary>
        public void Flash(float seconds, Color color)
        {
            if (Screen?.Root == null) return;
            var f = new VisualElement { pickingMode = PickingMode.Ignore };
            f.style.position = Position.Absolute;
            f.style.left = f.style.top = 0;
            f.style.width = Design.Width;
            f.style.height = Design.Height;
            f.style.backgroundColor = color;
            DesignRoot.Add(f);
            Anim.Float(1, 0, seconds, a => f.style.opacity = a, Ease.Linear, owner: this).OnComplete(f.RemoveFromHierarchy);
        }

        /// <summary>Fade to black, run `middle`, fade back in (a rest at the inn).</summary>
        public async void Blink(float outSeconds, float inSeconds, Action middle)
        {
            await FadeTo(1, outSeconds);
            try { middle?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            await FadeTo(0, inSeconds);
        }

        /// <summary>The camera knocked off its rest by a blow (design px, y down) and how fast it's moving back.</summary>
        Vector2 kick, kickVelocity;

        /// <summary>
        /// Knock the camera (dx, dy) design px, the way a blow travels; it springs back in about a tenth of a second
        /// (real time, so slow motion doesn't drag it out).
        /// </summary>
        public void Kick(float dx, float dy)
        {
            kick += new Vector2(dx, dy);
            kickVelocity = Vector2.zero;
        }

        float shakeTotal;

        /// <summary>How much of a shake is left: it fades out (quadratically) over its length instead of stopping dead.</summary>
        float ShakeLeft => shakeTotal > 0 && shakeTime > 0 ? (shakeTime / shakeTotal) * (shakeTime / shakeTotal) : 0;

        /// <summary>Camera shake of `pixels` design px that fades out over `seconds`. A weaker one doesn't cut a stronger one short.</summary>
        public void Shake(float seconds, float pixels)
        {
            if (pixels < shakeAmount * ShakeLeft) return;
            shakeAmount = pixels;
            shakeTime = shakeTotal = Mathf.Max(0.01f, seconds);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (warming) warming = Sfx.WarmUpStep();
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame) Snapshot();
#endif
            Anim.Tick(dt);
            Layout();
            var off = Vector2.zero;
            if (shakeTime > 0)
            {
                shakeTime -= dt;
                off = UnityEngine.Random.insideUnitCircle * shakeAmount * ShakeLeft;
                if (shakeTime <= 0) shakeAmount = 0;
            }
            if (kick != Vector2.zero || kickVelocity != Vector2.zero)
            {
                // A stiff spring, a little under-damped: back in about a tenth of a second with a small bounce. Stepped in
                // small fixed steps: one big step at a low frame rate would overshoot further each frame and blow up.
                const float omega = 40, damping = 0.6f, step = 1 / 240f;
                for (var left = Mathf.Min(dt, 0.1f); left > 0; left -= step)
                {
                    var h = Mathf.Min(step, left);
                    kickVelocity += (-omega * omega * kick - 2 * damping * omega * kickVelocity) * h;
                    kick += kickVelocity * h;
                }
                if (kick.sqrMagnitude < 0.01f && kickVelocity.sqrMagnitude < 1) kick = kickVelocity = Vector2.zero;
            }
            off += new Vector2(kick.x, -kick.y);
            if (shakeTime > 0 || off != Vector2.zero || Camera.transform.position != cameraHome)
                Camera.transform.position = cameraHome + (Vector3)(off / Design.PixelsPerUnit);
            if (Screen != null)
            {
                try { Screen.Tick(dt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void LateUpdate()
        {
            if (DesignRoot != null) UiSmooth.Pass(DesignRoot, DesignRoot.resolvedStyle.scale.value.x);
        }

        /// <summary>Fit the design area in the window (black bars on the sides) and center the UI over it.</summary>
        void Layout()
        {
            Camera.orthographicSize = FitSize / zoom;
            // Bars cover everything outside the design area (in camera space, so they follow pans, zooms and shakes).
            var size = Camera.orthographicSize;
            var halfH = size;
            var halfW = size * Camera.aspect;
            var innerW = Design.Width / 2 / Design.PixelsPerUnit / zoom;
            var innerH = Design.Height / 2 / Design.PixelsPerUnit / zoom;
            void Bar(SpriteRenderer b, float cx, float cy, float w, float h)
            {
                b.enabled = w > 0.001f && h > 0.001f;
                b.transform.localPosition = new Vector3(cx, cy, 1);
                // Art.White is 1 design px (0.01 units) across.
                b.transform.localScale = new Vector3(w * Design.PixelsPerUnit, h * Design.PixelsPerUnit, 1);
            }
            var side = Mathf.Max(0, halfW - innerW);
            var cap = Mathf.Max(0, halfH - innerH);
            Bar(bars[0], -(innerW + side / 2), 0, side + 0.02f, halfH * 2 + 0.02f);
            Bar(bars[1], innerW + side / 2, 0, side + 0.02f, halfH * 2 + 0.02f);
            Bar(bars[2], 0, innerH + cap / 2, halfW * 2 + 0.02f, cap + 0.02f);
            Bar(bars[3], 0, -(innerH + cap / 2), halfW * 2 + 0.02f, cap + 0.02f);
            if (side < 0.001f) bars[0].enabled = bars[1].enabled = false;
            if (cap < 0.001f) bars[2].enabled = bars[3].enabled = false;

            var panel = document.rootVisualElement.layout;
            if (float.IsNaN(panel.width) || panel.width <= 0) return;
            var scale = Mathf.Min(panel.width / Design.Width, panel.height / Design.Height);
            DesignRoot.style.left = (panel.width - Design.Width) / 2;
            DesignRoot.style.top = (panel.height - Design.Height) / 2;
            DesignRoot.style.scale = new Scale(new Vector3(scale, scale, 1));
            DesignRoot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
        }

        /// <summary>A save file's text from the browser's file picker (SendMessage from Plugins/WebGL/SaveFile.jslib).</summary>
        public void OnSaveFile(string text) => Saves.Picked(text);

        /// <summary>
        /// Save the next finished frame (world and UI) as a PNG. F12 saves one to the persistent data folder; tests and
        /// chats pass a path.
        /// </summary>
        public void Snapshot(string path = null)
        {
            path ??= Path.Combine(Application.persistentDataPath, $"drakepg-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            StartCoroutine(TakeSnapshot(path));
        }

        IEnumerator TakeSnapshot(string path)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log("drakepg: snapshot " + path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Snapshot failed: " + e.Message);
            }
            Destroy(tex);
        }

        /// <summary>The pointer in design pixels (null outside the window).</summary>
        public Vector2? PointerDesign()
        {
            var p = Pointer.current;
            if (p == null) return null;
            var screen = p.position.ReadValue();
            var world = Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            return Design.FromWorld(world);
        }

        /// <summary>True when the pointer is over a UI element (buttons, panels), so world clicks should be ignored.</summary>
        public bool PointerOverUi()
        {
            var p = Pointer.current;
            if (p == null || document.rootVisualElement.panel == null) return false;
            var screen = p.position.ReadValue();
            var panelPos = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            var picked = document.rootVisualElement.panel.Pick(panelPos);
            return picked != null && picked != document.rootVisualElement && picked != DesignRoot && picked.pickingMode == PickingMode.Position;
        }
    }
}
