using System.Collections.Generic;
using GameNet;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Watches for the local player: applies the session character to it in single player, and builds the pause menu
/// when the game scene has none. Created automatically (hidden).
/// </summary>
[AddComponentMenu("")]
internal class CharacterAutoApplyRunner : MonoBehaviour
{
    private static CharacterAutoApplyRunner instance;
    private float nextCheck;

    internal static void Ensure()
    {
        if (instance != null || !Application.isPlaying)
            return;
        var go = new GameObject("Character Auto Apply") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CharacterAutoApplyRunner>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck)
            return;
        nextCheck = Time.unscaledTime + 0.25f;
        if (NetAuthority.IsHeadless)
            return;
        PlayerStatusController local = CharacterProfileApplier.LocalPlayer();
        if (local == null || !local.IsInitialized)
            return;

        if (CharacterProfileApplier.CreatePauseMenuIfMissing && PauseMenuManager.Instance == null)
            GameMenuFactory.CreateRuntimeMenu();

        if (!CharacterProfileApplier.AutoApplyOffline || NetAuthority.IsNetworked)
            return;
        CharacterSave save = CharacterSession.Current;
        if (save == null)
            return;
        GameObject root = CharacterProfileApplier.RootOf(local);
        AppliedCharacter marker = root != null ? root.GetComponent<AppliedCharacter>() : null;
        if (marker != null && marker.Save == save)
            return;
        CharacterProfileApplier.Apply(root, save, true);
    }
}
