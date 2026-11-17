#if UNITY_EDITOR
using System;
using System.IO;
using Drakepg.Bones;
using UnityEngine;

namespace Drakepg.Test
{
    /// <summary>
    /// TEST, editor only: the bones read from a folder on disk (default unity/Bones/hero, next to Assets). A move edited
    /// on disk plays on the next Play without recompiling; a redrawn picture shows on the next look (cache by file time).
    /// </summary>
    public class BoneDiskSource : IBoneSource
    {
        public readonly string Folder;

        /// <summary>unity/Bones/hero (Assets/../Bones/hero; through the editor's link too).</summary>
        public static string HeroFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Bones/hero"));

        public BoneDiskSource(string folder = null) => Folder = Path.GetFullPath(folder ?? HeroFolder);

        string Full(string file) => Path.Combine(Folder, file.Replace('/', Path.DirectorySeparatorChar));

        public string Text(string file)
        {
            var p = Full(file);
            if (!File.Exists(p)) return null;
            try { return File.ReadAllText(p); }
            catch (IOException e)
            {
                Debug.LogWarning("bones: can't read " + p + " now (" + e.Message + ")");
                return null;
            }
        }

        public byte[] Bytes(string file)
        {
            var p = Full(file);
            if (!File.Exists(p)) return null;
            try { return File.ReadAllBytes(p); }
            catch (IOException e)
            {
                Debug.LogWarning("bones: can't read " + p + " now (" + e.Message + ")");
                return null;
            }
        }

        public long Stamp(string file)
        {
            var p = Full(file);
            try { return File.Exists(p) ? File.GetLastWriteTimeUtc(p).Ticks : 0; }
            catch (Exception) { return 0; }
        }

        public string Key(string file) => Full(file).Replace('\\', '/');

        /// <summary>moves/&lt;name&gt;.json, read fresh (null and a warning when missing).</summary>
        public BoneMove Move(string name, BoneRig rig = null)
        {
            var file = "moves/" + name + ".json";
            var text = Text(file);
            if (text == null)
            {
                Debug.LogWarning("bones: no move file " + Key(file));
                return null;
            }
            return BoneMove.Parse(text, file, rig);
        }
    }
}
#endif
