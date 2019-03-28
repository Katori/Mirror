using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkRigidbody : NetworkBehaviour
    {
        public struct ForceStateInput
        {
            public Vector3 Force;
            public int ForceMode;
            public bool ForceIsRelative;
            public Vector3 Torque;
            public int TorqueMode;
            public bool TorqueIsRelative;
        }

        public struct InputMessage
        {
            public long delivery_time;
            public uint start_tick_number;
            public ForceStateInput[] ForceInputs;
        }

        public struct ClientState
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        public struct StateMessage
        {
            public long delivery_time;
            public uint tick_number;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angular_velocity;
        }

        #region Shared Fields

        public float player_movement_impulse;
        public float player_jump_y_threshold;
        public GameObject smoothed_client_player;
        public GameObject server_display_player;

        private Rigidbody Rb;

        #endregion

        #region Client Fields

        public bool EnableClientCorrections = true;
        public bool EnableClientCorrectionSmoothing = true;
        public bool SendRedundantInputs = true;
        private float ClientTimer = 0;
        private uint ClientTickNumber = 0;
        private uint ClientLastReceivedStateTick = 0;
        private const int ClientBufferSize = 1024;
        private ClientState[] ClientStateBuffer = new ClientState[ClientBufferSize]; // client stores predicted moves here

        private ForceStateInput[] ClientForceBuffer = new ForceStateInput[ClientBufferSize]; // client stores predicted inputs here

        private Queue<StateMessage> ClientStateMessages = new Queue<StateMessage>();
        private Vector3 ClientPosError = Vector3.zero;
        private Quaternion ClientRotError = Quaternion.identity;

        private ForceStateInput ForceStateBuffer;

        #endregion

        #region Server Fields

        public uint ServerSnapshotRate;
        private uint ServerTickNumber = 0;
        private uint ServerTickAccumulator = 0;
        internal Queue<InputMessage> ServerInputMsgs = new Queue<InputMessage>();

        #endregion

        #region Unity Callbacks

        private void Start()
        {
            Rb = GetComponent<Rigidbody>();
            if (isServerOnly)
            {
                smoothed_client_player.SetActive(false);
            }

            if (isClientOnly)
            {
                server_display_player.SetActive(false);
            }
        }

        private void Update()
        {
            float dt = Time.fixedDeltaTime;

            if (isClient)
            {
                ClientUpdate(dt);
            }

            if (isServer)
            {
                ServerUpdate(dt);
            }
        }

        [Client]
        private void ClientUpdate(float dt)
        {
            float client_timer = this.ClientTimer;
            uint client_tick_number = this.ClientTickNumber;

            client_timer += Time.deltaTime;
            while (client_timer >= dt)
            {
                client_timer -= dt;

                if (isLocalPlayer)
                {
                    uint buffer_slot = client_tick_number % ClientBufferSize;

                    this.ClientForceBuffer[buffer_slot] = ForceStateBuffer;

                    // store state for this tick, then use current state + input to step simulation
                    this.ClientStoreCurrentStateAndStep(
                        ref this.ClientStateBuffer[buffer_slot],
                        ForceStateBuffer,
                        dt);

                    // send input packet to server
                    InputMessage input_msg;
                    input_msg.delivery_time = System.DateTime.Now.ToBinary();
                    input_msg.start_tick_number = this.SendRedundantInputs ? this.ClientLastReceivedStateTick : client_tick_number;
                    var InputBuffer = new List<ForceStateInput>();

                    for (uint tick = input_msg.start_tick_number; tick <= client_tick_number; ++tick)
                    {
                        InputBuffer.Add(ClientForceBuffer[tick % ClientBufferSize]);
                    }
                    input_msg.ForceInputs = InputBuffer.ToArray();
                    CmdSendInputMsg(input_msg);
                    ForceStateBuffer = default;
                }

                ++client_tick_number;
            }

            if (this.ClientHasStateMessage())
            {
                StateMessage state_msg = this.ClientStateMessages.Dequeue();
                while (this.ClientHasStateMessage()) // make sure if there are any newer state messages available, we use those instead
                {
                    state_msg = this.ClientStateMessages.Dequeue();
                }

                this.ClientLastReceivedStateTick = state_msg.tick_number;

                if (this.EnableClientCorrections)
                {
                    uint buffer_slot = state_msg.tick_number % ClientBufferSize;
                    Vector3 position_error = state_msg.position - this.ClientStateBuffer[buffer_slot].position;
                    float rotation_error = 1.0f - Quaternion.Dot(state_msg.rotation, this.ClientStateBuffer[buffer_slot].rotation);

                    if (position_error.sqrMagnitude > 0.0000001f ||
                        rotation_error > 0.00001f)
                    {
                        // capture the current predicted pos for smoothing
                        Vector3 prev_pos = Rb.position + this.ClientPosError;
                        Quaternion prev_rot = Rb.rotation * this.ClientRotError;

                        // rewind & replay
                        Rb.position = state_msg.position;
                        Rb.rotation = state_msg.rotation;
                        Rb.velocity = state_msg.velocity;
                        Rb.angularVelocity = state_msg.angular_velocity;

                        uint rewind_tick_number = state_msg.tick_number;
                        while (rewind_tick_number < client_tick_number)
                        {
                            buffer_slot = rewind_tick_number % ClientBufferSize;
                            this.ClientStoreCurrentStateAndStep(
                                ref this.ClientStateBuffer[buffer_slot],
                                this.ClientForceBuffer[buffer_slot],
                                dt);

                            ++rewind_tick_number;
                        }

                        // if more than 2ms apart, just snap
                        if ((prev_pos - Rb.position).sqrMagnitude >= 4.0f)
                        {
                            this.ClientPosError = Vector3.zero;
                            this.ClientRotError = Quaternion.identity;
                        }
                        else
                        {
                            this.ClientPosError = prev_pos - Rb.position;
                            this.ClientRotError = Quaternion.Inverse(Rb.rotation) * prev_rot;
                        }
                    }
                }
            }

            this.ClientTimer = client_timer;
            this.ClientTickNumber = client_tick_number;

            if (this.EnableClientCorrectionSmoothing)
            {
                this.ClientPosError *= 0.9f;
                this.ClientRotError = Quaternion.Slerp(this.ClientRotError, Quaternion.identity, 0.1f);
            }
            else
            {
                this.ClientPosError = Vector3.zero;
                this.ClientRotError = Quaternion.identity;
            }

            this.smoothed_client_player.transform.position = Rb.position + this.ClientPosError;
            this.smoothed_client_player.transform.rotation = Rb.rotation * this.ClientRotError;
        }

        [Server]
        private void ServerUpdate(float dt)
        {
            if (ServerInputMsgs.Count > 0)
            {
                NetworkRigidbodyManager.Instance.ServerDirtyTick(this);
            }
        }

        internal void UpdateClients(uint server_tick_number)
        {
            if (isServer)
            {
                StateMessage state_msg;
                state_msg.delivery_time = System.DateTime.Now.ToBinary();
                state_msg.tick_number = server_tick_number;
                state_msg.position = Rb.position;
                state_msg.rotation = Rb.rotation;
                state_msg.velocity = Rb.velocity;
                state_msg.angular_velocity = Rb.angularVelocity;
                RpcSendClientState(state_msg);
            }
        }

        internal void ApplyClientMotion()
        {
            
        }

        internal void ApplyServerMotion()
        {
            server_display_player.transform.position = Rb.position;
            server_display_player.transform.rotation = Rb.rotation;
        }

        // exploratory/unfinished
        public void AddNetworkedForce(Vector3 Force, ForceMode Mode)
        {
            if (hasAuthority || isLocalPlayer)
            {
                ForceStateBuffer = new ForceStateInput
                {
                    Force = Force,
                    ForceMode = (int)Mode,
                    ForceIsRelative = false
                };
            }
        }

        public void AddRelativeNetworkedForce(Vector3 Force, ForceMode Mode)
        {
            if (hasAuthority || isLocalPlayer)
            {
                ForceStateBuffer = new ForceStateInput
                {
                    Force = Force,
                    ForceMode = (int)Mode,
                    ForceIsRelative = true
                };
            }
        }

        public void AddNetworkedTorque(Vector3 Torque, ForceMode Mode)
        {
            if (hasAuthority || isLocalPlayer)
            {
                ForceStateBuffer = new ForceStateInput
                {
                    Torque = Torque,
                    TorqueMode = (int)Mode,
                    TorqueIsRelative = false
                };
            }
        }

        public void AddNetworkedRelativeTorque(Vector3 Torque, ForceMode Mode)
        {
            if (hasAuthority || isLocalPlayer)
            {
                ForceStateBuffer = new ForceStateInput
                {
                    Torque = Torque,
                    TorqueMode = (int)Mode,
                    TorqueIsRelative = true
                };
            }
        }

        [ClientRpc]
        public void RpcSendClientState(StateMessage state_msg)
        {
            ClientStateMessages.Enqueue(state_msg);
        }

        [Command]
        public void CmdSendInputMsg(InputMessage input_msg)
        {
            ServerInputMsgs.Enqueue(input_msg);
        }

        #endregion

        internal void PrePhysicsStep(ForceStateInput inputs)
        {
            ForceMode ForceMode = (ForceMode)inputs.ForceMode;
            ForceMode TorqueMode = (ForceMode)inputs.TorqueMode;
            if (!Mathf.Approximately(inputs.Force.sqrMagnitude, 0))
            {
                if (inputs.ForceIsRelative)
                {
                    Rb.AddRelativeForce(inputs.Force, ForceMode);
                }
                else
                {
                    Rb.AddForce(inputs.Force, ForceMode);
                }
            }

            if (!Mathf.Approximately(inputs.Torque.sqrMagnitude, 0))
            {
                if (inputs.TorqueIsRelative)
                {
                    Rb.AddRelativeTorque(inputs.Torque, TorqueMode);
                }
                else
                {
                    Rb.AddTorque(inputs.Torque, TorqueMode);
                }
            }
        }

        private bool ClientHasStateMessage()
        {
            return this.ClientStateMessages.Count > 0 && System.DateTime.Now.ToBinary() >= this.ClientStateMessages.Peek().delivery_time;
        }

        private void ClientStoreCurrentStateAndStep(ref ClientState current_state, ForceStateInput inputs, float dt)
        {
            current_state.position = Rb.position;
            current_state.rotation = Rb.rotation;

            this.PrePhysicsStep(inputs);
            Physics.Simulate(dt);
        }
    }
}
