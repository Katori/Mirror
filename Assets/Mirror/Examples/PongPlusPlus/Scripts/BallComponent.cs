using UnityEngine;

namespace Mirror.PongPlusPlus
{
    internal class BallComponent : NetworkBehaviour
    {
        [SerializeField]
        internal Rigidbody Rb;

        [SerializeField]
        internal GameObject playerKicked;

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
