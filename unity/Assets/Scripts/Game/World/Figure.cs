using System.Collections.Generic;
using Drakepg.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakepg
{
    /// <summary>
    /// A character on screen, origin at the feet (drawFigure in src/ui/figures.ts): its art scaled to 240 design px
    /// tall (at most 300 wide), the weapon drawn in its fists when both have art, a soft shadow. Sorted by feet y.
    /// Missing art falls back to a simple shape in the look's colors.
    /// </summary>
    public class Figure : MonoBehaviour
    {
        public const float ArtHeight = 240, ArtMaxWidth = 300;

        public SpriteRenderer Body;
        public readonly List<Transform> Weapons = new List<Transform>();
        public readonly List<SpriteRenderer> Renderers = new List<SpriteRenderer>();
        /// <summary>Drawn size in design px (before the look's scale).</summary>
        public float DrawnHeight, DrawnWidth;
        public Look Look;
        public int Facing = 1;
        SortingGroup group;
        Transform art;

        public static Figure Create(Transform parent, float x, float y, Look look, int facing = 1, ItemDef weapon = null, string sigil = null)
        {
            var go = new GameObject("Figure");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<Figure>();
            f.group = go.AddComponent<SortingGroup>();
            f.Look = look;
            f.Build(look, facing, weapon, sigil);
            f.SetFeet(x, y);
            return f;
        }

        void Build(Look look, int facing, ItemDef weapon, string sigil)
        {
            Facing = facing;
            art = new GameObject("art").transform;
            art.SetParent(transform, false);
            var bare = look.Bare?.Find(b => Rules.Db.Loaded && Art.Has(b.Art));
            var weaponArt = weapon?.Art != null && Art.Has(weapon.Art) ? weapon.Art : null;
            if (bare != null && weaponArt != null)
            {
                var sprite = Art.Get(bare.Art);
                var size = Art.Size(sprite);
                var s = Mathf.Min(ArtHeight / size.y, ArtMaxWidth / size.x);
                DrawnHeight = size.y * s;
                DrawnWidth = size.x * s;
                AddShadow(DrawnWidth * 0.7f);
                Body = AddSprite(sprite, art, 10, s);
                var wSprite = Art.Get(weaponArt);
                var wSize = Art.Size(wSprite);
                var grip = (float)(weapon.Grip ?? 0.85);
                foreach (var hand in bare.Hands)
                {
                    var pivot = new GameObject("hand").transform;
                    pivot.SetParent(art, false);
                    pivot.localPosition = new Vector3(((float)hand.X - size.x / 2) * s, (size.y - (float)hand.Y) * s, 0) / Design.PixelsPerUnit;
                    pivot.localRotation = Quaternion.Euler(0, 0, -(float)hand.Angle);
                    var ws = (float)(weapon.Size ?? 0.6) * DrawnHeight / wSize.y;
                    var w = AddSprite(wSprite, pivot, 5, ws);
                    w.transform.localPosition = new Vector3(0, -(0.5f - grip) * wSize.y * ws, 0) / Design.PixelsPerUnit;
                    Weapons.Add(pivot);
                }
            }
            else
            {
                var key = Art.Pick(look.Art);
                if (key != null)
                {
                    var sprite = Art.Get(key);
                    var size = Art.Size(sprite);
                    var s = Mathf.Min(ArtHeight / size.y, ArtMaxWidth / size.x);
                    DrawnHeight = size.y * s;
                    DrawnWidth = size.x * s;
                    AddShadow(DrawnWidth * 0.7f);
                    Body = AddSprite(sprite, art, 10, s);
                }
                else BuildShape(look);
            }
            transform.localScale = new Vector3((float)look.Scale * facing, (float)look.Scale, 1);
        }

        void AddShadow(float width)
        {
            var sh = AddSprite(Art.Soft, art, 0, 1);
            sh.color = new Color(0, 0, 0, 0.45f);
            sh.transform.localScale = new Vector3(width / 64f, 26 / 64f, 1);
            sh.name = "shadow";
        }

        SpriteRenderer AddSprite(Sprite sprite, Transform parent, int order, float scale)
        {
            var go = new GameObject(sprite != null ? sprite.name : "sprite");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(scale, scale, 1);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            Renderers.Add(r);
            return r;
        }

        /// <summary>Placeholder: a body and head in the look's colors.</summary>
        void BuildShape(Look look)
        {
            var color = Design.Hex(look.Color);
            var accent = Design.Hex(look.Accent);
            AddShadow(110);
            float w = look.Shape == "humanoid" ? 60 : 130, h = look.Shape == "humanoid" ? 150 : 90;
            var body = AddSprite(Art.Circle, art, 10, 1);
            body.color = color;
            body.transform.localScale = new Vector3(w / 64, h / 64, 1);
            body.transform.localPosition = new Vector3(0, h / 2, 0) / Design.PixelsPerUnit;
            var head = AddSprite(Art.Circle, art, 11, 1);
            head.color = look.Shape == "humanoid" ? Design.Hex(0xf0c8a0) : accent;
            head.transform.localScale = Vector3.one * (look.Shape == "humanoid" ? 56 / 64f : 50 / 64f);
            head.transform.localPosition = new Vector3(look.Shape == "humanoid" ? 0 : 60, look.Shape == "humanoid" ? h + 20 : h * 0.8f, 0) / Design.PixelsPerUnit;
            var eye = AddSprite(Art.Circle, art, 12, 1);
            eye.color = new Color(0.07f, 0.07f, 0.07f);
            eye.transform.localScale = Vector3.one * (9 / 64f);
            eye.transform.localPosition = head.transform.localPosition + new Vector3(12, 4, 0) / Design.PixelsPerUnit;
            Body = body;
            DrawnHeight = h + 50;
            DrawnWidth = w;
        }

        public Vector2 Feet => Design.FromWorld(transform.position);

        public void SetFeet(float x, float y)
        {
            transform.position = Design.World(x, y);
            group.sortingOrder = Design.Depth(y);
        }

        /// <summary>Sorting order (normally `Design.Depth(feet y)`).</summary>
        public int Order
        {
            get => group.sortingOrder;
            set => group.sortingOrder = value;
        }

        public void SetFacing(int facing)
        {
            Facing = facing;
            var s = transform.localScale;
            transform.localScale = new Vector3(Mathf.Abs(s.x) * facing, s.y, 1);
        }

        /// <summary>Size multiplier on top of the look's scale (1 = normal).</summary>
        public void SetScale(float extra)
        {
            transform.localScale = new Vector3((float)Look.Scale * extra * Facing, (float)Look.Scale * extra, 1);
        }

        /// <summary>Head height in design px above the feet (for names, marks), with the current scale.</summary>
        public float TopY => DrawnHeight * Mathf.Abs(transform.localScale.y);

        public void SetAlpha(float a)
        {
            foreach (var r in Renderers)
            {
                if (r == null || r.name == "shadow") continue;
                var c = r.color;
                c.a = a;
                r.color = c;
            }
        }

        /// <summary>Tint every part (white = normal).</summary>
        public void SetTint(Color c)
        {
            foreach (var r in Renderers)
            {
                if (r == null || r.name == "shadow") continue;
                r.color = new Color(c.r, c.g, c.b, r.color.a);
            }
        }

        /// <summary>The art (body and weapons) without the shadow, for bobbing and lunges.</summary>
        public Transform ArtRoot => art;
    }
}
