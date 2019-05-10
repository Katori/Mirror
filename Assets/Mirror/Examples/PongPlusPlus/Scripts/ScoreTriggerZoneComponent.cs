using UnityEngine;

namespace Mirror.PongPlusPlus
{
    class ScoreTriggerZoneComponent: NetworkBehaviour
    {
        [SerializeField]
        private int Team;

        [ServerCallback]
        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "Ball")
            {
                var c = other.GetComponentInParent<BallComponent>();
                GameManagerComponent.Instance.Score(c.playerKicked, Team);
                NetworkServer.Destroy(other.transform.parent.gameObject);
            }
        }
    }
}
