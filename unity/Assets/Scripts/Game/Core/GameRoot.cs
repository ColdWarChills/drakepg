using System;
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
            SetupCamera();
            SetupUi();
        }

        void Start() => Go(new TitleScreen(), 0);

        void SetupCamera()
        {
            // A plain black camera behind everything fills the letterbox bars.
            var back = new GameObject("Letterbox").AddComponent<Camera>();
            back.depth = -10;
            back.clearFlags = CameraClearFlags.SolidColor;
            back.backgroundColor = Color.black;
            back.cullingMask = 0;
            back.transform.SetParent(transform);

            Camera = Camera.main;
            if (Camera == null) Camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = Design.Height / 2 / Design.PixelsPerUnit;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Palette.Bg;
            Camera.transform.position = cameraHome = new Vector3(0, 0, -10);
            Camera.depth = 0;
        }

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
            if (switching) return;
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
        }

        Task FadeTo(float to, float duration)
        {
            var from = fade.resolvedStyle.opacity;
            if (fade.parent == null) DesignRoot.Add(fade);
            fade.BringToFront();
            fade.pickingMode = to > 0 ? PickingMode.Position : PickingMode.Ignore;
            return Anim.Float(from, to, duration, v => fade.style.opacity = v, Ease.Linear, owner: this).Task;
        }

        float BaseSize => Design.Height / 2 / Design.PixelsPerUnit;

        /// <summary>The camera back at the middle, zoom 1, level.</summary>
        public void ResetView()
        {
            viewTween?.Stop();
            cameraHome = new Vector3(0, 0, -10);
            Camera.transform.position = cameraHome;
            Camera.transform.rotation = Quaternion.identity;
            Camera.orthographicSize = BaseSize;
        }

        Anim.Handle viewTween;

        /// <summary>Pan the camera's center to (x, y) design px, zoom and tilt it (cam.pan + zoomTo + rotateTo).</summary>
        public Anim.Handle View(float x, float y, float zoom, float duration, Ease ease = Ease.CubicOut, float rotateRadians = 0)
        {
            viewTween?.Stop();
            var fromPos = cameraHome;
            var toPos = Design.World(x, y, -10);
            var fromZoom = BaseSize / Camera.orthographicSize;
            var fromRot = Camera.transform.eulerAngles.z;
            if (fromRot > 180) fromRot -= 360;
            var toRot = -rotateRadians * Mathf.Rad2Deg;
            viewTween = Anim.Float(0, 1, duration, t =>
            {
                cameraHome = Vector3.LerpUnclamped(fromPos, toPos, t);
                if (shakeTime <= 0) Camera.transform.position = cameraHome;
                Camera.orthographicSize = BaseSize / Mathf.LerpUnclamped(fromZoom, zoom, t);
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

        /// <summary>Camera shake in design pixels.</summary>
        public void Shake(float seconds, float pixels)
        {
            shakeTime = Mathf.Max(shakeTime, seconds);
            shakeAmount = Mathf.Max(shakeTime > 0 ? shakeAmount : 0, pixels);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            Anim.Tick(dt);
            Layout();
            if (shakeTime > 0)
            {
                shakeTime -= dt;
                var off = UnityEngine.Random.insideUnitCircle * shakeAmount / Design.PixelsPerUnit;
                Camera.transform.position = cameraHome + (Vector3)off;
                if (shakeTime <= 0)
                {
                    Camera.transform.position = cameraHome;
                    shakeAmount = 0;
                }
            }
            if (Screen != null)
            {
                try { Screen.Tick(dt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>Letterbox the camera to 16:9 and center the UI over it.</summary>
        void Layout()
        {
            float aspect = (float)UnityEngine.Screen.width / UnityEngine.Screen.height;
            const float target = Design.Width / Design.Height;
            Camera.rect = aspect > target
                ? new Rect((1 - target / aspect) / 2, 0, target / aspect, 1)
                : new Rect(0, (1 - aspect / target) / 2, 1, aspect / target);
            var panel = document.rootVisualElement.layout;
            if (float.IsNaN(panel.width) || panel.width <= 0) return;
            var scale = Mathf.Min(panel.width / Design.Width, panel.height / Design.Height);
            DesignRoot.style.left = (panel.width - Design.Width) / 2;
            DesignRoot.style.top = (panel.height - Design.Height) / 2;
            DesignRoot.style.scale = new Scale(new Vector3(scale, scale, 1));
            DesignRoot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
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
