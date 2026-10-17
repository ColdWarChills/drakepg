using System;
using System.Collections.Generic;
using Drakepg.Rules;
using UnityEngine;

namespace Drakepg
{
    /// <summary>A button under a dialogue's last page. `Run` gets a `close` action that shuts the dialogue.</summary>
    public class DialogueAction
    {
        public string Label, Sublabel;
        public bool Enabled = true;
        public Action<Action> Run;

        public DialogueAction(string label, Action<Action> run, string sublabel = null, bool enabled = true)
        {
            Label = label;
            Run = run;
            Sublabel = sublabel;
            Enabled = enabled;
        }
    }

    /// <summary>NPC dialogue and narration boxes (openDialogue / openNarration in src/ui/panels.ts).</summary>
    public static class Dialogue
    {
        /// <summary>NPC dialogue box. Pages through `lines` (default: the NPC's own), then shows the actions.</summary>
        public static UiModal Open(NpcDef npc, List<DialogueAction> actions, List<string> lines = null) =>
            PagedText($"{npc.Name}, {npc.Title}", lines ?? npc.Dialogue, actions, "Goodbye");

        /// <summary>Narration with no speaker: a note you read, a campfire. Same box as dialogue.</summary>
        public static UiModal Narration(string title, List<string> lines, List<DialogueAction> actions = null, string closeLabel = "Close") =>
            PagedText(title, lines, actions ?? new List<DialogueAction>(), closeLabel);

        static UiModal PagedText(string title, List<string> lines, List<DialogueAction> actions, string closeLabel)
        {
            var modal = new UiModal(860, 320, title);
            var line = 0;
            if (lines == null || lines.Count == 0) lines = new List<string> { "" };

            void Render()
            {
                modal.Clear();
                var c = modal.Content;
                var text = Ui.Text(c, 50, 80, lines[line], 23, wrap: 760);
                text.style.unityParagraphSpacing = 6;
                var isLast = line >= lines.Count - 1;
                var buttons = isLast
                    ? new List<DialogueAction>(actions) { new DialogueAction(closeLabel, _ => modal.Close()) }
                    : new List<DialogueAction> { new DialogueAction("Next", _ => { line += 1; Render(); }) };
                const float width = 220, gap = 16;
                var total = buttons.Count * width + (buttons.Count - 1) * gap;
                for (int i = 0; i < buttons.Count; i++)
                {
                    var b = buttons[i];
                    var x = (860 - total) / 2 + width / 2 + i * (width + gap);
                    var btn = new UiButton(c, x, 250, b.Label, () => b.Run(() => modal.Close()), width, 60, 20, b.Sublabel);
                    if (!b.Enabled) btn.SetEnabled(false);
                }
            }

            Render();
            return modal;
        }
    }
}
