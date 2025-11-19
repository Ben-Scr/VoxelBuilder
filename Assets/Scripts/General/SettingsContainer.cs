using UnityEngine;

namespace BenScr.MinecraftClone
{
    public class SettingsContainer : MonoBehaviour
    {
        public bool DebugRendering = false;
        public bool DebugGizmos = false;

        public static SettingsContainer Settings;

        private void Awake()
        {
            Settings = this;
        }
    }
}
