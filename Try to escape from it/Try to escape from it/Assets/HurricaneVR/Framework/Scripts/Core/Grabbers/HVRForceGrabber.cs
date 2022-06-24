using System.Collections;
using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Core.Bags;
using HurricaneVR.Framework.Core.Utils;
using HurricaneVR.Framework.Shared;
using HurricaneVR.Framework.Shared.Utilities;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HurricaneVR.Framework.Core.Grabbers
{
    public class HVRForceGrabber : HVRGrabberBase
    {
        public HVRForceGrabberLaser Laser;
        public HVRHandGrabber HandGrabber;
        public HVRTriggerGrabbableBag AutoGrabBag;
        public float ForceTime = 1f;
        public float YOffset = .3f;
        public float AdditionalAutoGrabTime = 1f;
        public bool RequiresFlick;
        public AudioClip SFXGrab;

        public float MaximumVelocityPostCollision = 5f;
        public float MaximumVelocityAutoGrab = 5f;

        public HVRPlayerInputs Inputs => HandGrabber.Inputs;

        private bool _grabbableCollided;

        public override Vector3 JointAnchorWorldPosition => HandGrabber.JointAnchorWorldPosition;

        public float FlickStartThreshold = 1.25f;
        public float FlickEndThreshold = .25f;
        private bool _canFlick;

        public float QuickMoveThreshold = 1.25f;
        public float QuickMoveResetThreshold = .25f;
        private bool _canQuickStart;
        private Coroutine _additionalGrabRoutine;

        public float VelocityMagnitude => HandGrabber.HVRTrackedController.VelocityMagnitude;
        public float AngularVelocityMagnitude => HandGrabber.HVRTrackedController.AngularVelocityMagnitude;

        public HVRHandSide HandSide => HandGrabber.HandSide;

        public bool IsForceGrabbing { get; private set; }

        protected override void Start()
        {
            base.Start();

            if (!HandGrabber)
            {
                HandGrabber = GetComponentInChildren<HVRHandGrabber>();
            }

            if (!HandGrabber)
            {
                Debug.LogWarning("Cannot find HandGrabber. Make sure to assign or have it on this level or below.");
            }

            AutoGrabBag.Grabber = HandGrabber;
        }


        protected override void Update()
        {
            CheckFlick();
            CheckDrawRay();
            CheckGripButtonGrab();
        }

        private void CheckFlick()
        {
            if (!RequiresFlick)
                return;

            if (IsGrabbing || !IsHovering || !Inputs.GetForceGrabActive(HandSide))
            {
                return;
            }

            if (_canFlick && AngularVelocityMagnitude > FlickStartThreshold)
            {
                TryGrab(HoverTarget);
                _canFlick = false;
            }

            if (AngularVelocityMagnitude < FlickEndThreshold)
            {
                _canFlick = true;
            }

            if (VelocityMagnitude < QuickMoveResetThreshold)
            {
                _canQuickStart = true;
            }

            if (_canQuickStart && VelocityMagnitude > QuickMoveThreshold)
            {
                TryGrab(HoverTarget);
                _canQuickStart = false;
            }
        }

        private void CheckGripButtonGrab()
        {
            if (!RequiresFlick && !IsGrabbing && IsHovering && Inputs.GetForceGrabActivated(HandSide))
            {
                TryGrab(HoverTarget);
            }
        }


        private void CheckDrawRay()
        {
            if (!RequiresFlick)
                return;

            if (!IsGrabbing && HoverTarget && Inputs.GetForceGrabActive(HandSide))
            {
                Laser.Enable(HoverTarget.transform);
            }
            else
            {
                Laser.Disable();
            }
        }


        protected override void CheckUnHover()
        {
            if (!HandGrabber.IsGrabbing && Inputs.GetForceGrabActive(HandSide) && HoverTarget && !HoverTarget.IsBeingForcedGrabbed && !HoverTarget.IsBeingHeld)
                return;
            base.CheckUnHover();
        }

        public override bool CanGrab(HVRGrabbable grabbable)
        {
            if (grabbable.IsBeingHeld)
                return false;
            if (HandGrabber.IsGrabbing) return false;
            if (!grabbable.ForceGrabbable) return false;
            if (HandGrabber.IsValidGrabbable(grabbable)) return false;
            if (grabbable == GrabbedTarget && grabbable.IsBeingForcedGrabbed) return true;
            if ((GrabbedTarget != null && GrabbedTarget != grabbable) || !IsHovering) return false;
            if (HandGrabber.ForceGrabCheck())
                return false;
            return base.CanGrab(grabbable);
        }

        public override bool CanHover(HVRGrabbable grabbable)
        {
            if (HandGrabber.IsGrabbing) return false;
            if (!grabbable.ForceGrabbable) return false;
            if (grabbable.IsBeingForcedGrabbed) return false;
            if (HandGrabber.IsValidGrabbable(grabbable)) return false;
            if (IsHovering) return false;
            var closest = GetClosestGrabbable();
            if (!closest || grabbable != closest) return false;
            if (grabbable.IsBeingHeld) return false;
            if (HandGrabber.ForceGrabCheck())
                return false;
            return base.CanHover(grabbable);
        }

        protected override void OnGrabbed(HVRGrabArgs args)
        {
            //Debug.Log($"force grabbed!");
            base.OnGrabbed(args);

            if (_additionalGrabRoutine != null)
            {
                StopCoroutine(_additionalGrabRoutine);
            }

            IsForceGrabbing = true;
            StartCoroutine(ForceGrab(args.Grabbable));

            Grabbed.Invoke(this, args.Grabbable);
            args.Grabbable.Collided.AddListener(OnGrabbableCollided);
            args.Grabbable.Grabbed.AddListener(OnGrabbableGrabbed);

            if (SFXGrab)
                SFXPlayer.Instance.PlaySFX(SFXGrab, transform.position);
        }



        public IEnumerator ForceGrab(HVRGrabbable grabbable)
        {
            try
            {
                _grabbableCollided = false;
                IsHoldActive = true;
            

                grabbable.IsBeingForcedGrabbed = true;
                grabbable.Rigidbody.useGravity = false;
                grabbable.Rigidbody.drag = 0f;

                var grabPoint = grabbable.GetForceGrabPoint(HandGrabber.HandSide);

                fts.solve_ballistic_arc_lateral(false,
                    grabPoint.position,
                    ForceTime,
                    JointAnchorWorldPosition,
                    JointAnchorWorldPosition.y + YOffset,
                    out var velocity,
                    out var gravity);

                grabbable.Rigidbody.velocity = velocity;


                var originalPosition = grabPoint.position;

                var previousPosition = originalPosition;

                var elapsed = 0f;

                var targetRotation = grabbable.GetForceGrabPointRotation(HandGrabber.HandSide);
                var startHandRotation = HandGrabber.HandModelRotator.rotation;

                while (GrabbedTarget)
                {
                    if (elapsed > ForceTime)
                    {
                        break;
                    }

                    var currentVector = JointAnchorWorldPosition - grabPoint.position;
                    var oldVector = JointAnchorWorldPosition - previousPosition;

                    currentVector.y = 0;
                    oldVector.y = 0;

                    var percentTime = elapsed / ForceTime;
                    var yExtra = YOffset * (1 - percentTime);

                    if (percentTime < .3) _grabbableCollided = false;
                    else if (_grabbableCollided)
                    {
                        if (grabbable.Rigidbody.velocity.magnitude > MaximumVelocityPostCollision)
                            grabbable.Rigidbody.velocity = grabbable.Rigidbody.velocity.normalized * MaximumVelocityPostCollision;
                        ForceRelease();
                        //Debug.Log($"Collided while force grabbing.");
                        break;
                    }

                    var angleDelta = Mathf.Abs(Vector3.Angle(currentVector, oldVector));
                    if (angleDelta > 20)
                    {
                        //Debug.Log($"{angleDelta}");
                    }

                    fts.solve_ballistic_arc_lateral(
                        false,
                        grabPoint.position,
                        ForceTime - elapsed,
                        JointAnchorWorldPosition,
                        JointAnchorWorldPosition.y + yExtra,
                        out velocity, out gravity);

                    grabbable.Rigidbody.velocity = velocity;
                    grabbable.Rigidbody.AddForce(-Vector3.up * gravity, ForceMode.Acceleration);

                    if (AutoGrabBag.ClosestGrabbable && AutoGrabBag.ClosestGrabbable == GrabbedTarget)
                    {
                        if (HandGrabber.TryAutoGrab(GrabbedTarget))
                        {
                            IsForceGrabbing = false;
                            break;
                        }
                    }

                    if (currentVector.magnitude < .1f)
                    {
                        //Debug.Log($"<.1f");
                        break;
                    }

                    var handDelta = Quaternion.Angle(HandGrabber.HandModelRotator.rotation, startHandRotation);
                    if (Mathf.Abs(handDelta) > 20)
                    {
                        startHandRotation = HandGrabber.HandModelRotator.rotation;
                    }

                    //todo
                    var delta = startHandRotation * Quaternion.Inverse(targetRotation);

                    delta.ToAngleAxis(out var angle, out var axis);

                    if (angle > 180.0f) angle -= 360.0f;

                    var remaining = ForceTime - elapsed;

                    if (percentTime > .3f && Mathf.Abs(angle) > 1 && remaining > .01)
                    {
                        grabbable.Rigidbody.angularVelocity = axis * (angle * Mathf.Deg2Rad) / ForceTime;
                    }
                    else
                    {
                        grabbable.Rigidbody.angularVelocity = Vector3.zero;
                    }


                    previousPosition = grabPoint.position;
                    elapsed += Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                }
            }
            finally
            {
                IsHoldActive = false;
                grabbable.IsBeingForcedGrabbed = false;
                grabbable.Collided.RemoveListener(OnGrabbableCollided);
                grabbable.Grabbed.RemoveListener(OnGrabbableGrabbed);
                if (GrabbedTarget)
                {
                    ForceRelease();
                }

                IsForceGrabbing = false;
            }

            if (AdditionalAutoGrabTime > 0 && !grabbable.IsBeingHeld)
            {
                _additionalGrabRoutine = StartCoroutine(ContinueAutoGrab(grabbable));
            }
        }

        private IEnumerator ContinueAutoGrab(HVRGrabbable grabbable)
        {
            var elapsed = 0f;
            while (grabbable && elapsed < AdditionalAutoGrabTime && !grabbable.IsBeingHeld)
            {
                if (grabbable.Rigidbody.velocity.magnitude > MaximumVelocityAutoGrab)
                    grabbable.Rigidbody.velocity *= .9f;

                if (AutoGrabBag.ClosestGrabbable && AutoGrabBag.ClosestGrabbable == grabbable)
                {
                    if (HandGrabber.TryAutoGrab(grabbable))
                    {
                        break;
                    }
                }

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            _additionalGrabRoutine = null;
        }

        private void OnGrabbableGrabbed(HVRGrabberBase arg0, HVRGrabbable grabbable)
        {
            //Debug.Log($"Grabbed while force grabbing.");
        }

        private void OnGrabbableCollided(HVRGrabbable g)
        {
            _grabbableCollided = true;
        }
    }
}