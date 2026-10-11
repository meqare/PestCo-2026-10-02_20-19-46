using UnityEngine;
using UnityEngine.SceneManagement;

// Sahne geçişlerini yapar; geçişten önce zamanı ve imleci oyun haline döndürür.
public class SceneLoader : MonoBehaviour
{
    public const string HubSceneName = "Hub";

    public static SceneLoader Instance { get; private set; }

    // Singleton'ı kurar.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // İş sahnesini yükler.
    public void LoadJobScene(string sceneName)
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SceneManager.LoadScene(sceneName);
    }

    // Hub sahnesine döner.
    public void LoadHub()
    {
        LoadJobScene(HubSceneName);
    }
}
