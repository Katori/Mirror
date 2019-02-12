using UnityEngine;
using UnityEngine.Profiling;
using System.Collections.Generic;

namespace Mirror
{
    /*
        Server-authoritative movement with Client-side prediction and reconciliation
        Original Author: gennadiy.shvetsov@gmail.com
        Original Repository: https://github.com/GenaSG/UnityUnetMovement

        The MIT License (MIT)

        Copyright (c) 2015 GenaSG

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
    */

    [RequireComponent(typeof(CharacterController))]
    public class NetworkCharacterController : NetworkBehaviour
    {
        #region Declarations

        CharacterController characterController;

        float turn = 0f;
        float verticalSpeed = 0f;

        [SerializeField]
        LayerMask layerMask;

        [SerializeField]
        float groundSpeed = 5f;

        [SerializeField]
        float jumpHeight = 5f;

        [SerializeField]
        float mouseSensitivity = 100f;

        [SerializeField]
        float turnSpeedMax = 100;
        [SerializeField]
        float turnSpeedAccel = 30;
        [SerializeField]
        float turnSpeedDecel = 30;

        [SerializeField]
        float snapDistance = 1f;

        [Header("Key Assignments")]
        [SerializeField]
        KeyCode steerMode = KeyCode.M;
        [SerializeField]
        KeyCode turnLeft = KeyCode.Q;
        [SerializeField]
        KeyCode turnRight = KeyCode.E;
        [SerializeField]
        KeyCode sprint = KeyCode.LeftShift;
        [SerializeField]
        KeyCode crouch = KeyCode.C;
        [SerializeField]
        KeyCode jump = KeyCode.Space;

        [Header("Diagnostic Values")]

        [SerializeField]
        bool mouseSteer = false;

        [SerializeField]
        bool isGrounded = false;

        [SerializeField]
        bool isJumping = false;

        [SerializeField]
        bool isSprinting = false;

        [SerializeField]
        bool isCrouching = false;

        // This struct would be used to collect player inputs
        [System.Serializable]
        public struct Inputs
        {
            public float forward;
            public float strafe;
            public float vertical;
            public float turn;
            public bool sprint;
            public bool crouch;

            public float timeStamp;
        }

        [System.Serializable]
        public struct SyncInputs
        {
            public sbyte forward;
            public sbyte strafe;
            public sbyte vertical;
            public float turn;
            public bool sprint;
            public bool crouch;

            public float timeStamp;
        }

        // This struct would be used to collect results of Move and Rotate functions
        [System.Serializable]
        public struct Results
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool sprinting;
            public bool crouching;
            public float timeStamp;
        }

        [System.Serializable]
        public struct SyncResults
        {
            public Vector3 position;
            public ushort turn;
            public bool sprinting;
            public bool crouching;
            public float timeStamp;
        }

        // Synced from server to all clients
        [SyncVar(hook = nameof(RecieveResults))]
        public SyncResults syncResults = new SyncResults();

        [SerializeField]
        Inputs inputs;

        [SerializeField]
        Results results;

        [SerializeField]
        Results rcvdResults;

        // Owner client and server would store it's inputs in this list
        [SerializeField]
        List<Inputs> inputsList = new List<Inputs>();

        // This list stores results of movement and rotation.
        // Needed for non-owner client interpolation
        [SerializeField]
        List<Results> resultsList = new List<Results>();

        // Interpolation related variables
        bool _playData = false;
        float _dataStep = 0f;
        float _lastTimeStamp = 0f;
        Vector3 _startPosition;
        Quaternion _startRotation;

        float _step = 0;

        #endregion

        #region Monobehaviors

        // These are public virtuals so extenders can override them and call them as base methods 

        public virtual void Start()
        {
            Debug.LogFormat("Start {0}", transform.position);

            results.position = transform.position;
            results.rotation = transform.rotation;

            characterController = GetComponent<CharacterController>();
            layerMask = 1 << gameObject.layer;
        }

        public virtual void Update()
        {
            Profiler.BeginSample("CC_Update");
            if (isLocalPlayer)
            {
                if (Input.GetKeyUp(steerMode))
                {
                    mouseSteer = !mouseSteer;
                    Cursor.visible = !mouseSteer;
                    Cursor.lockState = mouseSteer ? CursorLockMode.Confined : CursorLockMode.None;
                }

                // Getting clients inputs
                GetInputs(ref inputs);
            }
            Profiler.EndSample();
        }

        Vector3 lastPosition;
        Quaternion lastRotation;
        bool lastCrouch;
        SyncResults tempResults;
        SyncInputs syncInputs;
        Inputs __inputs;
        SyncResults __tempResults;

        public virtual void FixedUpdate()
        {
            Profiler.BeginSample("CC_FixedUpdate");
            isGrounded = Grounded();

            if (isLocalPlayer)
            {
                // Client side prediction for non-authoritative client or plane movement and rotation for listen server/host
                inputs.timeStamp = Time.time;

                lastPosition = results.position;
                lastRotation = results.rotation;
                lastCrouch = results.crouching;

                results.rotation = Rotate(inputs, results);
                results.sprinting = Sprint(inputs, results);
                results.crouching = Crouch(inputs, results);
                results.position = Move(inputs, results);

                if (hasAuthority)
                {
                    // Listen server/host part
                    // Sending results to other clients(state sync)
                    if (_dataStep >= syncInterval)
                    {
                        if (Vector3.Distance(results.position, lastPosition) > 0f
                            || Quaternion.Angle(results.rotation, lastRotation) > 0f
                            || results.crouching != lastCrouch)
                        {
                            results.timeStamp = inputs.timeStamp;
                            // Struct need to be fully new to count as dirty 
                            // Convering some of the values to get less traffic
                            tempResults.position = results.position;
                            tempResults.turn = (ushort)(results.rotation.eulerAngles.y * 182f);
                            tempResults.sprinting = results.sprinting;
                            tempResults.crouching = results.crouching;
                            tempResults.timeStamp = results.timeStamp;
                            syncResults = tempResults;
                        }
                        _dataStep = 0f;
                    }
                    _dataStep += Time.fixedDeltaTime;
                }
                else
                {
                    // Owner client. Non-authoritative part
                    // Add inputs to the inputs list so they could be used during reconciliation process
                    if (Vector3.Distance(results.position, lastPosition) > 0f
                        || Quaternion.Angle(results.rotation, lastRotation) > 0f
                        || results.crouching != lastCrouch)
                        inputsList.Add(inputs);

                    // Sending inputs to the server
                    // Unfortunately there is no method overload for [Command] so I need to write several almost similar functions
                    // This one is needed to save on network traffic
                    syncInputs.forward = (sbyte)(inputs.forward * 127f);
                    syncInputs.strafe = (sbyte)(inputs.strafe * 127f);
                    syncInputs.vertical = (sbyte)(inputs.vertical * 127f);
                    if (Vector3.Distance(results.position, lastPosition) > 0f)
                    {
                        if (Quaternion.Angle(results.rotation, lastRotation) > 0f)
                            Cmd_MovementRotationInputs(syncInputs.forward, syncInputs.strafe, syncInputs.vertical, inputs.turn, inputs.sprint, inputs.crouch, inputs.timeStamp);
                        else
                            Cmd_MovementInputs(syncInputs.forward, syncInputs.strafe, syncInputs.vertical, inputs.sprint, inputs.crouch, inputs.timeStamp);
                    }
                    else
                    {
                        if (Quaternion.Angle(results.rotation, lastRotation) > 0f)
                            Cmd_RotationInputs(inputs.turn, inputs.crouch, inputs.timeStamp);
                        else if (results.crouching != lastCrouch)
                            Cmd_OnlyStances(inputs.crouch, inputs.timeStamp);
                    }
                }
            }
            else
            {
                if (hasAuthority)
                {
                    // Server

                    // Check if there is atleast one record in inputs list
                    if (inputsList.Count == 0)
                        return;

                    // Move and rotate part. Nothing interesting here
                    __inputs = inputsList[0];
                    inputsList.RemoveAt(0);

                    lastPosition = results.position;
                    lastRotation = results.rotation;
                    lastCrouch = results.crouching;

                    results.rotation = Rotate(__inputs, results);
                    results.sprinting = Sprint(__inputs, results);
                    results.crouching = Crouch(__inputs, results);
                    results.position = Move(__inputs, results);

                    // Sending results to other clients(state sync)
                    if (_dataStep >= syncInterval)
                    {
                        if (Vector3.Distance(results.position, lastPosition) > 0f
                            || Quaternion.Angle(results.rotation, lastRotation) > 0f
                            || results.crouching != lastCrouch)
                        {
                            // Struct need to be fully new to count as dirty 
                            // Convering some of the values to get less traffic
                            results.timeStamp = __inputs.timeStamp;
                            __tempResults.position = results.position;
                            __tempResults.turn = (ushort)(results.rotation.eulerAngles.y * 182f);
                            __tempResults.sprinting = results.sprinting;
                            __tempResults.crouching = results.crouching;
                            __tempResults.timeStamp = results.timeStamp;
                            syncResults = __tempResults;
                        }
                        _dataStep = 0;
                    }
                    _dataStep += Time.fixedDeltaTime;
                }
                else
                {
                    // Non-owner client a.k.a. dummy client
                    // there should be at least two records in the results list so it would be possible to interpolate between them in case if there would be some dropped packed or latency spike
                    // And yes this stupid structure should be here because it should start playing data when there are at least two records and continue playing even if there is only one record left 
                    if (resultsList.Count == 0)
                        _playData = false;

                    if (resultsList.Count >= 2)
                        _playData = true;

                    if (_playData)
                    {
                        if (_dataStep == 0f)
                        {
                            _startPosition = results.position;
                            _startRotation = results.rotation;
                        }
                        _step = 1f / syncInterval;
                        results.position = Vector3.Lerp(_startPosition, resultsList[0].position, _dataStep);
                        results.rotation = Quaternion.Slerp(_startRotation, resultsList[0].rotation, _dataStep);
                        results.sprinting = resultsList[0].sprinting;
                        results.crouching = resultsList[0].crouching;
                        _dataStep += _step * Time.fixedDeltaTime;
                        if (_dataStep >= 1f)
                        {
                            _dataStep = 0;
                            resultsList.RemoveAt(0);
                        }
                    }

                    UpdatePosition(results.position);
                    UpdateRotation(results.rotation);
                    UpdateSprinting(results.sprinting);
                    UpdateCrouch(results.crouching);
                }
            }
            Profiler.EndSample();
        }

        #endregion

        #region Helpers

        sbyte RoundToLargest(float inp)
        {
            if (inp > 0f)
                return 1;
            else if (inp < 0f)
                return -1;

            return 0;
        }

        #endregion

        #region ClientCallback

        // Updating Clients with server states
        [Client]
        public void RecieveResults(SyncResults syncResults)
        {
            Profiler.BeginSample("CC_RecieveResults");
            if (!isClient) return;
            Debug.LogFormat("RecieveResults {0}", syncResults.turn);

            // Converting values back
            rcvdResults.rotation = Quaternion.Euler(0, (float)syncResults.turn / 182, 0);

            rcvdResults.position = syncResults.position;
            rcvdResults.sprinting = syncResults.sprinting;
            rcvdResults.crouching = syncResults.crouching;
            rcvdResults.timeStamp = syncResults.timeStamp;

            // Discard out of order results
            if (rcvdResults.timeStamp <= _lastTimeStamp)
                return;

            _lastTimeStamp = rcvdResults.timeStamp;

            // Non-owner client
            if (!isLocalPlayer && !hasAuthority)
            {
                // Adding results to the results list so they can be used in interpolation process
                rcvdResults.timeStamp = Time.time;
                resultsList.Add(rcvdResults);
            }

            // Owner client
            // Server client reconciliation process should be executed in order to sync client's
            // rotation and position with server values but do it without jittering
            if (isLocalPlayer && !hasAuthority)
            {
                // Update client's position and rotation with ones from server 
                results.rotation = rcvdResults.rotation;
                results.position = rcvdResults.position;
                int foundIndex = -1;

                // Search recieved time stamp in client's inputs list
                for (int index = 0; index < inputsList.Count; index++)
                {
                    // If time stamp found run through all inputs starting from needed time stamp 
                    if (inputsList[index].timeStamp > rcvdResults.timeStamp)
                    {
                        foundIndex = index;
                        break;
                    }
                }

                if (foundIndex == -1)
                {
                    // Clear Inputs list if no needed records found 
                    while (inputsList.Count != 0)
                        inputsList.RemoveAt(0);

                    return;
                }

                // Replay recorded inputs
                for (int subIndex = foundIndex; subIndex < inputsList.Count; subIndex++)
                {
                    results.rotation = Rotate(inputsList[subIndex], results);
                    results.crouching = Crouch(inputsList[subIndex], results);
                    results.sprinting = Sprint(inputsList[subIndex], results);

                    results.position = Move(inputsList[subIndex], results);
                }

                // Remove all inputs before time stamp
                int targetCount = inputsList.Count - foundIndex;
                while (inputsList.Count > targetCount)
                    inputsList.RemoveAt(0);
            }
            Profiler.EndSample();
        }

        #endregion

        #region ServerCommands

        // Standing on spot
        [Command]
        void Cmd_OnlyStances(bool crouch, float timeStamp)
        {
            if (isServer && !isLocalPlayer)
            {
                Inputs inputs;
                inputs.forward = 0f;
                inputs.strafe = 0f;
                inputs.vertical = 0f;
                inputs.turn = 0;
                inputs.sprint = false;
                inputs.crouch = crouch;
                inputs.timeStamp = timeStamp;
                inputsList.Add(inputs);
            }
        }

        // Only rotation inputs sent 
        [Command]
        void Cmd_RotationInputs(float turn, bool crouch, float timeStamp)
        {
            if (isServer && !isLocalPlayer)
            {
                Inputs inputs;
                inputs.forward = 0f;
                inputs.strafe = 0f;
                inputs.vertical = 0f;
                inputs.turn = turn;
                inputs.sprint = false;
                inputs.crouch = crouch;
                inputs.timeStamp = timeStamp;
                inputsList.Add(inputs);
            }
        }

        // Rotation and movement inputs sent 
        [Command]
        void Cmd_MovementRotationInputs(sbyte forward, sbyte strafe, sbyte vertical, float turn, bool sprint, bool crouch, float timeStamp)
        {
            if (isServer && !isLocalPlayer)
            {
                Inputs inputs;
                inputs.forward = Mathf.Clamp((float)forward / 127f, -1f, 1f);
                inputs.strafe = Mathf.Clamp((float)strafe / 127f, -1f, 1f);
                inputs.vertical = Mathf.Clamp((float)vertical / 127f, -1f, 1f);
                inputs.turn = turn;
                inputs.sprint = sprint;
                inputs.crouch = crouch;
                inputs.timeStamp = timeStamp;
                inputsList.Add(inputs);
            }
        }

        // Only movements inputs sent
        [Command]
        void Cmd_MovementInputs(sbyte forward, sbyte strafe, sbyte vertical, bool sprint, bool crouch, float timeStamp)
        {
            if (isServer && !isLocalPlayer)
            {
                Inputs inputs;
                inputs.forward = Mathf.Clamp((float)forward / 127f, -1f, 1f);
                inputs.strafe = Mathf.Clamp((float)strafe / 127f, -1f, 1f);
                inputs.vertical = Mathf.Clamp((float)vertical / 127f, -1f, 1f);
                inputs.turn = 0f;
                inputs.sprint = sprint;
                inputs.crouch = crouch;
                inputs.timeStamp = timeStamp;
                inputsList.Add(inputs);
            }
        }

        #endregion

        #region Virtuals

        // Next virtual functions can be changed in inherited class for custom movement and rotation mechanics
        // So it would be possible to control for example humanoid or vehicle from one script just by changing controlled pawn

        public virtual void GetInputs(ref Inputs inputs)
        {
            Profiler.BeginSample("CC_GetInputs");
            // Don't use one frame events in this part
            // It would be processed incorrectly 
            inputs.strafe = RoundToLargest(Input.GetAxis("Horizontal"));
            inputs.forward = RoundToLargest(Input.GetAxis("Vertical"));

            if (mouseSteer)
            {
                inputs.turn = Input.GetAxis("Mouse X") * mouseSensitivity;
            }
            else
            {
                if ((Input.GetKey(turnLeft)) && (turn > -turnSpeedMax))
                    turn = turn - turnSpeedAccel;
                else if ((Input.GetKey(turnRight)) && (turn < turnSpeedMax))
                    turn = turn + turnSpeedAccel;
                else
                {
                    if (turn > turnSpeedDecel)
                        turn = turn - turnSpeedDecel;
                    else if (turn < -turnSpeedDecel)
                        turn = turn + turnSpeedDecel;
                    else
                        turn = 0;
                }

                inputs.turn = turn;
            }

            float verticalTarget = -1;
            if (isGrounded)
            {
                isJumping = Input.GetKey(jump);

                if (!isJumping)
                {
                    isSprinting = Input.GetKey(sprint);

                    if (!isSprinting)
                        isCrouching = Input.GetKey(crouch);
                    else
                        isCrouching = false;
                }
                else
                {
                    isCrouching = false;
                    isSprinting = false;
                }

                inputs.sprint = isSprinting;
                inputs.crouch = isCrouching;

                verticalTarget = 0;
                inputs.vertical = 0;
            }

            if (isJumping)
            {
                verticalTarget = 1;

                if (inputs.vertical >= 0.9f)
                    isJumping = false;
            }

            inputs.vertical = Mathf.Lerp(inputs.vertical, verticalTarget, 10f * Time.deltaTime);
            Profiler.EndSample();
        }


        Vector3 sphereCastOrigin;
        float sphereCastDistance;

        public virtual bool Grounded()
        {
            if (characterController == null) return true;

            Profiler.BeginSample("CC_Grounded");
            sphereCastOrigin = transform.position + new Vector3(0, -characterController.height * 0.5f + characterController.radius, 0);
            sphereCastDistance = characterController.skinWidth + .01f;
            Debug.DrawRay(sphereCastOrigin, new Vector3(0, -characterController.skinWidth, 0), Color.red);
            Profiler.EndSample();
            //return Physics.Raycast(origin, Vector3.down, maxDistance, ~layerMask);
            return Physics.SphereCast(new Ray(sphereCastOrigin, Vector3.down), characterController.radius, sphereCastDistance, ~layerMask);
        }

        public virtual void UpdatePosition(Vector3 newPosition)
        {
            if (Vector3.Distance(newPosition, transform.position) > snapDistance)
                transform.position = newPosition;
            else
                if (characterController != null) characterController.Move(newPosition - transform.position);
        }

        public virtual void UpdateRotation(Quaternion newRotation)
        {
            transform.rotation = Quaternion.Euler(0, newRotation.eulerAngles.y, 0);
        }

        public virtual void UpdateSprinting(bool sprinting) { }

        public virtual void UpdateCrouch(bool crouch) { }


        Vector3 moveVector;
        Vector3 moveDirection;
        float moveSpeed;

        public virtual Vector3 Move(Inputs inputs, Results current)
        {
            Profiler.BeginSample("CC_Move");
            transform.position = current.position;
            moveSpeed = groundSpeed;

            if (current.crouching)
                moveSpeed = groundSpeed * .5f;

            if (current.sprinting)
                moveSpeed = groundSpeed * 1.6f;

            if (inputs.vertical > 0)
                verticalSpeed = inputs.vertical * jumpHeight;
            else
                verticalSpeed = inputs.vertical * Physics.gravity.magnitude;

            if (characterController != null)
            {
                moveVector = Vector3.ClampMagnitude(new Vector3(inputs.strafe, 0, inputs.forward), 1) * moveSpeed;
                moveVector += new Vector3(0, verticalSpeed, 0);
                moveDirection = transform.TransformDirection(moveVector * Time.fixedDeltaTime);
                characterController.Move(moveDirection);
                //characterController.Move(transform.TransformDirection((Vector3.ClampMagnitude(new Vector3(inputs.strafe, 0, inputs.forward), 1) * speed) + new Vector3(0, verticalSpeed, 0)) * Time.fixedDeltaTime);
            }
            Profiler.EndSample();
            return transform.position;
        }

        public virtual bool Sprint(Inputs inputs, Results current)
        {
            return inputs.sprint;
        }

        public virtual bool Crouch(Inputs inputs, Results current)
        {
            return inputs.crouch;
        }


        float yRotation;

        public virtual Quaternion Rotate(Inputs inputs, Results current)
        {
            Profiler.BeginSample("CC_Rotate");
            transform.rotation = current.rotation;
            yRotation = transform.eulerAngles.y + inputs.turn * Time.fixedDeltaTime;
            transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            Profiler.EndSample();
            return transform.rotation;
        }

        #endregion
    }
}
