using UnityEngine;
using Mirror;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    CharacterController characterController;

    public float moveSpeed = 300f;

    public float horiz;
    public float vert;
    public float turn;

    public float turnSpeedAccel = 30f;
    public float turnSpeedDecel = 30f;
    public float maxTurnSpeed = 100f;

    Vector3 direction = Vector3.zero;
    GameObject controllerColliderHitObject;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        characterController = GetComponent<CharacterController>();

        // Turn off main camera because GamePlayer prefab has its own camera
        GetComponentInChildren<Camera>().enabled = true;
        Camera.main.enabled = false;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
    }

    [SyncVar(hook = nameof(SetColor))]
    public Color playerColor = Color.black;

    void SetColor(Color color)
    {
        //playerColor = color;
        GetComponent<Renderer>().material.color = color;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        horiz = Input.GetAxis("Horizontal");
        vert = Input.GetAxis("Vertical");

        if ((Input.GetKey(KeyCode.Q)) && (turn > -maxTurnSpeed))
            turn = turn - turnSpeedAccel;
        else if ((Input.GetKey(KeyCode.E)) && (turn < maxTurnSpeed))
            turn = turn + turnSpeedAccel;
        else
        {
            if (turn > turnSpeedDecel)
                turn = turn - turnSpeedDecel;
            else if (turn < -turnSpeedDecel)
                turn = turn + turnSpeedDecel;
            else
                turn = 0f;
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer || characterController == null) return;

        transform.Rotate(0f, turn * Time.fixedDeltaTime, 0f);

        direction = transform.TransformDirection((Vector3.ClampMagnitude(new Vector3(horiz, 0f, vert), 1f) * moveSpeed));
        characterController.SimpleMove(direction * Time.fixedDeltaTime);
    }
}
