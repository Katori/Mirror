using UnityEngine;

namespace Mirror.PongPlusPlus
{
    class ScoreTriggerZoneComponent: NetworkBehaviour
    {
        [SerializeField]
        private int Team = default;

        [ServerCallback]
        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "Ball")
            {
                if (Team != 2)
                {
                    var c = other.GetComponentInParent<BallComponent>();
                    GameManagerComponent.Instance.Score(c.playerKicked, Team);
                    NetworkServer.Destroy(other.transform.parent.gameObject);
                }
                else
                {
                    GameManagerComponent.Instance.BallOut();
                    NetworkServer.Destroy(other.transform.parent.gameObject);
                }
            }
        }
    }
}
