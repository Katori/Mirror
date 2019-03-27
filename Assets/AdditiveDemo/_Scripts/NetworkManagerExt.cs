using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.SceneManagement;

public class NetworkManagerExt : NetworkManager
{
    [Scene]
    [Tooltip("Add all sub-scenes to this list")]
    public string[] SubScenes;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("Loading Scenes");
        foreach (string SceneName in SubScenes)
            StartCoroutine(LoadScene(SceneName));
    }

    IEnumerator LoadScene(string sceneName)
    {
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        Debug.Log("Loaded " + sceneName);
    }

    public override void OnStopServer()
    {
        Debug.Log("Stopping Server");
        base.OnStopServer();
        UnloadScenes();
    }

    public override void OnStopClient()
    {
        Debug.Log("Stopping Client");
        base.OnStopClient();
        UnloadScenes();
    }

    void UnloadScenes()
    {
        Debug.Log("Unloading Scenes");
        foreach (string sceneName in SubScenes)
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                if (SceneManager.GetSceneAt(i).name == sceneName)
                    StartCoroutine(UnloadScene(sceneName));
            }
    }

    IEnumerator UnloadScene(string sceneName)
    {
        yield return SceneManager.UnloadSceneAsync(sceneName);
        yield return Resources.UnloadUnusedAssets();
        Debug.Log("Unloaded " + sceneName);
    }
}
