using UnityEditor;

namespace Drakepg.EditorTools
{
    /// <summary>
    /// Keeps play mode running while the editor is in the background (a chat driving the game through the Unity MCP):
    /// an unfocused editor otherwise only advances a frame when something pokes it.
    /// </summary>
    [InitializeOnLoad]
    static class PlayTicker
    {
        static PlayTicker()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.isPlaying && !EditorApplication.isPaused) EditorApplication.QueuePlayerLoopUpdate();
            };
        }
    }
}
