using UnityEngine;

namespace Mirror.Examples.NetworkCharacterController
{
    public class ZoneHandler : MonoBehaviour
    {
        void OnTriggerEnter(Collider other) { Debug.Log("ZoneHandler : OnTriggerEnter"); }

        void OnTriggerExit(Collider other) { Debug.Log("ZoneHandler : OnTriggerExit"); }
    }
}
