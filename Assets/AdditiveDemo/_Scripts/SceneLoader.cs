using UnityEngine;
using Mirror;
using System.Collections;
using UnityEngine.SceneManagement;

public class SceneLoader : NetworkBehaviour
{
    public enum LoadAction
    {
        Load,
        Unload
    }

    [TargetRpc]
    public void TargetLoadUnloadScene(NetworkConnection networkConnection, string SceneName, LoadAction loadAction)
    {
        StartCoroutine(LoadUnloadScene(SceneName, loadAction));
    }

    private bool isBusy = false;

    private IEnumerator LoadUnloadScene(string sceneName, LoadAction loadAction)
    {
        while (isBusy)
        {
            Debug.Log("isBusy");
            yield return null;
        }

        isBusy = true;

        if (loadAction == LoadAction.Load)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
        else
        {
            yield return SceneManager.UnloadSceneAsync(sceneName);
            yield return Resources.UnloadUnusedAssets();
        }

        isBusy = false;
        Debug.LogFormat("{0} {1} Done", sceneName, loadAction.ToString());

        CmdSceneDone(sceneName, loadAction);
    }

    [Command]
    public void CmdSceneDone(string sceneName, LoadAction loadAction)
    {
        // The point of this is to show the client telling server it has loaded the subscene
        // so the server might take some further action, e.g. reposition the player.
        Debug.LogFormat("{0} {1} Done on client", sceneName, loadAction.ToString());
    }
}
