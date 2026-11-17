#if UNITY_EDITOR
namespace Drakepg.Bones
{
    /// <summary>
    /// Where the bones read their files (rig.json, fight.json, swaps.json, moves/*.json, part pictures), by a path
    /// relative to the hero's folder ("bare/torso.png", "moves/tour.json"). The core never touches the disk itself:
    /// the editor gives it a folder reader (Test/BoneDiskSource.cs); the game will later give it its own files.
    /// </summary>
    public interface IBoneSource
    {
        /// <summary>The file as text, or null when it is missing.</summary>
        string Text(string file);
        /// <summary>The file's bytes, or null when it is missing.</summary>
        byte[] Bytes(string file);
        /// <summary>A number that changes when the file changes (its write time), 0 when unknown or missing.</summary>
        long Stamp(string file);
        /// <summary>The file's full name, unique across sources (the picture cache's key).</summary>
        string Key(string file);
    }
}
#endif
