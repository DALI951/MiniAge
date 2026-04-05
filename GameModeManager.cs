using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }
    
    public enum GameMode { SinglePlayer, Multiplayer }
    public GameMode currentMode = GameMode.SinglePlayer;
    
    private void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public void StartSinglePlayer()
    {
        currentMode = GameMode.SinglePlayer;
        // Load game scene directly without networking
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
    }
    
    public void StartMultiplayerHost()
    {
        currentMode = GameMode.Multiplayer;
        // Your existing multiplayer host code
        RTSNetworkManager.Instance?.StartHost();
    }
    
    public void StartMultiplayerClient(string ipAddress)
    {
        currentMode = GameMode.Multiplayer;
        // Your existing multiplayer client code
        RTSNetworkManager.Instance.networkAddress = ipAddress;
        RTSNetworkManager.Instance.StartClient();
    }
}
