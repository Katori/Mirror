using UnityEngine;
using Mirror;

public class ZoneHandler : NetworkBehaviour
{
    // Set in the inspector
    [Scene]
    [Tooltip("Assign the sub-scene to load for this zone.")]
    public string subScene;

    [Server]
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Loading " + subScene);
        SceneLoader sceneLoader = other.gameObject.GetComponent<SceneLoader>();
        NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
        if (sceneLoader != null && networkIdentity != null)
            sceneLoader.TargetLoadUnloadScene(networkIdentity.connectionToClient, subScene, SceneLoader.LoadAction.Load);
    }

    [Server]
    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Unloading " + subScene);
        SceneLoader sceneLoader = other.gameObject.GetComponent<SceneLoader>();
        NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
        if (sceneLoader != null && networkIdentity != null)
            sceneLoader.TargetLoadUnloadScene(networkIdentity.connectionToClient, subScene, SceneLoader.LoadAction.Unload);
    }
}