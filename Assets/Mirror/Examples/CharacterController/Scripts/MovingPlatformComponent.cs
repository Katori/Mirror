using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.NetworkCharacterController
{
    public class MovingPlatformComponent : NetworkBehaviour
    {
        [SerializeField]
        Vector3[] points;

        int CurrentWaypoint;
        private const float Speed = 3f;

        Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (isServer)
            {
                if (transform.position == points[CurrentWaypoint])
                {
                    CurrentWaypoint++;
                    if (CurrentWaypoint > points.GetUpperBound(0))
                        CurrentWaypoint = 0;
                }

                transform.position = Vector3.MoveTowards(transform.position, points[CurrentWaypoint], Time.deltaTime * Speed);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            //Debug.LogFormat("MovingPlatformComponent : OnCollisionEnter {0}", collision.gameObject);
        }

        void OnCollisionStay(Collision collision)
        {
            //Debug.LogFormat("MovingPlatformComponent : OnCollisionStay {0}", collision.gameObject);
        }

        void OnCollisionExit(Collision collision)
        {
            //Debug.LogFormat("MovingPlatformComponent : OnCollisionExit {0}", collision.gameObject);
        }
    }
}