using UnityEngine;

namespace Mirror.PongPlusPlus
{
    internal class BallComponent : NetworkBehaviour
    {
        [SerializeField]
        internal Rigidbody Rb = default;

        [SerializeField]
        internal GameObject playerKicked = default;

        [ServerCallback]
        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "Player")
            {
                playerKicked = other.transform.parent.gameObject;
            }
        }
    }
}
