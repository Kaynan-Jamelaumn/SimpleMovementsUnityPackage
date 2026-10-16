using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The start screens for a single-player game without the network layer (Fish-Net's <c>NetBootstrap</c> shows them
/// itself): put it in a menu scene, set the game scene, press Play. The player picks or creates a character, which is
/// applied to the player of the game scene when it loads.
/// </summary>
public class OfflineStartScreens : MonoBehaviour
{
    [Tooltip("Scene loaded when the player starts (must be in Build Settings). Empty = the City scene of the Hub World Settings.")]
    public string gameScene = "";

    [Tooltip("Title on the start screen.")]
    public string gameTitle = "Simple Movements";

    private FrontEndMenu menu;

    private void Start()
    {
        menu = FrontEndMenu.Create(gameTitle, false, false, "", "");
        menu.PlayRequested += _ => LoadGame();
    }

    private void LoadGame()
    {
        string scene = !string.IsNullOrEmpty(gameScene) ? gameScene : HubWorld.HubWorldSettings.Instance.citySceneName;
        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[Start Screens] Scene '{scene}' is not in Build Settings: set the Game Scene of the Offline Start Screens.", this);
            menu.SetStatus($"Scene '{scene}' cannot be loaded.");
            return;
        }
        SceneManager.LoadScene(scene, LoadSceneMode.Single);
    }
}
