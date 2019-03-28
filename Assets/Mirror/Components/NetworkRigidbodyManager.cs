using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;

namespace Mirror
{
    public class NetworkRigidbodyManager : MonoBehaviour
    {
        internal static NetworkRigidbodyManager Instance { get; private set; }

        [SerializeField,Tooltip("You can set this programmatically to control it at runtime, but it must be set True at Start() time to initialize itself.")]
        private bool ClearTicksOnSceneChange = true;

        private bool SimulationIsDirty = false;

        internal uint TickNumber = 0;
        private uint ServerTickAccumulator;
        private int TicksToSimulate = 0;

        private Dictionary<uint, List<NetworkRigidbody>> DirtyClientTicksToSimulate = new Dictionary<uint, List<NetworkRigidbody>>();

        private Dictionary<uint, List<NetworkRigidbody>> DirtyServerTicksToSimulate = new Dictionary<uint, List<NetworkRigidbody>>();
        private uint ServerSnapshotRate;

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
            float dt = Time.fixedDeltaTime;
            if (SimulationIsDirty)
            {
                //Physics.Simulate(dt);

                foreach (var Tick in DirtyServerTicksToSimulate)
                {
                    uint server_tick_number = TickNumber;
                    uint server_tick_accumulator = ServerTickAccumulator;

                    foreach (var DirtyRb in Tick.Value)
                    {
                        while (DirtyRb.ServerInputMsgs.Count > 0 && System.DateTime.Now.ToBinary() >= DirtyRb.ServerInputMsgs.Peek().delivery_time)
                        {
                            NetworkRigidbody.InputMessage input_msg = DirtyRb.ServerInputMsgs.Dequeue();

                            // message contains an array of inputs, calculate what tick the final one is
                            uint max_tick = input_msg.start_tick_number + (uint)input_msg.ForceInputs.Length - 1;

                            // if that tick is greater than or equal to the current tick we're on, then it
                            // has inputs which are new
                            if (max_tick >= server_tick_number)
                            {
                                // there may be some inputs in the array that we've already had,
                                // so figure out where to start
                                uint start_i = server_tick_number > input_msg.start_tick_number ? (server_tick_number - input_msg.start_tick_number) : 0;

                                // run through all relevant inputs, and step player forward
                                for (int i = (int)start_i; i < input_msg.ForceInputs.Length; ++i)
                                {
                                    DirtyRb.PrePhysicsStep(input_msg.ForceInputs[i]);

                                    ++server_tick_number;
                                    ++server_tick_accumulator;
                                }
                            }
                        }
                    }
                    Physics.Simulate(dt);

                    if (server_tick_accumulator >= this.ServerSnapshotRate)
                    {
                        server_tick_accumulator = 0;
                        foreach (var DirtyRb in Tick.Value)
                        {
                            DirtyRb.UpdateClients(TickNumber);
                            DirtyRb.ApplyServerMotion();
                        }
                    }

                    TickNumber = server_tick_number;
                    ServerTickAccumulator = server_tick_accumulator;
                }
                DirtyServerTicksToSimulate.Clear();
                SimulationIsDirty = false;
            }
        }

        internal void MarkSimulationDirty()
        {
            SimulationIsDirty = true;
        }

        internal void ServerDirtyTick(NetworkRigidbody networkRigidbody)
        {
            if (DirtyServerTicksToSimulate.ContainsKey(TickNumber))
            {
                DirtyServerTicksToSimulate[TickNumber].Add(networkRigidbody);
            }
            else
            {
                DirtyServerTicksToSimulate.Add(TickNumber, new List<NetworkRigidbody> { networkRigidbody });
            }
            SimulationIsDirty = true;
        }
    }
}
