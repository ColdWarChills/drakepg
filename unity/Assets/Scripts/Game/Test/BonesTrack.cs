#if UNITY_EDITOR
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Drakepg.Test
{
    /// <summary>
    /// TEST, editor only (the judging kit, scripts/bones_sheets.py): a per-frame log written next to recorded frames.
    /// Points and boxes are given in world space and stored in the recorded picture's pixels (top-left origin, the
    /// picture `width` x `height` that Drive.Record writes), so Python can draw on the frames. Anyone can call it: an
    /// actor, a driver, or Drakepg.EditorTools.BonesCapture, which follows transforms from outside.
    /// File: { fps, width, height, screen:[w,h], frames: [ { i, t, points:{name:[x,y]}, boxes:{name:[x0,y0,x1,y1]},
    /// values:{name:number|string}, events:[name] } ] }. Frame i is the picture f{i:000}.jpg.
    /// </summary>
    public static class BonesTrack
    {
        static JArray frames;
        static JObject frame;
        static string path;
        static int width, height, fps;
        static Camera cam;

        public static bool Active => frames != null;
        /// <summary>Frames closed so far (the open one is number Count).</summary>
        public static int Count => frames?.Count ?? 0;

        /// <summary>Start a track file for pictures `width` x `height` taken from `camera` (default: the main camera).</summary>
        public static void Begin(string file, int fps, int width, int height, Camera camera = null)
        {
            path = file;
            BonesTrack.fps = fps;
            BonesTrack.width = width;
            BonesTrack.height = height;
            cam = camera != null ? camera : Camera.main;
            frames = new JArray();
            frame = NewFrame(0);
        }

        static JObject NewFrame(int i) => new JObject
        {
            ["i"] = i, ["points"] = new JObject(), ["boxes"] = new JObject(), ["values"] = new JObject(), ["events"] = new JArray(),
        };

        static double R(float v) => System.Math.Round(v, 1);

        /// <summary>A world point in the recorded picture's pixels (x right, y down).</summary>
        public static Vector2 Pixel(Vector3 world)
        {
            var s = cam.WorldToScreenPoint(world);
            return new Vector2(s.x * width / Screen.width, height - s.y * height / Screen.height);
        }

        public static void Point(string name, Vector3 world)
        {
            if (frame == null) return;
            var p = Pixel(world);
            frame["points"][name] = new JArray(R(p.x), R(p.y));
        }

        /// <summary>A world box (its x/y extent) as a pixel rectangle [x0, y0, x1, y1].</summary>
        public static void Box(string name, Bounds b)
        {
            if (frame == null) return;
            var a = Pixel(new Vector3(b.min.x, b.min.y, b.center.z));
            var c = Pixel(new Vector3(b.max.x, b.max.y, b.center.z));
            frame["boxes"][name] = new JArray(R(Mathf.Min(a.x, c.x)), R(Mathf.Min(a.y, c.y)), R(Mathf.Max(a.x, c.x)), R(Mathf.Max(a.y, c.y)));
        }

        public static void Value(string name, float v)
        {
            if (frame != null) frame["values"][name] = System.Math.Round(v, 4);
        }

        public static void Value(string name, string v)
        {
            if (frame != null) frame["values"][name] = v;
        }

        /// <summary>A named moment (wind-up, hit, back ...), on the frame being drawn now.</summary>
        public static void Event(string name)
        {
            if (frame != null) ((JArray)frame["events"]).Add(name);
        }

        /// <summary>Close the frame being drawn (call once per recorded picture, after it is captured).</summary>
        public static void Next(float time)
        {
            if (frame == null) return;
            frame["t"] = System.Math.Round(time, 4);
            frames.Add(frame);
            frame = NewFrame(frames.Count);
        }

        /// <summary>Write the file and stop. Returns its path.</summary>
        public static string End()
        {
            if (frames == null) return null;
            var o = new JObject
            {
                ["fps"] = fps, ["width"] = width, ["height"] = height, ["screen"] = new JArray(Screen.width, Screen.height),
                ["frames"] = frames,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, o.ToString(Newtonsoft.Json.Formatting.None));
            frames = null;
            frame = null;
            return path;
        }
    }
}
#endif
