using UnityEngine;

namespace Drakepg
{
    /// <summary>First scene. For now it only sets the frame rate; the real start-up (data, save, title) comes with the port.</summary>
    public class Boot : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;
            Debug.Log($"drakepg {Application.version} (Unity {Application.unityVersion})");
        }
    }
}
