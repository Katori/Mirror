using UnityEngine;

namespace Mirror.PongPlusPlus
{
    internal class BallComponent : NetworkBehaviour
    {
        [SerializeField]
        internal Rigidbody Rb = default;

        [SerializeField]
        private AudioSource SoundSource = default;

        [SerializeField]
        private AudioClip ServeSound = default;

        [SerializeField]
        private AudioClip HitSound = default;

        internal GameObject playerKicked = default;

        private float AliveTime = 0f;

        private void PlayAudioClip(AudioClip clip)
        {
            SoundSource.clip = clip;
            SoundSource.Play();
        }

        [ClientRpc]
        public void RpcPlayHitSound()
        {
            PlayAudioClip(HitSound);
        }

        [ClientRpc]
        public void RpcPlayServeSound()
        {
            PlayAudioClip(ServeSound);
        }

        [Server]
        internal void BallServed()
        {
            RpcPlayServeSound();
        }

        [ServerCallback]
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.sqrMagnitude > 0.5)
            {
                RpcPlayHitSound();
            }
        }

        [ServerCallback]
        private void LateUpdate()
        {
            AliveTime += Time.deltaTime;
            if (AliveTime > 1f)
            {
                if (Rb.velocity.sqrMagnitude < 0.5f)
                {
                    GameManagerComponent.Instance.BallOut();
                    NetworkServer.Destroy(gameObject);
                }
            }
        }

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
