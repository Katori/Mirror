using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OfflineGUI : MonoBehaviour
{
    [Scene]
    public string OnlineScene;

    void Start()
    {
        // Ensure main camera is enabled because it will be disabled by PlayerController
        Camera.main.enabled = true;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 200, 130));

        GUILayout.Box("OFFLINE  SCENE");
        GUILayout.Box("WASDQE keys to move & turn");

        if (GUILayout.Button("Join Game"))
            SceneManager.LoadScene(OnlineScene);

        GUILayout.EndArea();
    }
}
