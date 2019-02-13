using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.NetworkCharacterController
{
    public class MovingPlatformComponent : NetworkBehaviour
    {
        [SerializeField]
        private List<Transform> Waypoints;

        private int CurrentWaypoint;
        private const float Speed = 3f;

        private void Update()
        {
            if (isServer)
            {
                if (transform.position == Waypoints[CurrentWaypoint].position)
                {
                    CurrentWaypoint++;
                    if (CurrentWaypoint > Waypoints.Count - 1)
                    {
                        CurrentWaypoint = 0;
                    }
                }

                transform.position = Vector3.MoveTowards(transform.position, Waypoints[CurrentWaypoint].position, Time.deltaTime * Speed);
            }
        }
    }
}