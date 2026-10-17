using Drakepg.Rules;
using UnityEngine;

namespace Drakepg
{
    /// <summary>Title: New Game, Continue (TitleScene.ts).</summary>
    public class TitleScreen : GameScreen
    {
        public override void Enter()
        {
            var bg = Backdrop.Draw(World, new[] { "bg_title" });
            // Darken a little so the logo and buttons stay readable.
            if (bg != null) Backdrop.Rect(World, Design.Width / 2, Design.Height / 2, Design.Width, Design.Height, new Color(0, 0, 0, 0.25f), -31000);
            Fx.StarField(World, 70, Design.Height * 0.55f);
            Fx.Embers(World);
            const float cx = Design.Width / 2;

            var logo = Ui.Text(Root, cx, 170, "DRAKEPG", 96, Palette.TextGold, true, 0.5f, 0.5f, stroke: true);
            logo.style.opacity = 0;
            Anim.Float(0.6f, 1, 0.6f, v =>
            {
                logo.style.scale = new UnityEngine.UIElements.Scale(Vector3.one * v);
                logo.style.opacity = Mathf.InverseLerp(0.6f, 1, v);
            }, Ease.BackOut).OnComplete(() => Anim.Float(170, 178, 2.2f, v => logo.style.top = v, Ease.SineInOut, repeat: -1, yoyo: true));
            Ui.Text(Root, cx, 250, "a turn-based adventure", 26, Palette.TextDim, ax: 0.5f, ay: 0.5f);

            var hasSave = Saves.HasSave;
            new UiButton(Root, cx, 380, "New Game", () =>
            {
                if (hasSave) ConfirmOverwrite();
                else StartNew();
            }, 280, 64, 26);
            new UiButton(Root, cx, 465, "Continue", ContinueGame, 280, 64, 26).SetEnabled(hasSave);
            Ui.Text(Root, cx, Design.Height - 30, "v0.8 · Chapter 3 · Unity", 16, Palette.TextDim, ax: 0.5f, ay: 0.5f);
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
