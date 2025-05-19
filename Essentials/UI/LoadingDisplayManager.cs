using UnityEngine;

public class LoadingManager : MonoBehaviour
{
    public GameObject loadingPanel; // Arraste o "LoadingPanel" para este campo no Inspector.

    void Start()
    {
        // Mostra o painel se nenhuma tela estiver pronta
        if (!Display.main.active && loadingPanel != null)
        {
            ShowLoading();
        }
    }

    void Update()
    {
        // Esconde o painel assim que o display estiver pronto
        if (Display.main.active && loadingPanel.activeSelf)
        {
            HideLoading();
        }
    }

    public void ShowLoading()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }
    }

    public void HideLoading()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }
}
