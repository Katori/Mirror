using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class NetworkRigidbody : NetworkBehaviour
{
    private float timer = 0f;

    public List<Inputs> InputBuffer = new List<Inputs>();

    [SerializeField]
    private float MoveSpeed;

    public Rigidbody Rigidbody;

    //public GameObject ProxyPlayer;

    public GameObject SmoothedPlayer;

    private void Start()
    {
        NetworkRigidbodyManager.Instance.RegisterRigidbody(this);
    }

    private void OnDestroy()
    {
        NetworkRigidbodyManager.Instance.UnregisterRigidbody(netIdentity);
    }

    private void Update()
    {
        if (isLocalPlayer || hasAuthority)
        {
            if (SampleInputs(out Inputs[] inputs))
            {
                foreach (var item in inputs)
                {
                    InputBuffer.Add(item);
                }
            }
            else
            {
                InputBuffer.Add(new Inputs
                {
                    NetId = netIdentity,
                    Tick = NetworkRigidbodyManager.Instance.Tick,
                    AddForce = false,
                    AddTorque = false
                });
            }
        }
    }

    protected virtual bool SampleInputs(out Inputs[] inputs)
    {
        var inputBasis = new Inputs
        {
            Tick = NetworkRigidbodyManager.Instance.CurrentTick,
            NetId = netIdentity
        };
        var inputList = new List<Inputs>();
        bool input = false;
        if (!Mathf.Approximately(Input.GetAxis("Horizontal"), 0))
        {
            var forwardInput = inputBasis;
            forwardInput.AddForce = true;
            forwardInput.ForceToAdd = transform.forward * Input.GetAxis("Horizontal") * MoveSpeed;
            inputList.Add(forwardInput);
            input = true;
        }

        if (!Mathf.Approximately(Input.GetAxis("Vertical"), 0))
        {
            var sideInput = inputBasis;
            sideInput.AddForce = true;
            sideInput.ForceToAdd = transform.right * Input.GetAxis("Horizontal") * MoveSpeed;
            inputList.Add(sideInput);
            input = true;
        }
        inputs = inputList.ToArray();
        return input;
    }

    private void FixedUpdate()
    {
        if (isLocalPlayer || hasAuthority)
        {
            CmdSendInputs(new InputFrame
            {
                Tick = NetworkRigidbodyManager.Instance.Tick,
                Inputs = InputBuffer.ToArray()
            });
            foreach (var item in InputBuffer)
            {
                ApplyInputs(item);
                Physics.Simulate(Time.fixedDeltaTime);
                NetworkRigidbodyManager.Instance.IncrementLocalTick();
            }
            InputBuffer.Clear();
        }
    }

    [Command]
    public void CmdSendInputs(InputFrame inputFrame)
    {
        foreach (var item in inputFrame.Inputs)
        {
            NetworkRigidbodyManager.Instance.AddRigidbodyFrame(item);
        }
    }

    public void ApplyInputs(Inputs inputs)
    {
        if (inputs.AddForce)
        {
            if (inputs.RelativeForce)
            {
                Rigidbody.AddRelativeForce(inputs.ForceToAdd);
            }
            else
            {
                Rigidbody.AddForce(inputs.ForceToAdd);
            }
        }

        if (inputs.AddTorque)
        {
            if (inputs.RelativeTorque)
            {
                Rigidbody.AddRelativeTorque(inputs.TorqueToAdd);
            }
            else
            {
                Rigidbody.AddTorque(inputs.TorqueToAdd);
            }
        }
    }

    [System.Serializable]
    public struct InputFrame
    {
        public int Tick;
        public Inputs[] Inputs;
    }

    [System.Serializable]
    public struct Inputs
    {
        public int Tick;
        public NetworkIdentity NetId;
        public bool AddForce;
        public bool AddTorque;
        public Vector3 ForceToAdd;
        public Vector3 TorqueToAdd;
        public bool RelativeForce;
        public bool RelativeTorque;
    }
}
