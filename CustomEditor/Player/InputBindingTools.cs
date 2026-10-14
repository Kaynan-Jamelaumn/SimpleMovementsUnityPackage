#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Editor helpers for the saved key bindings (Tools ▸ SimpleMovements ▸ Input).</summary>
public static class InputBindingTools
{
    [MenuItem(SMMenu.Root + "Input/Reset Saved Key Bindings", false, SMMenu.Input + 21)]
    private static void ResetSaved()
    {
        string json = InputBindingStore.SavedJson;
        if (string.IsNullOrEmpty(json))
        {
            EditorUtility.DisplayDialog("Key bindings", "No saved key bindings: the keys of your Input Actions asset are used.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Key bindings",
                "Saved key bindings (made in the pause menu's Key Bindings) are applied on top of your Input Actions asset:\n\n" +
                (json.Length > 600 ? json.Substring(0, 600) + "…" : json) + "\n\nDelete them and use the asset's keys?", "Delete", "Cancel"))
            return;
        InputBindingStore.ResetToDefaults();
        Debug.Log("[Input] Saved key bindings deleted: the Input Actions asset's keys are used.");
    }

    [MenuItem(SMMenu.Root + "Input/Show Saved Key Bindings", false, SMMenu.Input + 20)]
    private static void ShowSaved()
    {
        string json = InputBindingStore.SavedJson;
        Debug.Log(string.IsNullOrEmpty(json) ? "[Input] No saved key bindings." : "[Input] Saved key bindings (PlayerPrefs '" + InputBindingStore.PrefsKey + "'):\n" + json);
    }
}
#endif
