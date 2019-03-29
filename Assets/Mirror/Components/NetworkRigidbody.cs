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

        [SyncVar(hook = "ClientStateUpdated")]
        public StateMessage CurrentState;

        [SerializeField]
        private bool SendRedundantInputs = false;

        private Rigidbody Rb;

        [SerializeField]
        private GameObject smoothed_client_player;

        private uint ClientLastReceivedStateTick;
        private const int ClientBufferSize = 1024;

        private ForceStateInput[] ClientForceBuffer = new ForceStateInput[ClientBufferSize];

        private ForceStateInput ForceStateBuffer;

        private ClientState[] ClientStateBuffer = new ClientState[ClientBufferSize];

        private Vector3 ClientPosError;
        private Quaternion ClientRotError;
        private Vector3 prev_pos;
        private Quaternion prev_rot;

        private Queue<InputMessage> ServerInputMessages = new Queue<InputMessage>();
        private int server_tick_accumulator;
        private int ServerSnapshotRate;

        // Start is called before the first frame update
        void Start()
        {
            Rb = GetComponent<Rigidbody>();
            if (isLocalPlayer || hasAuthority)
            {
                NetworkRigidbodyManager.Instance.RegisterLocalRb(this);
            }

            if (isServer)
            {
                NetworkRigidbodyManager.Instance.RegisterServerRb(this);
            }
        }

        internal void PostRewind()
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

        internal void ClientPostUpdate()
        {
            ClientPosError *= 0.9f;
            ClientRotError = Quaternion.Slerp(this.ClientRotError, Quaternion.identity, 0.1f);

            smoothed_client_player.transform.position = Rb.position + this.ClientPosError;
            smoothed_client_player.transform.rotation = Rb.rotation * this.ClientRotError;
        }

        internal void AuthorityPreUpdate(uint tickNumber)
        {
            if (isLocalPlayer || hasAuthority)
            {
                uint buffer_slot = tickNumber % ClientBufferSize;

                ClientForceBuffer[buffer_slot] = ForceStateBuffer;

                // store state for this tick, then use current state + input to step simulation
                this.ClientStoreCurrentStateBeforeStep(
                    ref this.ClientStateBuffer[buffer_slot],
                    ForceStateBuffer);
            }
        }

        internal void AuthorityPostUpdate(uint tickNumber)
        {
            if (isLocalPlayer || hasAuthority)
            {
                InputMessage input_msg;
                input_msg.delivery_time = System.DateTime.Now.ToBinary();
                input_msg.start_tick_number = SendRedundantInputs ? ClientLastReceivedStateTick : tickNumber;
                var InputBuffer = new List<ForceStateInput>();

                for (uint tick = input_msg.start_tick_number; tick <= tickNumber; ++tick)
                {
                    InputBuffer.Add(ClientForceBuffer[tick % ClientBufferSize]);
                }
                input_msg.ForceInputs = InputBuffer.ToArray();
                CmdSendInputMsg(input_msg);
                ForceStateBuffer = default;
            }
        }

        internal void PrepareForRewind(uint rewindTickNumber)
        {
            uint buffer_slot = rewindTickNumber % ClientBufferSize;
            this.ClientStoreCurrentStateBeforeStep(
                ref this.ClientStateBuffer[buffer_slot],
                this.ClientForceBuffer[buffer_slot]);
        }

        private void ClientStoreCurrentStateBeforeStep(ref ClientState current_state, ForceStateInput inputs)
        {
            current_state.position = Rb.position;
            current_state.rotation = Rb.rotation;

            this.PrePhysicsStep(inputs);
        }

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

        internal void ServerPostUpdate(uint serverTickNumber, uint currentTick)
        {
            ++server_tick_accumulator;
            if (server_tick_accumulator >= this.ServerSnapshotRate)
            {
                server_tick_accumulator = 0;

                StateMessage state_msg;
                state_msg.delivery_time = System.DateTime.Now.ToBinary();
                state_msg.tick_number = serverTickNumber;
                state_msg.position = Rb.position;
                state_msg.rotation = Rb.rotation;
                state_msg.velocity = Rb.velocity;
                state_msg.angular_velocity = Rb.angularVelocity;
                CurrentState = state_msg;
            }
        }

        internal void ServerPreUpdate(ForceStateInput forceStateInput)
        {
            PrePhysicsStep(forceStateInput);
        }

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

        public void ClientStateUpdated(StateMessage NewState)
        {
            CurrentState = NewState;
            ClientLastReceivedStateTick = CurrentState.tick_number;

            uint buffer_slot = CurrentState.tick_number % ClientBufferSize;
            Vector3 position_error = CurrentState.position - this.ClientStateBuffer[buffer_slot].position;
            float rotation_error = 1.0f - Quaternion.Dot(CurrentState.rotation, this.ClientStateBuffer[buffer_slot].rotation);

            if (position_error.sqrMagnitude > 0.0000001f ||
                rotation_error > 0.00001f)
            {
                // capture the current predicted pos for smoothing
                prev_pos = Rb.position + ClientPosError;
                prev_rot = Rb.rotation * ClientRotError;

                // rewind & replay
                Rb.position = CurrentState.position;
                Rb.rotation = CurrentState.rotation;
                Rb.velocity = CurrentState.velocity;
                Rb.angularVelocity = CurrentState.angular_velocity;

                uint rewind_tick_number = CurrentState.tick_number;
                NetworkRigidbodyManager.Instance.PrepareRewind(this, rewind_tick_number);
            }
        }

        [Command]
        public void CmdSendInputMsg(InputMessage input_msg)
        {
            ServerInputMessages.Enqueue(input_msg);
            NetworkRigidbodyManager.Instance.ServerRbHasMessage(this, input_msg);
        }
    }
}
