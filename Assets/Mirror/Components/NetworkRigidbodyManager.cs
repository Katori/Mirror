using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Mirror
{
    public class NetworkRigidbodyManager : MonoBehaviour
    {
        internal static NetworkRigidbodyManager Instance { get; private set; }

        [SerializeField,Tooltip("You can set this programmatically to control it at runtime, but it must be set True at Start() time to initialize itself.")]
        private bool ClearTicksOnSceneChange = true;

        private bool SimulationIsDirty = false;

        internal uint TickNumber = 0;
        private uint ServerTickAccumulator = 0;
        private uint TicksToSimulate = 0;

        private Dictionary<uint, List<NetworkRigidbody>> DirtyClientTicksToSimulate = new Dictionary<uint, List<NetworkRigidbody>>();

        private Dictionary<uint, List<NetworkRigidbody>> DirtyServerTicksToSimulate = new Dictionary<uint, List<NetworkRigidbody>>();
        private uint ServerSnapshotRate;

        private List<NetworkRigidbody> WaitingInputs = new List<NetworkRigidbody>();

        private List<NetworkRigidbody> RigidbodiesWithMessages = new List<NetworkRigidbody>();

        private List<NetworkRigidbody> ServerRigidbodiesWithMessages = new List<NetworkRigidbody>();

        void Start()
        {
            if (Instance == null)
            {
                Instance = this;
                if (ClearTicksOnSceneChange)
                {
                    SceneManager.activeSceneChanged += NewSceneChange;
                }
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            Physics.autoSimulation = false;
        }

        private void NewSceneChange(Scene current, Scene next)
        {
            if (ClearTicksOnSceneChange)
            {
                TicksToSimulate = 0;
            }
        }

        void Update()
        {
            var dt = Time.fixedDeltaTime;
            if (WaitingInputs.Count > 0)
            {
                foreach (var WaitingRb in WaitingInputs)
                {
                    WaitingRb.PrePhysicsClientUpdate();
                }
                Physics.Simulate(dt);
                foreach (var WaitingRb in WaitingInputs)
                {
                    WaitingRb.SendClientInputs();
                }
                ++TickNumber;
                WaitingInputs.Clear();
            }

            if (RigidbodiesWithMessages.Count > 0)
            {
                uint RewindTick = 0;
                foreach (var RbWithMessage in RigidbodiesWithMessages)
                {
                    uint TempRewindTick = 0;
                    RbWithMessage.ProcessMessagesPrePhysics(ref TempRewindTick);
                    RewindTick = (uint)Mathf.Min(RewindTick, TempRewindTick);
                }
                if (RewindTick != 0)
                {
                    while (RewindTick < TickNumber)
                    {
                        var buffer_slot = RewindTick % 1024;
                        foreach (var RbWithMessage in RigidbodiesWithMessages)
                        {
                            RbWithMessage.ClientStateWrapper(buffer_slot);
                        }
                        Physics.Simulate(dt);
                        ++RewindTick;
                    }
                }
            }

            if (ServerRigidbodiesWithMessages.Count > 0)
            {
                // foreach server rigidbody with messages
                Dictionary<NetworkRigidbody, List<NetworkRigidbody.InputMessage>> Messages = new Dictionary<NetworkRigidbody, List<NetworkRigidbody.InputMessage>>();

                foreach (var item in Messages)
                {

                }
                // pull 
            }
        }

        internal void ClientHasInputs(NetworkRigidbody nrb)
        {
            WaitingInputs.Add(nrb);
        }

        internal void ServerDirtyTick(NetworkRigidbody networkRigidbody)
        {
            
        }

        internal void RigidbodyHasMessages(NetworkRigidbody networkRigidbody)
        {
            RigidbodiesWithMessages.Add(networkRigidbody);
        }

        internal void ServerRigidbodyHasMessages(NetworkRigidbody nrb)
        {
            ServerRigidbodiesWithMessages.Add(nrb);
        }
    }
}
