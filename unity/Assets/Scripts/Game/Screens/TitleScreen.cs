using Drakepg.Rules;
using UnityEngine;

namespace Drakepg
{
    /// <summary>Title: New Game, Continue (TitleScene.ts), and Import save (a file the web version's Export save made).</summary>
    public class TitleScreen : GameScreen
    {
        /// <summary>A line to show once the title is up ("Imported Aria, Lv 7").</summary>
        readonly string note;

        public TitleScreen(string note = null) => this.note = note;

        public override void Enter()
        {
            var bg = Backdrop.Draw(World, new[] { "bg_title" });
            // Darken a little so the logo and buttons stay readable.
            if (bg != null) Backdrop.Rect(World, Design.Width / 2, Design.Height / 2, Design.Width, Design.Height, new Color(0, 0, 0, 0.25f), -31000);
            Fx.StarField(World, 70, Design.Height * 0.55f);
            Fx.Embers(World);
            const float cx = Design.Width / 2;

            // The name in Alegreya, cream with an ink outline like a map's lettering.
            var logo = Ui.Text(Root, cx, 170, "DRAKEPG", 104, Design.Hex(0xf6e2b0), false, 0.5f, 0.5f, stroke: true);
            Fonts.Title(logo);
            logo.style.unityTextOutlineColor = Felt.Ink;
            logo.style.unityTextOutlineWidth = 0.14f;
            logo.style.opacity = 0;
            Anim.Float(0.6f, 1, 0.6f, v =>
            {
                logo.style.scale = new UnityEngine.UIElements.Scale(Vector3.one * v);
                logo.style.opacity = Mathf.InverseLerp(0.6f, 1, v);
            }, Ease.BackOut).OnComplete(() => Anim.Float(170, 178, 2.2f, v => logo.style.top = v, Ease.SineInOut, repeat: -1, yoyo: true));
            var tagline = Ui.Text(Root, cx, 252, "a turn-based adventure", 26, Design.Hex(0xd9c7a0), false, 0.5f, 0.5f, stroke: true);
            Fonts.Title(tagline);
            tagline.style.unityTextOutlineColor = Felt.Ink;

            var hasSave = Saves.HasSave;
            new UiButton(Root, cx, 380, "New Game", () =>
            {
                if (hasSave) ConfirmOverwrite();
                else StartNew();
            }, 280, 64, 26);
            new UiButton(Root, cx, 465, "Continue", ContinueGame, 280, 64, 26).SetEnabled(hasSave);
            // The browser opens a file picker only inside a click, so there it's asked for on the press (Saves.PickFile).
            Saves.PreparePicker();
            var import =new UiButton(Root, cx, 548, "Import save", () => { if (!Saves.PickOnPress) Saves.PickFile(Imported); }, 220, 44, 18);
            import.RegisterCallback<UnityEngine.UIElements.PointerDownEvent>(_ => { if (Saves.PickOnPress) Saves.PickFile(Imported); });
            Ui.Text(Root, cx, Design.Height - 30, "v0.8 · Chapter 3 · Unity", 16, Palette.TextDim, ax: 0.5f, ay: 0.5f);
            if (note != null) Ui.Toast(note, Palette.TextGood);
        }

        /// <summary>A picked file: a save from the web version (Export save on its title) becomes this game's save.</summary>
        void Imported(string text)
        {
            var player = SaveFile.Read(text);
            if (player == null)
            {
                var bad = new UiModal(640, 260, "Can't import");
                Ui.Text(bad.Content, 320, 100, "That file isn't a drakepg save.", 22, ax: 0.5f, ay: 0.5f);
                new UiButton(bad.Content, 320, 190, "OK", () => bad.Close(), 220);
                return;
            }
            var current = Saves.HasSave ? Saves.Load() : null;
            if (current == null)
            {
                Keep(player);
                return;
            }
            var modal = new UiModal(680, 280, "Replace your save?");
            Ui.Text(modal.Content, 340, 110, $"{current.Name} (Lv {current.Level}) is replaced by\n{player.Name} (Lv {player.Level}) from the file.", 22,
                ax: 0.5f, ay: 0.5f, align: TextAnchor.MiddleCenter);
            new UiButton(modal.Content, 210, 210, "Import", () => Keep(player), 220);
            new UiButton(modal.Content, 470, 210, "Cancel", () => modal.Close(), 220);
        }

        static void Keep(Player player)
        {
            var ok = Saves.Save(player);
            GameRoot.I.Go(new TitleScreen(ok ? $"Imported {player.Name}, Lv {player.Level}" : "Could not save the imported hero."));
        }

        void StartNew() => GameRoot.I.Go(new CreateScreen());

        void ContinueGame()
        {
            var player = Saves.Load();
            if (player == null)
            {
                StartNew();
                return;
            }
            Session.Start(player);
            var at = player.Data.Location;
            GameRoot.I.Go(new RoomScreen(RoomArrival.Spawn(at.RoomId, (float)at.X, (float)at.Y)));
        }

        void ConfirmOverwrite()
        {
            var modal = new UiModal(640, 260, "Start over?");
            Ui.Text(modal.Content, 320, 100, "Your current save is replaced once you begin.", 22, ax: 0.5f, ay: 0.5f);
            new UiButton(modal.Content, 200, 190, "New character", StartNew, 220);
            new UiButton(modal.Content, 440, 190, "Cancel", () => modal.Close(), 220);
        }
    }
}
