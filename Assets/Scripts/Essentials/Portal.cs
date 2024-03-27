using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour
{
    [SerializeField] public string sceneToLoad; // O nome da cena para carregar
    [SerializeField] public Vector3 position;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Manter o GameObject do jogador durante a transição de cena
            DontDestroyOnLoad(other.gameObject);

            // Carregar a cena especificada
            SceneManager.LoadScene(sceneToLoad);

            // Definir a posição do jogador na nova cena
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == sceneToLoad)
        {
            // Encontrar o jogador na nova cena e definir sua posição
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                DungeonGenerator dungeonGenerator = FindObjectOfType<DungeonGenerator>();
                if (dungeonGenerator != null)
                {
                    // Define a posição do jogador como a posição do spawnPosition do DungeonGenerator
                    player.transform.position = dungeonGenerator.SpawnPosition;
                }
                else
                {
                    Debug.LogWarning("DungeonGenerator não encontrado na cena carregada.");
                }

                //player.transform.position = position;
            }

            // Remover o evento OnSceneLoaded para evitar chamadas repetidas
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
