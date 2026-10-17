using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Drakepg
{
    /// <summary>
    /// A one-line text box (the hero's name). Reads typed characters from the Input System (`Keyboard.onTextInput`), so
    /// it works the same in the editor, Windows and the browser, where UI Toolkit's TextField didn't take input.
    /// Click it to type; Backspace deletes; Esc or a click elsewhere stops typing. The screen calls `Tick` every frame.
    /// </summary>
    public class NameBox : VisualElement
    {
        /// <summary>The box being typed in, if any (keys like 1-3 or WASD are ignored meanwhile: see Keys.Typing).</summary>
        public static NameBox Focused { get; private set; }

        readonly Label label;
        readonly string placeholder;
        readonly int maxLength;
        string value = "";
        bool focused, caret;
        float blink;

        public NameBox(VisualElement parent, float x, float y, float width, float height, string placeholder, int maxLength, float fontSize = 24)
        {
            this.placeholder = placeholder;
            this.maxLength = maxLength;
            pickingMode = PickingMode.Position;
            Ui.Place(this, x, y);
            Ui.Size(this, width, height);
            Ui.Round(this, 10);
            style.backgroundColor = Palette.Panel;
            style.justifyContent = Justify.Center;
            label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            Add(label);
            RegisterCallback<PointerDownEvent>(e =>
            {
                Focus();
                e.StopPropagation();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => Unfocus());
            parent?.Add(this);
            Paint();
        }

        public string Value
        {
            get => value;
            set
            {
                this.value = (value ?? "").Length > maxLength ? value.Substring(0, maxLength) : value ?? "";
                Paint();
            }
        }

        public new void Focus()
        {
            if (Focused == this) return;
            Focused?.Unfocus();
            Focused = this;
            focused = true;
            caret = true;
            blink = 0;
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnText;
            Paint();
        }

        public void Unfocus()
        {
            if (!focused) return;
            focused = false;
            if (Focused == this) Focused = null;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
            Paint();
        }

        void OnText(char c)
        {
            if (char.IsControl(c) || value.Length >= maxLength) return;
            if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '\'') return;
            value += c;
            caret = true;
            blink = 0;
            Paint();
        }

        /// <summary>Backspace, Esc and the blinking caret.</summary>
        public void Tick(float dt)
        {
            if (!focused) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.backspaceKey.wasPressedThisFrame && value.Length > 0)
                {
                    value = value.Substring(0, value.Length - 1);
                    caret = true;
                    blink = 0;
                }
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    Unfocus();
                    return;
                }
            }
            blink += dt;
            if (blink >= 0.5f)
            {
                blink = 0;
                caret = !caret;
            }
            Paint();
        }

        void Paint()
        {
            Ui.Border(this, 2, focused ? Palette.Gold : Palette.PanelBorder);
            var empty = value.Length == 0;
            label.style.color = empty && !focused ? Palette.TextDim : Palette.Text;
            // The caret takes no room when hidden, so the text doesn't jump.
            label.text = empty && !focused ? placeholder : value + (focused ? (caret ? "|" : "<alpha=#00>|</alpha>") : "");
        }
    }
}
