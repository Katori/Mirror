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
                var c = other.GetComponent<BallComponent>();
                GameManagerComponent.Instance.Score(c.playerKicked, Team);
            }
        }
    }
}
