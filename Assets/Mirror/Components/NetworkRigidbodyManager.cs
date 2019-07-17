using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Linq;

public class NetworkRigidbodyManager : NetworkBehaviour
{
    public static NetworkRigidbodyManager Instance { get; private set; }

    [SyncVar]
    public int Tick;

    public int CurrentTick
    {
        get
        {
            if (isClientOnly)
            {
                return localTick;
            }
            else
            {
                return Tick;
            }
        }
    }

    private Dictionary<NetworkIdentity, SyncedRigidbodyDefinition> rigidbodies = new Dictionary<NetworkIdentity, SyncedRigidbodyDefinition>();

    public class SyncedRigidbodyDefinition
    {
        public ClientState[] client_state_buffer;
        public NetworkRigidbody Rb;
        public Vector3 client_pos_error;
        public Quaternion client_rot_error;
    }

    public struct ClientState
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    private const int c_client_buffer_size = 1024;

    private Dictionary<int, RigidbodyFrame> rbFramesToProcess = new Dictionary<int, RigidbodyFrame>();

    private int MaxTick;

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        Physics.autoSimulation = false;
    }

    private int SnapshotTick = 0;

    [SerializeField]
    private int SnapshotRate = 5;

    private int localTick = 0;

    public override void OnStartClient()
    {
        base.OnStartClient();
        localTick = Tick;
    }

    private void Update()
    {

        if (isClient)
        {
            if (ClientHasStateMessage)
            {
                bool DoRewind = false;
                foreach (var stateMessage in ClientSyncedRbFrameToApply.ItemFrames)
                {
                    var netRb = rigidbodies[stateMessage.NetId];
                    //netRb.Rb.ProxyPlayer.transform.position = stateMessage.position;
                    //netRb.Rb.ProxyPlayer.transform.rotation = stateMessage.rotation;

                    int buffer_slot = ClientSyncedRbFrameToApply.Tick % c_client_buffer_size;
                    Vector3 position_error = stateMessage.position - netRb.client_state_buffer[buffer_slot].position;
                    float rotation_error = 1.0f - Quaternion.Dot(stateMessage.rotation, netRb.client_state_buffer[buffer_slot].rotation);

                    if (position_error.sqrMagnitude > 0.0000001f ||
                        rotation_error > 0.00001f)
                    {
                        // capture the current predicted pos for smoothing
                        Vector3 prev_pos = netRb.Rb.Rigidbody.position + netRb.client_pos_error;
                        Quaternion prev_rot = netRb.Rb.Rigidbody.rotation * netRb.client_rot_error;

                        // rewind & replay
                        netRb.Rb.Rigidbody.position = stateMessage.position;
                        netRb.Rb.Rigidbody.rotation = stateMessage.rotation;
                        netRb.Rb.Rigidbody.velocity = stateMessage.velocity;
                        netRb.Rb.Rigidbody.angularVelocity = stateMessage.angular_velocity;

                        int rewind_tick_number = ClientSyncedRbFrameToApply.Tick;
                        while (rewind_tick_number < localTick)
                        {
                            buffer_slot = rewind_tick_number % c_client_buffer_size;
                            netRb.client_state_buffer[buffer_slot].position = netRb.Rb.Rigidbody.position;
                            netRb.client_state_buffer[buffer_slot].rotation = netRb.Rb.Rigidbody.rotation;

                            ++rewind_tick_number;
                            DoRewind = true;
                        }

                        // if more than 2ms apart, just snap
                        if ((prev_pos - netRb.Rb.Rigidbody.position).sqrMagnitude >= 4.0f)
                        {
                            netRb.client_pos_error = Vector3.zero;
                            netRb.client_rot_error = Quaternion.identity;
                        }
                        else
                        {
                            netRb.client_pos_error = prev_pos - netRb.Rb.Rigidbody.position;
                            netRb.client_rot_error = Quaternion.Inverse(netRb.Rb.Rigidbody.rotation) * prev_rot;
                        }
                    }
                }
                if (DoRewind)
                {
                    for (int i = 0; i < localTick - ClientSyncedRbFrameToApply.Tick; i++)
                    {
                        Physics.Simulate(Time.fixedDeltaTime);
                    }
                }
                localTick = ClientSyncedRbFrameToApply.Tick;
                ClientHasStateMessage = false;
            }

            if (client_correction_smoothing)
            {
                foreach (var item in rigidbodies)
                {
                    item.Value.client_pos_error *= 0.9f;
                    item.Value.client_rot_error = Quaternion.Slerp(item.Value.client_rot_error, Quaternion.identity, 0.1f);
                }
            }
            else
            {
                foreach (var item in rigidbodies)
                {
                    item.Value.client_pos_error = Vector3.zero;
                    item.Value.client_rot_error = Quaternion.identity;
                }
            }

            foreach (var item in rigidbodies)
            {
                item.Value.Rb.SmoothedPlayer.transform.position = item.Value.Rb.Rigidbody.position + item.Value.client_pos_error;
                item.Value.Rb.SmoothedPlayer.transform.rotation = item.Value.Rb.Rigidbody.rotation * item.Value.client_rot_error;
            }
        }
    }

    internal void IncrementLocalTick()
    {
        localTick++;
    }

    private void FixedUpdate()
    {
        if (isServer)
        {
            if (MaxTick >= Tick)
            {
                var c = rbFramesToProcess.Keys.ToList();
                for (int i = 0; i < rbFramesToProcess.Count; i++)
                {
                    foreach (var item in rbFramesToProcess[c[i]].inputsToSim)
                    {
                        if (rigidbodies.ContainsKey(item.NetId))
                        {
                            rigidbodies[item.NetId].Rb.ApplyInputs(item);
                        }
                        else
                        {
                            Debug.LogError("rb not found");
                        }
                    }
                    Debug.LogWarning("Simulating on server");
                    Physics.Simulate(Time.fixedDeltaTime);
                    ++Tick;
                    ++SnapshotTick;

                    if (SnapshotTick >= SnapshotRate)
                    {
                        SnapshotTick = 0;
                        CaptureAndSendState();
                    }
                }
                rbFramesToProcess.Clear();
            }
        }
    }

    [Server]
    private void CaptureAndSendState()
    {
        var messageBuffer = new List<StateMessage>();
        foreach (var item in rigidbodies.Values)
        {
            var c = new StateMessage
            {
                NetId = item.Rb.netIdentity,
                position = item.Rb.Rigidbody.position,
                rotation = item.Rb.Rigidbody.rotation,
                velocity = item.Rb.Rigidbody.velocity,
                angular_velocity = item.Rb.Rigidbody.angularVelocity
            };
            messageBuffer.Add(c);
        }
        var FrameBuffer = new SyncedRigidbodyFrame
        {
            Tick = Tick,
            ItemFrames = messageBuffer.ToArray()
        };
        RpcSendPackedState(FrameBuffer);
    }

    [ClientRpc]
    public void RpcSendPackedState(SyncedRigidbodyFrame frameBuffer)
    {
        ClientSyncedRbFrameToApply = frameBuffer;
        ClientLastReceivedTick = frameBuffer.Tick;
        ClientHasStateMessage = true;
    }

    private SyncedRigidbodyFrame ClientSyncedRbFrameToApply;

    private int ClientLastReceivedTick;

    private bool ClientHasStateMessage = false;
    private bool client_correction_smoothing;

    [System.Serializable]
    public struct StateMessage
    {
        public NetworkIdentity NetId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angular_velocity;
    }

    public void RegisterRigidbody(NetworkRigidbody rb)
    {
        rigidbodies.Add(rb.netIdentity, new SyncedRigidbodyDefinition
        {
            Rb = rb,
            client_pos_error = Vector3.zero,
            client_rot_error = Quaternion.identity,
            client_state_buffer = new ClientState[c_client_buffer_size]
        });
    }

    public void UnregisterRigidbody(NetworkIdentity netid)
    {
        rigidbodies.Remove(netid);
    }

    public void AddRigidbodyFrame(NetworkRigidbody.Inputs inputs)
    {
        if (inputs.Tick >= Tick)
        {
            if (inputs.Tick > MaxTick)
            {
                MaxTick = inputs.Tick;
                rbFramesToProcess.Add(inputs.Tick, new RigidbodyFrame { Tick = inputs.Tick, inputsToSim = new List<NetworkRigidbody.Inputs> { inputs } });
            }
            else
            {
                if (rbFramesToProcess.TryGetValue(inputs.Tick, out RigidbodyFrame rbFrame))
                {
                    rbFrame.inputsToSim.Add(inputs);
                }
                else
                {
                    rbFramesToProcess.Add(inputs.Tick, new RigidbodyFrame
                    {
                        Tick = inputs.Tick,
                        inputsToSim = new List<NetworkRigidbody.Inputs>
                    {
                        inputs
                    }
                    });
                }
            }
        }
    }

    public class RigidbodyFrame
    {
        public int Tick;
        public List<NetworkRigidbody.Inputs> inputsToSim;
    }

    [System.Serializable]
    public struct SyncedRigidbodyFrame
    {
        public int Tick;
        public StateMessage[] ItemFrames;
    }
}
