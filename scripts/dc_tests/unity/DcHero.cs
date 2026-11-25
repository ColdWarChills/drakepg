using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Test only (2026-11-25, job 1 "our hero on the knight's skeleton"): dress a Dragon Crashers knight instance as
    /// today's hero. Every drawing in its Sprite Library is swapped for the repaint of the same name in PaintDir
    /// (scripts/dc_tests/hero_paint.py: same canvas, pivot and outline; the original's mesh, weights, bones and bind
    /// poses copied), through SpriteLibrary.AddOverride, as DcClothes does for one torso. The knight's sword drawing is
    /// swapped for our Iron Sword (wpn_iron_sword, the grip on the sword slot, its length a share of the body as in our
    /// game), and the shield is turned off (today's hero has none). No bone, weight or pose is changed.
    /// </summary>
    public static class DcHero
    {
        public static string PaintDir = "C:/Users/Epic/Desktop/dragonfab/.claude/worktrees/unity-port/art/tests/dc/hero/paint";
        /// <summary>Our sword's length over the knight's standing body height (our game: size 0.62 of a canvas the body fills to ~93%).</summary>
        public static float SwordShare = 0.67f;
        /// <summary>The Iron Sword's grip, from the top of its drawing (items.ts grip).</summary>
        public static float SwordGrip = 0.78f;

        static readonly HashSet<Sprite> ours = new HashSet<Sprite>();
        static readonly List<Object> made = new List<Object>();

        /// <summary>Sprites put on by Dress (anything else showing on a body part is a knight drawing).</summary>
        public static bool IsOurs(Sprite s) => s != null && ours.Contains(s);

        /// <summary>A copy of `orig` (same pivot, ppu, mesh, weights, bones, bind poses) drawing the pixels of `png`.</summary>
        public static Sprite MakePart(Sprite orig, string png)
        {
            var src = orig.texture;
            var load = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            load.LoadImage(File.ReadAllBytes(png));
            var rect = orig.rect;
            // A repaint may come at twice the knight's pixel size (try 2: thin ink lines survive); it then draws at double ppu.
            float k = load.width == 2 * (int)rect.width && load.height == 2 * (int)rect.height ? 2f : 1f;
            if (load.width != (int)(rect.width * k) || load.height != (int)(rect.height * k))
                throw new System.Exception($"{png} is {load.width}x{load.height}, {orig.name} is {rect.width}x{rect.height}");
            var tex = new Texture2D(load.width, load.height, TextureFormat.RGBA32, src.mipmapCount > 1);
            tex.SetPixels32(load.GetPixels32());
            tex.filterMode = src.filterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = src.anisoLevel;
            tex.Apply(true);
            Object.Destroy(load);
            made.Add(tex);
            var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(orig.pivot.x / rect.width, orig.pivot.y / rect.height), orig.pixelsPerUnit * k, 0, SpriteMeshType.FullRect);
            sp.name = orig.name + " (hero)";
            made.Add(sp);
            int n = orig.GetVertexCount();
            sp.SetVertexCount(n);
            var tr = orig.textureRect;
            using (var pos = new NativeArray<Vector3>(n, Allocator.Temp))
            using (var uv = new NativeArray<Vector2>(n, Allocator.Temp))
            {
                // copied at once: a second read can free the first one's buffer
                var p0 = orig.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();
                var u0 = orig.GetVertexAttribute<Vector2>(VertexAttribute.TexCoord0).ToArray();
                var a = pos; var b = uv;
                for (int i = 0; i < n; i++)
                {
                    a[i] = p0[i];
                    b[i] = new Vector2((u0[i].x * src.width - tr.x) / tr.width, (u0[i].y * src.height - tr.y) / tr.height);
                }
                sp.SetVertexAttribute(VertexAttribute.Position, a);
                sp.SetVertexAttribute(VertexAttribute.TexCoord0, b);
            }
            if (orig.HasVertexAttribute(VertexAttribute.Tangent))
                using (var tg = new NativeArray<Vector4>(orig.GetVertexAttribute<Vector4>(VertexAttribute.Tangent).ToArray(), Allocator.Temp))
                    sp.SetVertexAttribute(VertexAttribute.Tangent, tg);
            if (orig.HasVertexAttribute(VertexAttribute.BlendWeight))
                using (var bw = new NativeArray<BoneWeight>(orig.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray(), Allocator.Temp))
                    sp.SetVertexAttribute(VertexAttribute.BlendWeight, bw);
            using (var idx = new NativeArray<ushort>(orig.GetIndices().ToArray(), Allocator.Temp))
                sp.SetIndices(idx);
            sp.SetBones(orig.GetBones());
            using (var bp = new NativeArray<Matrix4x4>(orig.GetBindPoses().ToArray(), Allocator.Temp))
                sp.SetBindPoses(bp);
            return sp;
        }

        /// <summary>
        /// Every drawing of a Sprite Library as a PNG named Category__Label.png (spaces to _), the input of
        /// scripts/dc_tests/hero_paint.py. The knight: Assets/Test2D/dc/Art/Characters/Knight/SpriteLibrary_Character_Knight.spriteLib
        /// into art/tests/dc/hero/lib (never in git: the knight's own art).
        /// </summary>
        public static string Export(string libraryPath, string dir)
        {
            Directory.CreateDirectory(dir);
            var lib = UnityEditor.AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(libraryPath);
            int n = 0;
            foreach (var c in lib.GetCategoryNames())
                foreach (var l in lib.GetCategoryLabelNames(c))
                {
                    var s = lib.GetSprite(c, l);
                    var src = s.texture;
                    var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(src, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var rc = s.textureRect;
                    var t = new Texture2D((int)rc.width, (int)rc.height, TextureFormat.RGBA32, false);
                    t.ReadPixels(new Rect(rc.x, rc.y, rc.width, rc.height), 0, 0);
                    t.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    File.WriteAllBytes(Path.Combine(dir, (c + "__" + l).Replace(" ", "_") + ".png"), t.EncodeToPNG());
                    Object.DestroyImmediate(t);
                    n++;
                }
            return $"{n} drawings to {dir}";
        }

        static bool IsWeapon(Transform t, Transform root)
        {
            for (var p = t; p != null && p != root; p = p.parent) if (p.name.StartsWith("P_Weapon")) return true;
            return false;
        }

        /// <summary>Our Iron Sword as a sprite whose pivot is its grip, sized to `length` local units.</summary>
        static Sprite Sword(float length)
        {
            var art = Resources.Load<Sprite>("Art/wpn_iron_sword");
            if (art == null) return null;
            var src = art.texture;
            var r = art.textureRect;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, true);
            tex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
            tex.Apply(true);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            made.Add(tex);
            // Resources art sits in the top-left of a power-of-two canvas (scripts/unity_art.py): the grip and length are
            // shares of the picture itself, so cut the canvas to the opaque box first (try 1 used the padded canvas, which
            // put the grip below and beside the hilt and drew the blade short).
            var px = tex.GetPixels32();
            int x0 = tex.width, y0 = tex.height, x1 = -1, y1 = -1;
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    if (px[y * tex.width + x].a > 10) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            var box = x1 < 0 ? new Rect(0, 0, tex.width, tex.height) : new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            var sp = Sprite.Create(tex, box, new Vector2(0.5f, 1 - SwordGrip), box.height / length, 0, SpriteMeshType.FullRect);
            sp.name = "Iron Sword (hero)";
            made.Add(sp);
            return sp;
        }

        /// <summary>
        /// Dress `knight` (an instance of the knight prefab, its resolvers live): every library label that has a
        /// repaint gets it, every part re-resolved, our sword on, the shield off. `bodyHeight` = the knight's standing
        /// body height in its own local units (for the sword's length); 0 = measure it here from the drawings' bounds.
        /// </summary>
        public static string Dress(GameObject knight, float bodyHeight = 0)
        {
            var lib = knight.GetComponentInChildren<SpriteLibrary>();
            var asset = lib.spriteLibraryAsset;
            int swapped = 0;
            var missing = new List<string>();
            foreach (var cat in asset.GetCategoryNames())
                foreach (var label in asset.GetCategoryLabelNames(cat))
                {
                    var png = Path.Combine(PaintDir, (cat + "__" + label).Replace(" ", "_") + ".png");
                    if (!File.Exists(png)) { missing.Add(cat + "/" + label); continue; }
                    var sp = MakePart(asset.GetSprite(cat, label), png);
                    ours.Add(sp);
                    lib.AddOverride(sp, cat, label);
                    swapped++;
                }
            lib.RefreshSpriteResolvers();
            // Re-set each part's category and label so parts the clip never keys pick up their override too.
            var names = new Dictionary<string, string>();
            foreach (var cat in asset.GetCategoryNames()) names[cat.ToLowerInvariant()] = cat;
            foreach (var res in knight.GetComponentsInChildren<SpriteResolver>(true))
            {
                var cat = res.GetCategory();
                var label = res.GetLabel();
                if (string.IsNullOrEmpty(cat))
                {
                    names.TryGetValue(res.name.ToLowerInvariant(), out cat);
                    if (cat == null) continue;
                    var labels = new List<string>(asset.GetCategoryLabelNames(cat));
                    label = labels.Count > 0 ? labels[0] : null;
                }
                if (cat == null || label == null) continue;
                res.SetCategoryAndLabel(cat, label);
                res.ResolveSpriteToSpriteRenderer();
            }
            // The sword and the shield.
            if (bodyHeight <= 0)
            {
                Bounds? all = null;
                foreach (var r in knight.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (r.sprite == null || IsWeapon(r.transform, knight.transform)) continue;
                    if (all == null) all = r.bounds; else { var b = all.Value; b.Encapsulate(r.bounds); all = b; }
                }
                bodyHeight = all.HasValue ? all.Value.size.y / Mathf.Abs(knight.transform.lossyScale.y) : 6f;
            }
            string sword = "no sword";
            foreach (var r in knight.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.name == "P_Weapon_Shield") r.enabled = false;
                if (r.name == "P_Weapon_Sword")
                {
                    // the sprite's world length = its pixel height / ppu x the renderer's own scale chain below the knight
                    float chain = Mathf.Abs(r.transform.lossyScale.y / knight.transform.lossyScale.y);
                    var sp = Sword(SwordShare * bodyHeight / chain);
                    if (sp != null) { r.sprite = sp; ours.Add(sp); sword = $"Iron Sword {SwordShare * bodyHeight:0.00} u of body {bodyHeight:0.00} u"; }
                }
            }
            return $"{swapped} drawings swapped" + (missing.Count > 0 ? ", no repaint for " + string.Join(", ", missing.ToArray()) : "") + "; " + sword + "; shield off";
        }

        /// <summary>Body renderers on `knight` showing a sprite that is not ours (shown, enabled, not a weapon or shadow).</summary>
        public static List<string> Strays(GameObject knight)
        {
            var list = new List<string>();
            if (knight == null) return list;
            foreach (var r in knight.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!r.enabled || r.sprite == null || r.name.Contains("shadow")) continue;
                if (r.name == "P_Weapon_Shield") continue;
                if (!ours.Contains(r.sprite)) list.Add(r.name + "=" + r.sprite.name);
            }
            return list;
        }

        public static string Clear()
        {
            foreach (var o in made) if (o != null) Object.Destroy(o);
            made.Clear();
            ours.Clear();
            return "cleared";
        }
    }
}
