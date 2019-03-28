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
        internal Queue<ForceStateInput> ServerInputMsgs = new Queue<ForceStateInput>();
        private Vector3 prev_pos;
        private Quaternion prev_rot;

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

        private void LateUpdate()
        {
            ProcessMessagePostPhysics();
        }

        private void ProcessMessagePostPhysics()
        {
            if (EnableClientCorrections)
            {
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

        [Client]
        private void ClientUpdate(float dt)
        {
            NetworkRigidbodyManager.Instance.RigidbodyHasMessages(this, this.ClientStateMessages.Count);
        }

        [Server]
        private void ServerUpdate(float dt)
        {
            NetworkRigidbodyManager.Instance.ServerRigidbodyHasMessages(this, ServerInputMsgs.Count);
        }

        [Server]
        internal void ServerPreUpdate()
        {
            this.PrePhysicsStep(this.ServerInputMsgs.Dequeue());
        }

        [Server]
        internal void ServerPostUpdate()
        {
            ++this.ServerTickAccumulator;
            if (this.ServerTickAccumulator >= this.ServerSnapshotRate)
            {
                this.ServerTickAccumulator = 0;

                StateMessage state_msg;
                state_msg.tick_number = ServerTickNumber - (uint) this.ServerInputMsgs.Count;
                state_msg.position = Rb.position;
                state_msg.rotation = Rb.rotation;
                state_msg.velocity = Rb.velocity;
                state_msg.angular_velocity = Rb.angularVelocity;
                RpcSendClientState(state_msg);
            }
        }

        internal void AuthorityPreUpdate()
        {
            uint buffer_slot = NetworkRigidbodyManager.Instance.TickNumber % ClientBufferSize;

            this.ClientForceBuffer[buffer_slot] = ForceStateBuffer;

            // store state for this tick, then use current state + input to step simulation
            this.ClientStoreCurrentStateAndStep(
                ref this.ClientStateBuffer[buffer_slot],
                ForceStateBuffer);
        }

        internal void AuthorityPostUpdate()
        {
            // send input packet to server
            InputMessage input_msg;
            input_msg.start_tick_number = this.SendRedundantInputs ? this.ClientLastReceivedStateTick : NetworkRigidbodyManager.Instance.TickNumber;
            var InputBuffer = new List<ForceStateInput>();

            for (uint tick = input_msg.start_tick_number; tick <= NetworkRigidbodyManager.Instance.TickNumber; ++tick)
            {
                InputBuffer.Add(ClientForceBuffer[tick % ClientBufferSize]);
            }
            input_msg.ForceInputs = InputBuffer.ToArray();
            CmdSendInputMsg(input_msg);
            ForceStateBuffer = default;
        }

        internal void ClientPreUpdate(ref uint tempRewindTick)
        {
            StateMessage state_msg = this.ClientStateMessages.Dequeue();
            while (this.ClientStateMessages.Count > 0) // make sure if there are any newer state messages available, we use those instead
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
                    prev_pos = Rb.position + this.ClientPosError;
                    prev_rot = Rb.rotation * this.ClientRotError;

                    // rewind & replay
                    Rb.position = state_msg.position;
                    Rb.rotation = state_msg.rotation;
                    Rb.velocity = state_msg.velocity;
                    Rb.angularVelocity = state_msg.angular_velocity;

                    tempRewindTick = state_msg.tick_number;
                }
            }
        }

        internal void ClientApplyRewindState(uint BufferSlot)
        {
            ClientStoreCurrentStateAndStep(
                ref ClientStateBuffer[BufferSlot],
                ClientForceBuffer[BufferSlot]);
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
                NetworkRigidbodyManager.Instance.ClientHasInputs(this);
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
                NetworkRigidbodyManager.Instance.ClientHasInputs(this);
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
                NetworkRigidbodyManager.Instance.ClientHasInputs(this);
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
                NetworkRigidbodyManager.Instance.ClientHasInputs(this);
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
            uint server_tick_number = this.ServerTickNumber;

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
                    ++server_tick_number;
                    ServerInputMsgs.Enqueue(input_msg.ForceInputs[i]);
                }

                this.server_display_player.transform.position = Rb.position;
                this.server_display_player.transform.rotation = Rb.rotation;
            }

            this.ServerTickNumber = server_tick_number;
        }

        #endregion

        private void PrePhysicsStep(ForceStateInput inputs)
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

        private void ClientStoreCurrentStateAndStep(ref ClientState current_state, ForceStateInput inputs)
        {
            current_state.position = Rb.position;
            current_state.rotation = Rb.rotation;

            this.PrePhysicsStep(inputs);
        }
    }
}
