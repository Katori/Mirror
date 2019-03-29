using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mirror
{
    public class NetworkRigidbodyManager : MonoBehaviour
    {
        internal static NetworkRigidbodyManager Instance { get; private set; }

        internal uint TickNumber = 0;

        private List<NetworkRigidbody> LocalRbs = new List<NetworkRigidbody>();

        private List<NetworkRigidbody> RbsNeedingRewind = new List<NetworkRigidbody>();

        private List<NetworkRigidbody> ServerRbs = new List<NetworkRigidbody>();

        private Dictionary<NetworkRigidbody, Dictionary<uint, NetworkRigidbody.InputMessage>> ServerRbsWithTicksToSim = new Dictionary<NetworkRigidbody, Dictionary<uint, NetworkRigidbody.InputMessage>>();

        private uint RewindTickNumber = 0;
        private bool RewindNeeded = false;

        private bool ServerMode = false;
        private uint ServerTickNumber;

        // Start is called before the first frame update
        void Start()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            Physics.autoSimulation = false;
        }

        // Update is called once per frame
        void Update()
        {
            uint tickNumber = TickNumber;
            float clientTimer = 0;

            clientTimer += Time.deltaTime;
            while (clientTimer >= Time.fixedDeltaTime)
            {
                clientTimer -= Time.fixedDeltaTime;
                foreach (var LocalRb in LocalRbs)
                {
                    LocalRb.AuthorityPreUpdate(tickNumber);
                }
                Physics.Simulate(Time.fixedDeltaTime);
                foreach (var LocalRb in LocalRbs)
                {
                    LocalRb.AuthorityPostUpdate(tickNumber);
                }
                tickNumber++;
            }

            if (RewindNeeded)
            {
                while (RewindTickNumber < TickNumber)
                {
                    foreach (var item in RbsNeedingRewind)
                    {
                        item.PrepareForRewind(RewindTickNumber);
                    }
                    Physics.Simulate(Time.fixedDeltaTime);
                    foreach (var item in RbsNeedingRewind)
                    {
                        item.PostRewind();
                    }
                    ++RewindTickNumber;
                }
                RbsNeedingRewind.Clear();
                RewindNeeded = false;
            }

            TickNumber = tickNumber;

            foreach (var item in LocalRbs)
            {
                item.ClientPostUpdate();
            }

            if (ServerMode)
            {
                // should this be a while? original logic was a while
                if (ServerRbsWithTicksToSim.Count > 0)
                {
                    var CurrentTick = ServerRbsWithTicksToSim.Values.Min(x => x.Min(y => y.Key));
                    var RigidbodiesWithCurrentTickInfo = ServerRbsWithTicksToSim.Where(x => x.Value.ContainsKey(CurrentTick)).ToArray();
                    var MaximumInputLength = RigidbodiesWithCurrentTickInfo.Max(x => x.Value.Max(y => y.Value.ForceInputs.Length));

                    var maxTick = CurrentTick + (uint)MaximumInputLength - 1;
                    if (maxTick >= ServerTickNumber)
                    {
                        uint start_i = ServerTickNumber > CurrentTick ? (ServerTickNumber - CurrentTick) : 0;
                        for (int i = (int)start_i; i < MaximumInputLength; ++i)
                        {
                            foreach (var item in RigidbodiesWithCurrentTickInfo)
                            {
                                if (item.Value[CurrentTick].ForceInputs.Length > i)
                                {
                                    item.Key.ServerPreUpdate(item.Value[CurrentTick].ForceInputs[i]);
                                }
                            }
                            Physics.Simulate(Time.fixedDeltaTime);
                            ++ServerTickNumber;
                            foreach (var item in RigidbodiesWithCurrentTickInfo)
                            {
                                item.Key.ServerPostUpdate(ServerTickNumber, CurrentTick);
                            }
                        }
                        foreach (var item in RigidbodiesWithCurrentTickInfo)
                        {
                            ServerTickSimFinished(item.Key, CurrentTick);
                        }
                    }
                }
            }
        }

        internal void RegisterServerRb(NetworkRigidbody ServerRb)
        {
            if (!ServerMode)
            {
                ServerMode = true;
            }
            ServerRbs.Add(ServerRb);
        }

        internal void RegisterLocalRb(NetworkRigidbody networkRigidbodySyncVar)
        {
            LocalRbs.Add(networkRigidbodySyncVar);
        }

        internal void PrepareRewind(NetworkRigidbody networkRigidbodySyncVar, uint rewind_tick_number)
        {
            RbsNeedingRewind.Add(networkRigidbodySyncVar);
            if (RewindNeeded)
            {
                RewindTickNumber = (uint)Mathf.Min(rewind_tick_number, RewindTickNumber);
            }
            else
            {
                RewindTickNumber = rewind_tick_number;
                RewindNeeded = true;
            }
        }

        private void ServerTickSimFinished(NetworkRigidbody networkRigidbodySyncVar, uint currentTick)
        {
            ServerRbsWithTicksToSim[networkRigidbodySyncVar].Remove(currentTick);
            if (ServerRbsWithTicksToSim[networkRigidbodySyncVar].Count == 0)
            {
                ServerRbsWithTicksToSim.Remove(networkRigidbodySyncVar);
            }
        }

        internal void ServerRbHasMessage(NetworkRigidbody networkRigidbodySyncVar, NetworkRigidbody.InputMessage input_msg)
        {
            if (ServerRbsWithTicksToSim.ContainsKey(networkRigidbodySyncVar))
            {
                if (!ServerRbsWithTicksToSim[networkRigidbodySyncVar].ContainsKey(input_msg.start_tick_number))
                {
                    ServerRbsWithTicksToSim[networkRigidbodySyncVar][input_msg.start_tick_number] = input_msg;
                }
            }
            else
            {
                ServerRbsWithTicksToSim.Add(networkRigidbodySyncVar, new Dictionary<uint, NetworkRigidbody.InputMessage> { { input_msg.start_tick_number, input_msg } });
            }
        }
    }
}
