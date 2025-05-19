using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class UIPlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab; // Reference to your player prefab
    [SerializeField] private List<TMP_Text> classStatsText; // Reference to the text component displaying class stats
    [SerializeField] private TMP_InputField playerName; // Reference to the text component displaying class stats
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private TMP_Text errorMessage;
    public GameObject player;

    private string selectedClassName; // Store the selected class name
    void Start()
    {
        // Disable the first option
        if (dropdown != null)
        {
            string selectedOption = dropdown.options[0].text;
            UpdateClassStats(selectedOption);
        }
    }
    public void GetDropdownValue()
    {

        int pickedEntryIndex = dropdown.value;
        string selectedOption = dropdown.options[pickedEntryIndex].text;
        // ClassSelectDropdown(pickedEntryIndex);
        UpdateClassStats(selectedOption);
        
    }
    public void UpdateClassStats(string className)
    {
        // You need to implement a method to fetch class stats based on className
        BasePlayerClass classStats = FetchClassStats(className);
        if (classStats != null)
        {
            errorMessage.text = "";
            classStatsText[0].text = classStats.health.ToString();
            classStatsText[1].text = classStats.stamina.ToString();
            classStatsText[2].text = classStats.speed.ToString();
            classStatsText[3].text = classStats.weight.ToString();
            selectedClassName = className;
        }
        else errorMessage.text = "Invalid class";
    }

    // Method to be called when confirm button is clicked
    public void OnConfirmButtonClicked()
    {
        if (playerName.text == null || playerName.text.Length <=0) errorMessage.text = "No chosen name.";

        else
        {
            if (!string.IsNullOrEmpty(selectedClassName))
                SpawnPlayer(selectedClassName, playerName.text);
            else
                errorMessage.text = "No class selected.";
            
        }
    }

    // Method to spawn player and set class
    private void SpawnPlayer(string className, string playerName)
    {
        //GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

        //PlayerStatusController playerStatusController = newPlayer.GetComponent<PlayerStatusController>();
        //PlayerStartItemController playerStartItemController = newPlayer.GetComponent<PlayerStartItemController>();

        //newPlayer.GetComponent<Player>().PlayerName = playerName;

        //playerStartItemController.SetPlayerClassItems(className);
        //playerStatusController.SelectPlayerClass(className);
        //LoadScene("SampleScene");

        //DontDestroyOnLoad(newPlayer);
        LoadScene("SampleScene", className, playerName);

    }
    public void LoadScene(string sceneName, string className, string playerName)
    {
        // Carrega a cena
        SceneManager.LoadScene(sceneName);

        // Quando a cena é carregada, spawna o jogador nela
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == sceneName)
            {
                GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

                // Configurações do jogador, como nome e classe
                newPlayer.GetComponent<Player>().PlayerName = playerName;
                newPlayer.GetComponent<PlayerStartItemController>().SetPlayerClassItems(className);
                newPlayer.GetComponent<PlayerStatusController>().SelectPlayerClass(className);
            }
        };
    }
    // Method to fetch class stats based on class name (Replace this with your actual method)
    private BasePlayerClass FetchClassStats(string className)
    {
        // This is just a placeholder, you need to implement the actual method
        // to fetch class stats based on className
        BasePlayerClass basePlayerClass = null;
        switch (className.ToLower())
        {
            case "warrior":
                basePlayerClass = new WarriorClass();
                break;
            case "mage":
                //basePlayerClass = new MageClass();
                break;
            default:
                Debug.Log("Invalid chosen class!");
                return null;
        }
        if (basePlayerClass != null) return basePlayerClass;
        return null;
    }

}
