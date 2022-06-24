using System;
using System.Collections;
using System.Collections.Generic;
using HurricaneVR.Framework.Components;
using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.SpatialTracking;

namespace HurricaneVR.Framework.Core.Player
{
    public class HVRRigidPlayerController : MonoBehaviour
    {
        public bool WaitForHMDActive = true;
        public HVRCameraRig CameraRig;
        public HVRHeadCollision HeadCollision;
        public Transform Camera;
        public Transform FloorOffset;
        public Transform Waist;

        public Rigidbody LocoBall;

        public float HeadCollisionFadeSpeed = 1f;

        [Tooltip("If true, when your head collides it returns your head to the body's position")]
        public bool HeadCollisionPushesBack = true;

        [Tooltip("If true, limits the head distance from the body by MaxLean amount.")]
        public bool LimitHeadDistance = true;

        public float DoubleClickThreshold = .25f;

        [Tooltip("If LimitHeadDistance is true, the max distance your head can be from your body.")]
        public float MaxLean = .5f;

        public float Acceleration;
        public float WalkMaxAngularVelocity;
        public float RunMaxAngularVelocity;
        
        public float MinHeight = .3f;

        public float Gravity = 2.50f;
        public float MaxFallSpeed = 2f;

        public RotationType RotationType;
        public float SmoothTurnSpeed = 90f;
        public float SmoothTurnThreshold = .1f;

        public float SnapAmount = 45f;
        public float SnapThreshold = .75f;

        [Tooltip("Player height must be above this to toggle crouch.")]
        public float CrouchMinHeight = 1.2f;

        [Tooltip("Player height after toggling a crouch via controller.")]
        public float CrouchHeight = 0.7f;

        [Tooltip("Speed at which toggle crouch moves the player up and down.")]
        public float CrouchSpeed = 1.5f;

        [Tooltip("Offset from the camera to determine the waist height.")]
        public float WaistOffset = .6f;

        public HVRHandGrabber LeftHand;
        public HVRHandGrabber RightHand;

        public Transform LeftControllerTransform;
        public Transform RightControllerTransform;

        public float RotationTeleportThreshold = .3f;

        public Vector2 MouseSensitivity = new Vector2(1f, 1f);

        public Rigidbody RigidBody { get; private set; }
        public HVRTeleporter Teleporter { get; private set; }

        public float Height => 0f;
        public bool IsCrouching => Height < CrouchMinHeight;

        public bool IsClimbing => LeftHand && LeftHand.IsClimbing || RightHand && RightHand.IsClimbing;

        public bool Sprinting { get; set; }

        public HVRPlayerInputs Inputs { get; private set; }

        private Vector3 _previousLeftControllerPosition;
        private Vector3 _previousRightControllerPosition;

        public Vector3 RightPosition;

        private bool _waitingForHMDActive;
        private bool _waitingForCameraMovement;
        private float _timeSinceLastRotation;
        private Quaternion _previousRotation;

        private Transform _leftParent;
        private Transform _rightParent;

        private Transform _leftGrabbableParent;
        private Transform _rightGrabbableParent;

        private float _timeSinceLastPress;
        private bool _awaitingSecondClick;

        private bool _crouchInProgress;
        private bool _cameraBelowCrouchHeight = false;
        private Coroutine _crouchRoutine;
        private float _previousTurnAxis;
        private float _crouchOffset;
        private bool _isCrouchingToggled;
        private bool _isCameraCorrecting = false;

        private void Awake()
        {
            RigidBody = GetComponent<Rigidbody>();
            
            //Teleporter = GetComponent<HVRTeleporter>();

            //Teleporter.BeforeTeleport.AddListener(OnBeforeTeleport);
            //Teleporter.AfterTeleport.AddListener(OnAfterTeleport);
            Inputs = GetComponent<HVRPlayerInputs>();
        }

        private IEnumerator CorrectCamera()
        {
            _isCameraCorrecting = true;
            var fader = HVRManager.Instance.ScreenFader;

            var delta = transform.position - Camera.position;
            delta.y = 0f;

            if (!fader)
            {
                CameraRig.transform.position += delta;
                _isCameraCorrecting = false;
                yield break;
            }

            fader.Fade(1, HeadCollisionFadeSpeed);

            while (fader.CurrentFade < .9)
            {
                yield return null;
            }

            delta = transform.position - Camera.position;
            delta.y = 0f;
            CameraRig.transform.position += delta;

            fader.Fade(0, HeadCollisionFadeSpeed);

            while (fader.CurrentFade > .1)
            {
                yield return null;
            }

            _isCameraCorrecting = false;
        }

        private void OnAfterTeleport()
        {
            if (_leftParent)
                LeftHand.transform.SetParent(_leftParent, true);
            else
                LeftHand.transform.parent = null;

            if (_rightParent)
                RightHand.transform.SetParent(_leftParent, true);
            else
                RightHand.transform.parent = null;

            if (LeftHand.GrabbedTarget)
            {
                if (_leftGrabbableParent)
                    LeftHand.GrabbedTarget.transform.SetParent(_leftGrabbableParent, true);
                else
                    LeftHand.GrabbedTarget.transform.parent = null;
            }

            if (LeftHand.GrabbedTarget != RightHand.GrabbedTarget && RightHand.GrabbedTarget)
            {
                if (_rightGrabbableParent)
                    RightHand.GrabbedTarget.transform.SetParent(_rightGrabbableParent, true);
                else
                    RightHand.GrabbedTarget.transform.parent = null;
            }


            _leftGrabbableParent = null;
            _rightGrabbableParent = null;
        }

        private void OnBeforeTeleport()
        {
            _leftParent = LeftHand.transform.parent;
            _rightParent = RightHand.transform.parent;

            LeftHand.transform.SetParent(transform, true);
            RightHand.transform.SetParent(transform, true);

            if (LeftHand.GrabbedTarget)
            {
                _leftGrabbableParent = LeftHand.GrabbedTarget.transform.parent;
                LeftHand.GrabbedTarget.transform.SetParent(LeftHand.transform, true);
            }

            if (LeftHand.GrabbedTarget != RightHand.GrabbedTarget && RightHand.GrabbedTarget)
            {
                _rightGrabbableParent = RightHand.GrabbedTarget.transform.parent;
                RightHand.GrabbedTarget.transform.SetParent(RightHand.transform, true);
            }

        }

        private void Start()
        {
            _waitingForHMDActive = WaitForHMDActive;

            if (HVRInputManager.Instance.HMDActive)
            {
                _waitingForHMDActive = false;
            }
            else
            {
                HVRInputManager.Instance.HMDFirstActivation.AddListener(OnIntialHMDActivation);
            }
        }

        private void OnIntialHMDActivation()
        {
            StartCoroutine(CheckCameraOffset());
        }

        private IEnumerator CheckCameraOffset()
        {
            yield return null;
            yield return null;

            var delta = Camera.localPosition;

            _waitingForHMDActive = false;
            _waitingForCameraMovement = true;
        }

        private void Update()
        {
            CheckCameraCorrection();
            CheckSprinting();
            //UpdateHeight();
            CheckCrouching();
            CameraRig.PlayerControllerYOffset = _crouchOffset;
        }

        private void CheckCameraCorrection()
        {
            if (HeadCollisionPushesBack && HeadCollision && HeadCollision.IsColliding && !_isCameraCorrecting)
            {
                StartCoroutine(CorrectCamera());
            }
        }

        private void FixedUpdate()
        {
            //if (_waitingForHMDActive)
            //{
            //    return;
            //}

            //if (_waitingForCameraMovement)
            //{
            //    if (Camera.localPosition == Vector3.zero)
            //    {
            //        return;
            //    }

            //    var delta = Camera.transform.position - transform.position;
            //    delta.y = 0f;
            //    if (delta.magnitude > 0.0f)
            //    {
                    
            //    }
            //    _waitingForCameraMovement = false;
            //}



            //if (CharacterController.enabled)
            {
                HandleMovement();
                HandleRotation();
            }

            //CheckLean();

            RightPosition = RightControllerTransform.position;

            if (Quaternion.Angle(transform.rotation, _previousRotation) > 1f)
            {
                _timeSinceLastRotation = 0f;
            }
            else
            {
                _timeSinceLastRotation += Time.deltaTime;
            }



            //if (Teleporter)
            //{
            //    if (_timeSinceLastRotation < RotationTeleportThreshold && !Teleporter.IsAiming ||
            //        IsClimbing || !CharacterController.isGrounded)
            //    {
            //        Teleporter.Disable();
            //    }
            //    else
            //    {
            //        Teleporter.Enable();
            //    }
            //}

            _previousLeftControllerPosition = LeftControllerTransform.position;
            _previousRightControllerPosition = RightControllerTransform.position;
            _previousRotation = transform.rotation;
        }

        //private void CheckLean()
        //{
        //    if (_isCameraCorrecting)
        //        return;
        //    var delta = Camera.transform.position - CharacterController.transform.position;
        //    delta.y = 0;
        //    if (delta.magnitude > MaxLean)
        //    {
        //        var allowedPosition = CharacterController.transform.position + delta.normalized * MaxLean;
        //        var difference = allowedPosition - Camera.transform.position;
        //        difference.y = 0f;
        //        CameraRig.transform.position += difference;
        //    }
        //}

        //private void UpdateHeight()
        //{
        //    CharacterController.height = Mathf.Clamp(CameraRig.AdjustedCameraHeight, MinHeight, CameraRig.AdjustedCameraHeight);
        //    CharacterController.center = new Vector3(0, CharacterController.height * .5f + CharacterController.skinWidth, 0f);

        //    if (Waist)
        //    {
        //        var waistLocalPosition = Waist.transform.localPosition;
        //        waistLocalPosition.y = CameraRig.AdjustedCameraHeight - WaistOffset;
        //        Waist.transform.localPosition = waistLocalPosition;
        //    }
        //}

        private void HandleHMDMovement()
        {
            var originalCameraPosition = CameraRig.transform.position;
            var originalCameraRotation = CameraRig.transform.rotation;

            //var delta = Camera.transform.position - CharacterController.transform.position;
            //delta.y = 0f;
            //if (delta.magnitude > 0.0f && CharacterController.enabled)
            //{
            //    CharacterController.Move(delta);
            //}

            transform.rotation = Quaternion.Euler(0.0f, Camera.rotation.eulerAngles.y, 0.0f);

            CameraRig.transform.position = originalCameraPosition;
            var local = CameraRig.transform.localPosition;
            local.y = 0f;
            CameraRig.transform.localPosition = local;
            CameraRig.transform.rotation = originalCameraRotation;
        }

        private void HandleRotation()
        {
            if (Teleporter != null && Teleporter.IsAiming)
            {
                return;
            }

            if (RotationType == RotationType.Smooth)
            {
                HandleSmoothRotation();
            }
            else if (RotationType == RotationType.Snap)
            {
                HandleSnapRotation();
            }

            if (Input.GetMouseButton(1))
            {
                var offset = Quaternion.Euler(new Vector3(0, Input.GetAxis("Mouse X") * MouseSensitivity.x, 0));
                transform.rotation *= offset;

                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }

            _previousTurnAxis = GetTurnAxis().x;
        }

        private void HandleSnapRotation()
        {
            var input = GetTurnAxis().x;
            if (Math.Abs(input) < SnapThreshold || Mathf.Abs(_previousTurnAxis) > SnapThreshold)
                return;

            var rotation = Quaternion.Euler(0, Mathf.Sign(input) * SnapAmount, 0);
            transform.rotation *= rotation;
        }

        private void HandleSmoothRotation()
        {
            var input = GetTurnAxis().x;
            if (Math.Abs(input) < SmoothTurnThreshold)
                return;

            var rotation = input * SmoothTurnSpeed * Time.fixedDeltaTime;
            var rotationVector = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y + rotation, transform.eulerAngles.z);
            transform.rotation = Quaternion.Euler(rotationVector);
        }



        private void HandleMovement()
        {
            if (IsClimbing)
            {
                return;
            }

            HandleHMDMovement();

            var maxVelocity = WalkMaxAngularVelocity;

            if (Sprinting)
                maxVelocity = RunMaxAngularVelocity;

            var movement = GetMovementAxis();

            var acceleration = Acceleration * (transform.forward * movement.y + transform.right * movement.x);

            LocoBall.angularVelocity += acceleration;
            if (LocoBall.angularVelocity.magnitude > maxVelocity)
            {
                LocoBall.angularVelocity = maxVelocity * LocoBall.angularVelocity.normalized;
            }

            var wasd = CheckWASD();

            acceleration = Acceleration * (transform.forward * wasd.y + transform.right * wasd.x);

            LocoBall.angularVelocity += acceleration;
            if (LocoBall.angularVelocity.magnitude > maxVelocity)
            {
                LocoBall.angularVelocity = maxVelocity * LocoBall.angularVelocity.normalized;
            }
        }

        private Vector2 CheckWASD()
        {
            var x = 0f;
            var y = 0f;
            if (Input.GetKey(KeyCode.W))
                y += 1f;
            if (Input.GetKey(KeyCode.S))
                y -= 1f;
            if (Input.GetKey(KeyCode.A))
                x += -1f;
            if (Input.GetKey(KeyCode.D))
                x += 1f;

            return new Vector2(x, y);
        }


        private Vector3 _playerVelocity;
        public Vector3 Forward;
        public Vector3 Right;

        protected virtual Vector2 GetMovementAxis()

        {
            return Inputs.MovementAxis;
        }

        protected virtual Vector2 GetTurnAxis()
        {
            return Inputs.TurnAxis;
        }

        protected virtual void CheckSprinting()
        {
            if (Inputs.SprintRequiresDoubleClick)
            {
                if (_awaitingSecondClick)
                {
                    _timeSinceLastPress += Time.deltaTime;
                }

                //if (Sprinting && !LeftController.TrackpadButtonState.Active)
                //{
                //    Sprinting = false;
                //}
                //else 
                if (!Sprinting && Inputs.IsSprintingActivated)
                {
                    if (_timeSinceLastPress < DoubleClickThreshold && _awaitingSecondClick)
                    {
                        Sprinting = true;
                        _awaitingSecondClick = false;
                    }
                    else
                    {
                        _timeSinceLastPress = 0f;
                        _awaitingSecondClick = true;
                    }
                }
            }
            else
            {

                if (Sprinting && Inputs.IsSprintingActivated)
                    Sprinting = false;
                else if (!Sprinting && Inputs.IsSprintingActivated)
                    Sprinting = true;
            }

            if (GetMovementAxis().magnitude < .01f)
            {
                Sprinting = false;
            }
        }



        private void CheckCrouching()
        {
            if (!_crouchInProgress && Height >= CrouchMinHeight)
            {
                if (Inputs.IsCrouchActivated)
                {
                    //Debug.Log($"toggled crouching");
                    Crouch();
                }
                else if (_isCrouchingToggled)
                {
                    //Debug.Log($"forced out of toggled crouch");
                    StopCrouching();
                }
            }
            else if (_isCrouchingToggled && Inputs.IsCrouchActivated)
            {
                //Debug.Log($"untoggled crouching");
                StopCrouching();
            }

            if (IsCrouching && _isCrouchingToggled)
            {
                if (_cameraBelowCrouchHeight && CameraRig.AdjustedCameraHeight > CrouchHeight)
                {
                    StopCrouching();
                }
                else if (CameraRig.AdjustedCameraHeight < (CrouchHeight - MinHeight) / 2f)
                {
                    _cameraBelowCrouchHeight = true;
                }
            }

        }

        private void Crouch()
        {
            _isCrouchingToggled = true;
            var target = CrouchHeight - Height;
            _cameraBelowCrouchHeight = false;
            if (_crouchRoutine != null)
                StopCoroutine(_crouchRoutine);
            _crouchRoutine = StartCoroutine(CrouchRoutine(target, true));
        }

        private void StopCrouching()
        {
            _isCrouchingToggled = false;
            _cameraBelowCrouchHeight = false;
            if (_crouchRoutine != null)
                StopCoroutine(_crouchRoutine);
            _crouchRoutine = StartCoroutine(CrouchRoutine(0f, false));
        }

        private IEnumerator CrouchRoutine(float target, bool crouching)
        {
            _crouchInProgress = true;

            var total = 0f;

            float delta;
            float min;
            float max;
            float sign;
            if (crouching)
            {
                delta = _crouchOffset - target;
                sign = -1;
                min = target;
                max = 0f;
            }
            else
            {
                delta = 0 - _crouchOffset;
                sign = 1;
                min = _crouchOffset;
                max = 0f;
            }

            while (total < delta)
            {
                _crouchOffset += sign * Time.deltaTime * CrouchSpeed;
                total += Time.deltaTime * CrouchSpeed;

                _crouchOffset = Mathf.Clamp(_crouchOffset, min, max);

                yield return new WaitForEndOfFrame();
            }

            _crouchInProgress = false;
        }

        public virtual void IgnoreCollision(IEnumerable<Collider> colliders)
        {
            foreach (var otherCollider in colliders)
            {
                //if (otherCollider)
                    //Physics.IgnoreCollision(CharacterController, otherCollider, true);
            }
        }

        /// <summary>
        /// Removes components not necessary on other players rigs
        /// </summary>
        public void RemoveMultiplayerComponents()
        {
            foreach (var t in new[]{
                typeof(HVRCamera),
                typeof(Camera),
                typeof(AudioListener),
                typeof(TrackedPoseDriver),
                typeof(HVRScreenFade),
                typeof(HVRHeadCollision),
                typeof(HVRThrowingCenterOfMass),
                typeof(HVRControllerOffset), typeof(HVRJointHand)
            })
            {
                foreach (var component in GetComponentsInChildren(t))
                {
                    Destroy(component);
                }
            }

        }
    }


  

}