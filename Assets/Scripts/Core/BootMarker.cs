using UnityEngine;

namespace SanMonica.Core
{
    /// <summary>
    /// Placeholder component that gives the boot scene something to hold. The
    /// game itself is created by <see cref="GameBootstrap"/>, so this scene can
    /// stay empty - it exists only so the build has a scene to start from.
    /// </summary>
    public class BootMarker : MonoBehaviour
    {
        [TextArea]
        public string Note = "San Monica builds itself at runtime. Press Play from any scene.";
    }
}
