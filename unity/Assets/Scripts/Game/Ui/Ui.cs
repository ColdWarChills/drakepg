using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// UI building blocks drawn in code with UI Toolkit, positioned in design pixels (src/ui/text.ts, Button.ts, Bar.ts,
    /// Modal.ts, Hud.ts). They wear the felt kit (Ui/Felt.cs): panels and menus are stitched navy felt, buttons sand
    /// felt, bars felt strips in a groove; the colors screens give them are themed for felt (FeltPass).
    /// </summary>
    public static class Ui
    {
        /// <summary>A positioned box: (x, y) is where its anchor point goes (0,0 top-left, 0.5,0.5 center).</summary>
        public static T Place<T>(T e, float x, float y, float ax = 0, float ay = 0) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            if (ax != 0 || ay != 0) e.style.translate = new Translate(Length.Percent(-ax * 100), Length.Percent(-ay * 100));
            return e;
        }

        public static void Size(VisualElement e, float w, float h)
        {
            e.style.width = w;
            e.style.height = h;
        }

        public static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = color;
        }

        /// <summary>Text (addText). `wrap` > 0 wraps at that width. Rich text tags (&lt;color&gt;, &lt;b&gt;) work.</summary>
        public static Label Text(VisualElement parent, float x, float y, string text, float size = 20, Color? color = null, bool bold = false,
            float ax = 0, float ay = 0, float wrap = 0, bool stroke = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color ?? Palette.Text;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.unityTextAlign = align;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            if (wrap > 0)
            {
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.width = wrap;
            }
            else l.style.whiteSpace = WhiteSpace.NoWrap;
            if (stroke)
            {
                l.style.unityTextOutlineWidth = Mathf.Clamp(size / 60f, 0.12f, 0.3f);
                l.style.unityTextOutlineColor = Color.black;
            }
            Place(l, x, y, ax, ay);
            parent?.Add(l);
            return l;
        }

        /// <summary>A filled, optionally rounded and bordered rectangle (top-left at x, y).</summary>
        public static VisualElement Rect(VisualElement parent, float x, float y, float w, float h, Color color, float radius = 0, float border = 0,
            Color? borderColor = null, bool blocksClicks = false)
        {
            var e = new VisualElement { pickingMode = blocksClicks ? PickingMode.Position : PickingMode.Ignore };
            Place(e, x, y);
            Size(e, w, h);
            e.style.backgroundColor = color;
            if (radius > 0) Round(e, radius);
            if (border > 0) Border(e, border, borderColor ?? Palette.PanelBorder);
            parent?.Add(e);
            return e;
        }

        /// <summary>The standard panel: a stitched navy felt patch (`alpha` is from the old dark panels, unused).</summary>
        public static VisualElement Panel(VisualElement parent, float x, float y, float w, float h, float alpha = 0.92f, bool blocksClicks = true) =>
            Sheet(parent, x, y, w, h, blocksClicks);

        /// <summary>
        /// A felt patch at (x, y) top-left, w x h: fuzzy edged, running stitches inside (`patch`: the panel look by default).
        /// Content goes straight in it; the Palette colors of what's inside are themed for felt (FeltPass).
        /// </summary>
        public static VisualElement Sheet(VisualElement parent, float x, float y, float w, float h, bool blocksClicks = true, Felt.Patch patch = null,
            int seed = 0)
        {
            patch ??= Felt.Patch.Panel();
            var e = new VisualElement { pickingMode = blocksClicks ? PickingMode.Position : PickingMode.Ignore, name = "sheet" };
            Place(e, x, y);
            Size(e, w, h);
            e.AddToClassList(FeltPass.Class);
            var bg = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(bg, -patch.Pad, -patch.Pad);
            Size(bg, w + patch.Pad * 2, h + patch.Pad * 2);
            bg.style.backgroundImage = new StyleBackground(Felt.PatchTexture(w, h, seed != 0 ? seed : Seed(w, h), patch));
            e.Add(bg);
            parent?.Add(e);
            return e;
        }

        /// <summary>A seed from a size or a text, so the same panel or button always tears the same way.</summary>
        public static int Seed(float w, float h) => 100 + (int)(w * 7 + h * 13) % 900;

        public static int Seed(string text)
        {
            var n = 17;
            foreach (var ch in text ?? "") n = (n * 31 + ch) % 100003;
            return 300 + n % 600;
        }

        /// <summary>An image from the art (a sprite key), fit inside w x h.</summary>
        public static VisualElement Image(VisualElement parent, float x, float y, float w, float h, string key, float ax = 0, float ay = 0)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(e, x, y, ax, ay);
            Size(e, w, h);
            var s = Art.Get(key);
            if (s != null)
            {
                e.style.backgroundImage = new StyleBackground(s);
                e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            parent?.Add(e);
            return e;
        }

        /// <summary>Short message near the top of the screen; several stack downward (toast()).</summary>
        public static void Toast(string text, Color? color = null)
        {
            var screen = GameRoot.I.Screen;
            if (screen?.Root == null) return;
            int slot = toasts.TryGetValue(screen, out var n) ? n : 0;
            toasts[screen] = slot + 1;
            var y = 110 + slot * 38;
            var label = Text(screen.Root, Design.Width / 2, y, text, 24, color, true, 0.5f, 0.5f, stroke: true);
            label.style.opacity = 0;
            Anim.Float(0, 1, 0.2f, v =>
            {
                label.style.opacity = v;
                label.style.top = y - 10 * v;
            });
            Anim.Float(0, 1, 0.3f, v =>
            {
                label.style.opacity = 1 - v;
                label.style.top = y - 10 - 10 * v;
            }, Ease.Linear, 1.8f).OnComplete(() =>
            {
                label.RemoveFromHierarchy();
                toasts[screen] = Math.Max(0, (toasts.TryGetValue(screen, out var m) ? m : 1) - 1);
            });
        }

        static readonly Dictionary<GameScreen, int> toasts = new Dictionary<GameScreen, int>();

        /// <summary>Damage numbers, "+40 HP": pops in, drifts up and fades (floatText()).</summary>
        public static void FloatText(float x, float y, string text, Color color, float size = 32, string tag = null)
        {
            var screen = GameRoot.I.Screen;
            if (screen?.Root == null) return;
            var jitter = UnityEngine.Random.Range(-24f, 24f);
            var label = Text(screen.Root, x + jitter, y, text, size, color, true, 0.5f, 0.5f, stroke: true);
            label.style.scale = new Scale(Vector3.one * 0.3f);
            Anim.Float(0.3f, 1.3f, 0.11f, v => label.style.scale = new Scale(Vector3.one * v), Ease.BackOut);
            Anim.Float(1.3f, 1f, 0.12f, v => label.style.scale = new Scale(Vector3.one * v), Ease.Linear, 0.11f);
            Anim.Float(0, 1, 0.8f, v =>
            {
                label.style.top = y - 80 * v;
                label.style.left = x + jitter + jitter * 0.6f * v;
                label.style.opacity = 1 - v;
            }, Ease.CubicIn, 0.48f).OnComplete(label.RemoveFromHierarchy);
            if (tag == null) return;
            var t = Text(screen.Root, x + jitter, y - size * 0.9f, tag, Mathf.Round(size * 0.5f), Palette.TextGold, true, 0.5f, 0.5f, stroke: true);
            t.style.rotate = new Rotate(-8);
            Anim.Float(0, 1, 0.7f, v =>
            {
                t.style.top = y - size * 0.9f - 60 * v;
                t.style.opacity = 1 - v;
            }, Ease.Linear, 0.5f).OnComplete(t.RemoveFromHierarchy);
        }

        public static string Css(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Wrap text in a color tag.</summary>
        public static string Colored(string text, Color c) => $"<color={Css(c)}>{text}</color>";
    }

    /// <summary>
    /// A button (Button.ts): a sand felt patch with brown running stitches, an Alegreya label and an optional second
    /// line; a color of its own (a selected tab) dyes the felt toward it. Lifts on hover, presses down. (x, y) is its center.
    /// </summary>
    public class UiButton : VisualElement
    {
        readonly Label label;
        readonly Label sub;
        readonly VisualElement sheet;
        readonly float width, height;
        readonly int seed;
        readonly Action onClick;
        Color baseColor;
        bool enabled = true, hover, down;

        public UiButton(VisualElement parent, float x, float y, string text, Action onClick, float width = 200, float height = 56, float fontSize = 22,
            string sublabel = null, Color? color = null)
        {
            this.onClick = onClick;
            this.width = width;
            this.height = height;
            seed = Ui.Seed(text);
            baseColor = color ?? Palette.Button;
            Ui.Place(this, x - width / 2, y - height / 2);
            Ui.Size(this, width, height);
            AddToClassList(FeltPass.Keep);
            style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;
            sheet = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(sheet, -10, -10);
            Ui.Size(sheet, width + 20, height + 20);
            Add(sheet);
            label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = Felt.Brown;
            Fonts.Title(label);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingTop = label.style.paddingBottom = 0;
            Add(label);
            if (sublabel != null)
            {
                sub = new Label(sublabel) { pickingMode = PickingMode.Ignore };
                sub.style.fontSize = 15;
                sub.style.color = Felt.BrownDim;
                Fonts.Bold(sub);
                sub.style.unityTextAlign = TextAnchor.MiddleCenter;
                sub.style.marginTop = sub.style.marginBottom = 0;
                sub.style.paddingTop = sub.style.paddingBottom = 0;
                Add(sub);
            }
            RegisterCallback<PointerEnterEvent>(_ => { hover = true; Paint(); });
            RegisterCallback<PointerLeaveEvent>(_ => { hover = down = false; Paint(); });
            RegisterCallback<PointerDownEvent>(_ =>
            {
                if (!enabled) return;
                down = true;
                Paint();
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                var was = down;
                down = false;
                Paint();
                if (!enabled || !was || GameRoot.I.Screen?.InputEnabled == false) return;
                Sfx.Play("click");
                e.StopPropagation();
                this.onClick?.Invoke();
            });
            Paint();
            parent?.Add(this);
        }

        /// <summary>The plain button color means sand felt; any other color (a selected tab) dyes the felt toward it.</summary>
        Color Cloth()
        {
            var b = Palette.Button;
            if (Mathf.Abs(baseColor.r - b.r) < 0.01f && Mathf.Abs(baseColor.g - b.g) < 0.01f && Mathf.Abs(baseColor.b - b.b) < 0.01f) return Felt.Sand;
            return Color.Lerp(Felt.Sand, baseColor, 0.45f);
        }

        void Paint()
        {
            sheet.style.backgroundImage = new StyleBackground(Felt.PatchTexture(width, height, seed, Felt.Patch.Button(Cloth())));
            style.translate = new Translate(0, !enabled ? 0 : down ? 1 : hover ? -2 : 0);
            style.scale = new Scale(Vector3.one * (down && enabled ? 0.97f : 1));
            style.opacity = enabled ? 1 : 0.5f;
        }

        public new UiButton SetEnabled(bool on)
        {
            enabled = on;
            Paint();
            return this;
        }

        public bool Enabled => enabled;

        public UiButton SetText(string text, string sublabel = null)
        {
            label.text = text;
            if (sub != null && sublabel != null) sub.text = sublabel;
            return this;
        }

        public UiButton SetColor(Color c)
        {
            baseColor = c;
            Paint();
            return this;
        }

        public void Click()
        {
            if (enabled) onClick?.Invoke();
        }
    }

    /// <summary>
    /// HP / MP / XP bar (Bar.ts): a felt strip in a dark felt groove, the "cur / max" label in cream, and a pale trail
    /// of the strip when it drops. (x, y) = left edge, middle. Its own colors stay (FeltPass leaves it alone).
    /// </summary>
    public class UiBar : VisualElement
    {
        readonly VisualElement fill, chip;
        readonly Label label;
        readonly float inner;
        readonly string prefix;
        float shown = -1, chipShown = -1;
        Anim.Handle fillTween, chipTween;

        static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        public UiBar(VisualElement parent, float x, float y, float width, float height, Color color, string prefix = "")
        {
            this.prefix = prefix;
            var cloth = Same(color, Palette.Hp) ? Felt.Hp : Same(color, Palette.Mp) ? Felt.Mp : Same(color, Palette.Xp) ? Design.Hex(0xc9a227) : color;
            Ui.Place(this, x, y - height / 2);
            Ui.Size(this, width, height);
            pickingMode = PickingMode.Ignore;
            AddToClassList(FeltPass.Keep);
            var seed = Ui.Seed(prefix) + (int)width;
            var groove = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(groove, -2, -2);
            Ui.Size(groove, width + 4, height + 4);
            groove.style.backgroundImage = new StyleBackground(Felt.GrooveTexture(width, height, seed));
            Add(groove);
            inner = width - 6;
            chip = Strip(Felt.StripTexture(inner, height - 6, Color.Lerp(cloth, Felt.Groove, 0.55f), seed + 1), height);
            fill = Strip(Felt.StripTexture(inner, height - 6, cloth, seed + 2), height);
            label = Ui.Text(this, width / 2, height / 2, "", Mathf.Max(12, height - 8), Felt.Cream, false, 0.5f, 0.5f);
            Fonts.Heavy(label);
            label.style.unityTextOutlineWidth = 0.2f;
            label.style.unityTextOutlineColor = Design.Hex(0x1a1410);
            parent?.Add(this);
        }

        /// <summary>A felt strip in its own clip: it slides in from the left, so its rounded right end always shows.</summary>
        VisualElement Strip(Texture2D tex, float height)
        {
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(clip, 3, 3);
            Ui.Size(clip, inner, height - 6);
            clip.style.overflow = Overflow.Hidden;
            Ui.Round(clip, (height - 6) / 2);
            Add(clip);
            var strip = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(strip, -inner, 0);
            Ui.Size(strip, inner, height - 6);
            strip.style.backgroundImage = new StyleBackground(tex);
            clip.Add(strip);
            return strip;
        }

        void ShowFill(float w) => fill.style.left = w - inner;

        void ShowChip(float w)
        {
            chipShown = w;
            chip.style.left = w - inner;
        }

        public UiBar SetValue(double current, double max, bool animate = true)
        {
            var ratio = max > 0 ? Mathf.Clamp01((float)(current / max)) : 0;
            var target = inner * ratio;
            var from = shown < 0 ? target : shown;
            fillTween?.Stop();
            chipTween?.Stop();
            if (!animate || shown < 0)
            {
                ShowFill(target);
                ShowChip(target);
            }
            else if (target < from)
            {
                fillTween = Anim.Float(from, target, 0.12f, ShowFill);
                chipTween = Anim.Float(chipShown < 0 ? from : chipShown, target, 0.45f, ShowChip, Ease.CubicIn, 0.35f);
            }
            else
            {
                ShowChip(target);
                fillTween = Anim.Float(from, target, 0.4f, ShowFill);
            }
            shown = target;
            label.text = $"{prefix}{Math.Max(0, Math.Round(current))} / {max}";
            return this;
        }
    }

    /// <summary>
    /// Dimmed overlay + a centered navy felt patch with a title; content goes in `Content`, positioned from the panel's
    /// top-left (Modal.ts). The shade swallows clicks. Modals stack; Esc closes the top one. The X is a sewing button.
    /// </summary>
    public class UiModal
    {
        public readonly VisualElement Root, Panel, Content;
        public readonly float Width, Height;
        public bool Closed { get; private set; }
        readonly Action onClose;

        static readonly List<UiModal> open = new List<UiModal>();
        public static int OpenCount { get { open.RemoveAll(m => m.Closed || m.Root.panel == null); return open.Count; } }
        public static UiModal Top { get { open.RemoveAll(m => m.Closed || m.Root.panel == null); return open.Count > 0 ? open[open.Count - 1] : null; } }

        public UiModal(float width, float height, string title, Action onClose = null, bool closeButton = true)
        {
            Width = width;
            Height = height;
            this.onClose = onClose;
            var parent = GameRoot.I.Screen.Root;
            Root = new VisualElement { pickingMode = PickingMode.Position };
            Ui.Place(Root, 0, 0);
            Ui.Size(Root, Design.Width, Design.Height);
            Root.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.6f);
            parent.Add(Root);
            var left = (Design.Width - width) / 2;
            var top = (Design.Height - height) / 2;
            Panel = Ui.Sheet(Root, left, top, width, height, true, Felt.Patch.Menu());
            var heading = Ui.Text(Panel, width / 2, 32, title, 30, Felt.Text, false, 0.5f, 0.5f);
            Fonts.Title(heading);
            Content = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Place(Content, 0, 0);
            Ui.Size(Content, width, height);
            Panel.Add(Content);
            if (closeButton && onClose != null) CloseSeal(Panel, width - 50, 12, () => Close(onClose));
            Sfx.Play("open");
            Root.style.opacity = 0;
            Anim.Float(0, 1, 0.15f, v => Root.style.opacity = v, Ease.Linear);
            Anim.Float(24, 0, 0.2f, v => Panel.style.top = top + v, Ease.BackOut);
            open.Add(this);
        }

        /// <summary>The close button: a sewing button with an X, a little bigger on hover.</summary>
        static void CloseSeal(VisualElement parent, float x, float y, Action run)
        {
            var hit = new VisualElement { pickingMode = PickingMode.Position, name = "close" };
            Ui.Place(hit, x, y);
            Ui.Size(hit, 38, 38);
            hit.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            Felt.SewnButton(hit, 4, 4, "X", 30, 16);
            hit.RegisterCallback<PointerEnterEvent>(_ => hit.style.scale = new Scale(Vector3.one * 1.1f));
            hit.RegisterCallback<PointerLeaveEvent>(_ => hit.style.scale = new Scale(Vector3.one));
            hit.RegisterCallback<PointerUpEvent>(e =>
            {
                if (GameRoot.I.Screen?.InputEnabled == false) return;
                Sfx.Play("click");
                e.StopPropagation();
                run();
            });
            parent.Add(hit);
        }

        public void Clear() => Content.Clear();

        public void Close(Action after = null)
        {
            if (Closed) return;
            Closed = true;
            Root.RemoveFromHierarchy();
            open.Remove(this);
            after?.Invoke();
        }

        /// <summary>Esc: close with the close action (if it has an X button), else just close.</summary>
        public void Escape() => Close(onClose);
    }

    /// <summary>Top-left status block on a felt patch: name, level, HP, MP, XP, gold (Hud.ts).</summary>
    public class Hud
    {
        readonly Label nameText, goldText;
        readonly UiBar hp, mp, xp;
        public readonly VisualElement Root;

        public Hud(VisualElement parent, float x, float y)
        {
            Root = Ui.Panel(parent, x, y, 340, 150, 0.85f, false);
            nameText = Ui.Text(Root, 18, 10, "", 23, Felt.Text);
            Fonts.Title(nameText);
            goldText = Ui.Text(Root, 322, 13, "", 20, Felt.Gold, false, 1, 0);
            Fonts.Heavy(goldText);
            hp = new UiBar(Root, 16, 58, 308, 24, Palette.Hp, "HP ");
            mp = new UiBar(Root, 16, 90, 308, 24, Palette.Mp, "MP ");
            xp = new UiBar(Root, 16, 124, 308, 18, Palette.Xp, "XP ");
        }

        public void Refresh(Rules.Player player, bool animate = true)
        {
            nameText.text = $"{player.Name}  ·  Lv {player.Level}";
            goldText.text = $"{player.Gold} g";
            hp.SetValue(player.Hp, player.MaxHp, animate);
            mp.SetValue(player.Mp, player.MaxMp, animate);
            xp.SetValue(player.Xp, player.XpNeeded, animate);
        }
    }
}
