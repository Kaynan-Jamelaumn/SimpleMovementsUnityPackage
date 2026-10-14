using UnityEngine;

namespace GameNet
{
    /// <summary>Keyboard checks for the network tools that work with either input backend (the project uses the Input System).</summary>
    public static class NetInput
    {
        /// <summary>Was <paramref name="key"/> pressed this frame?</summary>
        public static bool KeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
                return false;
            switch (key)
            {
                case KeyCode.F1: return kb.f1Key.wasPressedThisFrame;
                case KeyCode.F2: return kb.f2Key.wasPressedThisFrame;
                case KeyCode.F3: return kb.f3Key.wasPressedThisFrame;
                case KeyCode.F4: return kb.f4Key.wasPressedThisFrame;
                case KeyCode.F5: return kb.f5Key.wasPressedThisFrame;
                case KeyCode.F6: return kb.f6Key.wasPressedThisFrame;
                case KeyCode.F7: return kb.f7Key.wasPressedThisFrame;
                case KeyCode.F8: return kb.f8Key.wasPressedThisFrame;
                case KeyCode.F9: return kb.f9Key.wasPressedThisFrame;
                case KeyCode.F10: return kb.f10Key.wasPressedThisFrame;
                case KeyCode.F11: return kb.f11Key.wasPressedThisFrame;
                case KeyCode.F12: return kb.f12Key.wasPressedThisFrame;
                case KeyCode.T: return kb.tKey.wasPressedThisFrame;
                case KeyCode.Y: return kb.yKey.wasPressedThisFrame;
                case KeyCode.N: return kb.nKey.wasPressedThisFrame;
                case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
                case KeyCode.Return: return kb.enterKey.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }
    }
}
