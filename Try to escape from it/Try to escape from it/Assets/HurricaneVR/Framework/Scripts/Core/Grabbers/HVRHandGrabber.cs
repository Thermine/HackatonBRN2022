using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HurricaneVR.Framework.Components;
using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Core.Bags;
using HurricaneVR.Framework.Core.Player;
using HurricaneVR.Framework.Core.Utils;
using HurricaneVR.Framework.Shared;
using HurricaneVR.Framework.Shared.HandPoser;
using HurricaneVR.Framework.Shared.Utilities;
using HurricaneVR.Framework.Weapons;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HurricaneVR.Framework.Core.Grabbers
{
    public class HVRHandGrabber : HVRGrabberBase
    {
        [Tooltip("HVRSocketBag used for placing and removing from sockets")]
        public HVRSocketBag SocketBag;


        [Header("Grab Settings")]
        [Tooltip("If in a networked game, can someone take this an object from your hand?")]
        public bool AllowMultiplayerSwap;

        [Tooltip("Default hand pose to fall back to.")]
        public HVRHandPoser FallbackPoser;
        [Tooltip("Hold down or Toggle grabbing")]
        public HVRButtonTrigger GrabTrigger = HVRButtonTrigger.Active;
        [Tooltip("Left or right hand.")]
        public HVRHandSide HandSide;

        [Tooltip("Vibration strength when hovering over something you can pick up.")]
        public float HapticsAmplitude = .1f;
        [Tooltip("Vibration durection when hovering over something you can pick up.")]
        public float HapticsDuration = .1f;

        [Tooltip("Ignores hand model parenting distance check.")]
        public bool IgnoreParentingDistance;
        [Tooltip("Ignores hand model parenting angle check.")]
        public bool IgnoreParentingAngle;

        [Tooltip("Angle to meet before hand model parents to the grabbable.")]
        public float ParentingMaxAngleDelta = 20f;
        [Tooltip("Distance to meet before hand model parents to the grabbable")]
        public float ParentingMaxDistance = .01f;

        [Tooltip("Settings used to pull and rotate the object into position")]
        public HVRJointSettings PullingSettings;

        [Tooltip("Layer mask to determine line of sight to the grabbable.")]
        public LayerMask RaycastLayermask;

        [Header("Components")]

        [Tooltip("The hand animator component, loads from children on startup if not supplied.")]
        public HVRHandAnimator HandAnimator;

        [Tooltip("Component that holds collider information about the hands. Auto populated from children if not set.")]
        public HVRHandPhysics HandPhysics;
        public HVRPlayerInputs Inputs;
        public HVRPhysicsPoser PhysicsPoser;
        public HVRForceGrabber ForceGrabber;

        [Header("Required Transforms")]
        [Tooltip("Object holding the hand model.")]
        public Transform HandModel;

        [Tooltip("Configurable joints are anchored here")]
        public Transform JointAnchor;
        [Tooltip("Used to shoot ray casts at the grabbable to check if there is line of sight before grabbing.")]
        public Transform RaycastOrigin;
        [Tooltip("The transform that is handling device tracking.")]
        public Transform TrackedController;

        [Tooltip("Physics hand that will prevent the grabber from going through walls while you're holding something.")]
        public Transform InvisibleHand;

        [Tooltip("Sphere collider that checks when collisions should be re-enabled between a released grabbable and this hand.")]
        public Transform OverlapSizer;

        [Header("Dynamic Grabber")]

        [Tooltip("When dynamic solve grabbing, the velocity threshold of the grabbable to instantly grab the item")]
        public float InstantVelocityThreshold = 1f;
        [Tooltip("When dynamic solve grabbing, the angular velocity threshold of the grabbable to instantly grab the item")]
        public float InstantAngularThreshold = 1f;

        [Tooltip("How fast the physics poser hand will move towards the grabbable.")]
        public float PhysicsPoserVelocity = .75f;


        [Tooltip("Sphere collider that sizes the physics grabber check for closest collider.")]
        public Transform PhysicsGrabberSizer;




        [Header("Throw Settings")]
        [Tooltip("Factor to apply to the linear velocity of the throw.")]
        public float ReleasedVelocityFactor = 1.1f;

        [Tooltip("Factor to apply to the angular to linear calculation.")]
        public float ReleasedAngularConversionFactor = 1.0f;

        [Tooltip("Hand angular velocity must exceed this to add linear velocity based on angular velocity.")]
        public float ReleasedAngularThreshold = 1f;

        [Tooltip("Number of frames to average velocity for throwing.")]
        public int ThrowLookback = 5;

        [Tooltip("Number of frames to skip while averaging velocity.")]
        public int ThrowLookbackStart = 0;

        [Tooltip("Uses the center of mass that should match with current controller type you are using.")]
        public HVRThrowingCenterOfMass ThrowingCenterOfMass;




        [Header("Debugging")]

        [Tooltip("If enabled displays vectors involved in throwing calculation.")]
        public bool DebugThrowing;
        public HVRGrabbable PermanentGrabbable;


        public bool GrabToggleActive;

        public override bool IsHandGrabber => true;

        public HVRJointHand JointHand { get; private set; }
        public Transform HandModelRotator { get; private set; }
        public Transform HandModelParent { get; private set; }
        public Vector3 HandModelPosition { get; private set; }
        public Quaternion HandModelRotation { get; private set; }
        public Vector3 HandModelScale { get; private set; }

        public Collider[] HandColliders { get; private set; }
        public Collider[] InvisibleHandColliders { get; private set; }

        public Dictionary<HVRGrabbable, Coroutine> OverlappingGrabbables = new Dictionary<HVRGrabbable, Coroutine>();

        public GameObject TempGrabPoint { get; internal set; }

        public HVRController Controller => HandSide == HVRHandSide.Left ? HVRInputManager.Instance.LeftController : HVRInputManager.Instance.RightController;

        public Vector3 GrabbableJointAnchor
        {
            get
            {
                if (GrabbedTarget.Rigidbody && _configurableJoint)
                {
                    return GrabbedTarget.Rigidbody.transform.TransformPoint(_configurableJoint.anchor);
                }

                if (GrabPoint)
                {
                    return GrabPoint.position;
                }
                return GrabbedTarget.transform.position;
            }
        }

        public HVRTrackedController HVRTrackedController { get; private set; }

        public override Transform GrabPoint
        {
            get => base.GrabPoint;
            set
            {
                base.GrabPoint = value;
                PosableGrabPoint = !value ? null : value.GetComponent<HVRPosableGrabPoint>();
            }
        }

        public override Vector3 JointAnchorWorldPosition => JointAnchor.position;

        public HVRPosableGrabPoint PosableGrabPoint { get; private set; }



        public Quaternion GrabPointPoseRotation
        {
            get
            {
                if (PosableGrabPoint) return PosableGrabPoint.GetPoseWithJointRotation(HandSide);
                return GrabPoint.rotation;
            }
        }

        public Quaternion GrabPointLocalJointRotation
        {
            get
            {
                if (PosableGrabPoint)
                    return PosableGrabPoint.transform.localRotation * PosableGrabPoint.GetJointRotationOffset(HandSide);
                return GrabPoint.localRotation * HandModelRotation;
            }
        }

        private Transform _handOffset;
        private Transform _fakeHand;
        private Transform _fakeHandAnchor;
        public Vector3 GrabPointAnchor
        {
            get
            {
                var positionOffset = HandModelPosition;
                var rotationOffset = HandModelRotation;
                if (PosableGrabPoint)
                {
                    positionOffset = PosableGrabPoint.GetPosePositionOffset(HandSide);
                    rotationOffset = PosableGrabPoint.GetPoseRotationOffset(HandSide);
                }
                else if (IsPhysicsPose)
                {
                    //should be parented by this point already
                    positionOffset = HandModel.localPosition;
                    rotationOffset = HandModel.localRotation;
                }
                else if (GrabbedTarget.GrabType == HVRGrabType.Offset)
                {
                    //should be parented by this point already
                    positionOffset = HandModel.localPosition;
                    rotationOffset = HandModel.localRotation;
                }

                _fakeHand.localPosition = HandModelPosition;
                _fakeHand.localRotation = HandModelRotation;
                _fakeHandAnchor.position = JointAnchorWorldPosition;
                _fakeHand.parent = GrabPoint;
                _fakeHand.localPosition = positionOffset;
                _fakeHand.localRotation = rotationOffset;
                var anchor = GrabbedTarget.Rigidbody.transform.InverseTransformPoint(_fakeHandAnchor.position);
                _fakeHand.parent = transform;
                return anchor;
            }
        }



        public Quaternion SnapJointRotation => transform.rotation * Quaternion.Inverse(GrabPointLocalJointRotation * Quaternion.Inverse(HandModelRotation));

        public override Quaternion ControllerRotation => TrackedController.rotation;

        public Transform Palm => PhysicsPoser.Palm;





        public bool IsClimbing { get; private set; }

        #region Private

        private SphereCollider _physicsGrabberCollider;
        private SphereCollider _overlapCollider;
        private readonly Collider[] _overlapColliders = new Collider[1000];
        private readonly List<Tuple<Collider, Vector3, float>> _physicsGrabPoints = new List<Tuple<Collider, Vector3, float>>();
        private readonly List<Tuple<GrabPointMeta, float>> _grabPoints = new List<Tuple<GrabPointMeta, float>>();
        private bool _movingToGrabbable;
        private bool _hasHandModelParented;
        private Quaternion _previousRotation = Quaternion.identity;
        private float _jointTimer;
        private float _poseDeltaAtJointCreation;
        private bool _fixedJointCreated;
        #endregion

        public readonly CircularBuffer<Vector3> RecentVelocities = new CircularBuffer<Vector3>(200);
        public readonly CircularBuffer<Vector3> RecentAngularVelocities = new CircularBuffer<Vector3>(200);
        public readonly CircularBuffer<Vector3> RecentPositions = new CircularBuffer<Vector3>(200);

        protected virtual void Awake()
        {
            if (TrackedController)
                HVRTrackedController = TrackedController.GetComponent<HVRTrackedController>();
        }

        protected override void Start()
        {
            base.Start();

            JointHand = GetComponent<HVRJointHand>();

            if (!ForceGrabber)
            {
                ForceGrabber = GetComponentInChildren<HVRForceGrabber>();
            }

            if (!HandAnimator)
            {
                if (HandModel)
                {
                    HandAnimator = HandModel.GetComponentInChildren<HVRHandAnimator>();
                }
                else
                {
                    HandAnimator = GetComponentInChildren<HVRHandAnimator>();
                }
            }

            if (!PhysicsPoser)
            {
                if (HandModel)
                {
                    PhysicsPoser = HandModel.GetComponentInChildren<HVRPhysicsPoser>();
                }
                else
                {
                    PhysicsPoser = GetComponentInChildren<HVRPhysicsPoser>();
                }
            }

            if (!HandPhysics)
            {
                HandPhysics = GetComponentInChildren<HVRHandPhysics>();
            }

            if (HandModel)
            {
                HandModelParent = HandModel.parent;
                HandModelPosition = HandModel.localPosition;
                HandModelRotation = HandModel.localRotation;
                HandModelScale = HandModel.localScale;

                HandColliders = HandModel.gameObject.GetComponentsInChildren<Collider>().Where(e => !e.isTrigger).ToArray();

                var go = new GameObject("HandModelRotator");
                HandModelRotator = go.transform;
                HandModelRotator.parent = transform;
                HandModelRotator.localRotation = HandModel.localRotation;
                HandModelRotator.localPosition = HandModel.localPosition;
                HandModelRotator.localScale = HandModel.localScale;

                go = new GameObject("FakeHand");
                go.transform.parent = transform;
                go.transform.localPosition = HandModelPosition;
                go.transform.localRotation = HandModelRotation;
                _fakeHand = go.transform;

                go = new GameObject("FakeHandJointAnchor");
                go.transform.parent = _fakeHand;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                _fakeHandAnchor = go.transform;

                go = new GameObject("HandOffset");
                go.transform.parent = transform;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                _handOffset = go.transform;
            }

            if (InvisibleHand)
            {
                InvisibleHandColliders = InvisibleHand.gameObject.GetComponentsInChildren<Collider>().Where(e => !e.isTrigger).ToArray();
            }

            if (HandColliders != null && InvisibleHandColliders != null)
            {
                foreach (var handCollider in HandColliders)
                {
                    foreach (var invisCollider in InvisibleHandColliders)
                    {
                        Physics.IgnoreCollision(handCollider, invisCollider, true);
                    }
                }
            }

            if (OverlapSizer)
            {
                _overlapCollider = OverlapSizer.GetComponent<SphereCollider>();
            }

            if (PhysicsGrabberSizer)
            {
                _physicsGrabberCollider = PhysicsGrabberSizer.GetComponent<SphereCollider>();
            }

            if (!SocketBag)
                SocketBag = GetComponentInChildren<HVRSocketBag>();

            if (!ThrowingCenterOfMass)
                ThrowingCenterOfMass = GetComponentInChildren<HVRThrowingCenterOfMass>();



            ResetTrackedVelocities();

        }

        public override bool IsGrabActive
        {
            get
            {
                if (PermanentGrabbable) return true;
                switch (GrabTrigger)
                {
                    case HVRButtonTrigger.Active:
                        return Inputs.GetGrabActive(HandSide);
                    case HVRButtonTrigger.Toggle:
                        return GrabToggleActive;
                    default:
                        return false;
                }
            }
        }

        public override bool IsHoldActive
        {
            get
            {
                if (PermanentGrabbable) return true;
                switch (GrabTrigger)
                {
                    case HVRButtonTrigger.Active:
                        return Inputs.GetHoldActive(HandSide);
                    case HVRButtonTrigger.Toggle:
                        return GrabToggleActive;
                    default:
                        return false;
                }
            }
        }



        protected override void Update()
        {
            if (PerformUpdate)
            {
                //todo let this be customizable
                if (Inputs.GetGrabActive(HandSide))
                {
                    var closest = GetClosestGrabbable();
                    if (GrabToggleActive || closest || SocketBag.ValidSockets.Count > 0)
                    {
                        GrabToggleActive = !GrabToggleActive;
                    }
                }

                if (GrabbedTarget)
                {
                    if (Controller.TriggerButtonState.JustActivated)
                    {
                        GrabbedTarget.InternalOnActivate(this);
                    }
                    else if (Controller.TriggerButtonState.JustDeactivated)
                    {
                        GrabbedTarget.InternalOnDeactivate(this);
                    }
                }
            }

            CheckParentHandModel();
        }

        protected void ResetTrackedVelocities()
        {
            for (var i = 0; i < 200; i++)
            {
                RecentVelocities.Enqueue(Vector3.zero);
                RecentAngularVelocities.Enqueue(Vector3.zero);
            }
        }

        private void DetermineGrabPoint(HVRGrabbable grabbable)
        {
            if (IsGrabbing || !grabbable)
                return;

            for (int i = 0; i < grabbable.GrabPointsMeta.Count; i++)
            {
                var grabPoint = grabbable.GrabPointsMeta[i];
                if (!grabPoint.GrabPoint)
                {
                    continue;
                }

                var angleDelta = 0f;
                var posableGrabPoint = grabPoint.PosableGrabPoint;
                Vector3 grabbableWorldAnchor;
                if (posableGrabPoint != null)
                {
                    if (HandSide == HVRHandSide.Left && !posableGrabPoint.LeftHand ||
                        HandSide == HVRHandSide.Right && !posableGrabPoint.RightHand)
                    {
                        continue;
                    }

                    var poseRotation = posableGrabPoint.GetPoseRotation(HandSide);

                    angleDelta = Quaternion.Angle(HandModelRotator.rotation, poseRotation);
                    if (angleDelta > posableGrabPoint.AllowedAngleDifference)
                    {
                        continue;
                    }

                    grabbableWorldAnchor = grabPoint.GrabPoint.position;
                    //grabbableWorldAnchor = CalculateGrabPointWorldAnchor(grabbable, posableGrabPoint);
                }
                else
                {
                    grabbableWorldAnchor = grabPoint.GrabPoint.position;
                }

                var distance = Vector3.Distance(grabbableWorldAnchor, JointAnchorWorldPosition);
                distance += angleDelta;

                _grabPoints.Add(new Tuple<GrabPointMeta, float>(grabPoint, distance));
            }

            if (_grabPoints.Count > 0)
            {
                _grabPoints.Sort((x, y) => x.Item2.CompareTo(y.Item2));
                GrabPoint = _grabPoints[0].Item1.GrabPoint;
            }
            else
            {
                GrabPoint = null;
            }

            _grabPoints.Clear();
        }

        private Vector3 CalculateGrabPointWorldAnchor(HVRGrabbable grabbable, HVRPosableGrabPoint grabPoint)
        {
            if (!grabbable.Rigidbody)
                return grabPoint.transform.position;

            var positionOffset = HandModelPosition;
            var rotationOffset = HandModelRotation;

            if (grabPoint)
            {
                positionOffset = grabPoint.GetPosePositionOffset(HandSide);
                rotationOffset = grabPoint.GetPoseRotationOffset(HandSide);
            }

            _fakeHand.transform.localPosition = HandModelPosition;
            _fakeHand.transform.localRotation = HandModelRotation;
            _fakeHandAnchor.position = JointAnchorWorldPosition;
            _fakeHand.parent = GrabPoint;
            _fakeHand.localPosition = positionOffset;
            _fakeHand.localRotation = rotationOffset;
            var anchor = _fakeHandAnchor.position;
            _fakeHand.parent = transform;
            return anchor;
        }

        protected override void FixedUpdate()
        {
            if (PerformUpdate)
            {
                var closestCanGrab = GetClosestGrabbable(CanGrab);
                //DetermineGrabPoint(closestCanGrab);
                CheckBreakDistance();
                TrackVelocities();
                CheckSocketUnhover();
                CheckSocketHover();
                CheckSocketGrab(); //before base grab check
            }

            UpdateJoints();

            base.FixedUpdate();

            _previousRotation = transform.rotation;
        }



        private void TrackVelocities()
        {
            var deltaRotation = transform.rotation * Quaternion.Inverse(_previousRotation);
            deltaRotation.ToAngleAxis(out var angle, out var axis);
            angle *= Mathf.Deg2Rad;
            var angularVelocity = axis * (angle * (1.0f / Time.fixedDeltaTime));

            RecentVelocities.Enqueue(Rigidbody.velocity);
            RecentAngularVelocities.Enqueue(angularVelocity);
            RecentPositions.Enqueue(transform.position);
        }

        private void CheckSocketUnhover()
        {
            if (!HoveredSocket)
                return;

            if (SocketBag.ClosestSocket == null || !SocketBag.ValidSockets.Contains(HoveredSocket) ||
                (SocketBag.ClosestSocket != HoveredSocket && SocketBag.ClosestSocket.IsGrabbing))
            {
                HoveredSocket.OnHandGrabberExited();
                HoveredSocket = null;
                //Debug.Log($"socket exited");
            }
        }

        private void CheckSocketGrab()
        {
            if (GrabbedTarget)
                return;

            if (ForceGrabber && ForceGrabber.IsForceGrabbing)
                return;
            
            if (HoveredSocket && HoveredSocket.GrabbedTarget && IsGrabActive && HoveredSocket.CanGrabbableBeRemoved)
            {
                DetermineGrabPoint(HoveredSocket.GrabbedTarget);
                if (TryGrab(HoveredSocket.GrabbedTarget, true))
                {
                    HoveredSocket.OnHandGrabberExited();
                    HoveredSocket = null;
                    //Debug.Log($"grabbed from socket directly");
                }
            }
        }

        public bool IsHoveringSocket => HoveredSocket;
        public HVRSocket HoveredSocket;

        private void CheckSocketHover()
        {
            if (IsGrabbing || IsHoveringSocket || !SocketBag)
                return;

            for (var i = 0; i < SocketBag.ValidSockets.Count; i++)
            {
                var socket = SocketBag.ValidSockets[i];
                if (!socket.IsGrabbing || !socket.CanInteract)
                    continue;

                HoveredSocket = socket;
                socket.OnHandGrabberEntered();
                break;
            }
        }

        private void UpdateJoints()
        {
            //applying forces to a joint with a target rotation doesn't rotate the body in the desired direction
            //recreating the joint once its parented to get the rotation as small as possible
            if (GrabbedTarget && GrabPoint)
            {
                if (MonitoringJointRotation && (_hasHandModelParented || !GrabbedTarget.ParentHandModel))
                {
                    _jointTimer += Time.fixedDeltaTime;

                    //var delta = Mathf.Abs(_poseDeltaAtJointCreation - Quaternion.Angle(GrabPointPoseRotation, HandModelRotator.rotation));
                    var distance = Vector3.Distance(JointAnchorWorldPosition, GrabbableJointAnchor);

                    var rotation = IsPhysicsPose ? PhysicsPoserRotation : SnapJointRotation;

                    //if ((GrabbedTarget.ResetJoint || GrabbedTarget.TrackingType == HVRGrabTracking.FixedJoint) && delta > threshold)
                    //if (delta > threshold)
                    {
                        //Debug.Log($"recreating joint { delta}");

                        //SetupConfigurableJoint(GrabbedTarget, rotation);
                    }

                    var angleDelta = Quaternion.Angle(GrabPointPoseRotation, HandModelRotator.rotation);

                    //if (GrabbedTarget.ResetJoint && delta < threshold && distance < ParentingMaxDistance || distance < ParentingMaxDistance)
                    if (angleDelta < GrabbedTarget.FinalJointMaxAngle && distance < ParentingMaxDistance ||
                        (_jointTimer > GrabbedTarget.FinalJointTimeout && GrabbedTarget.FinalJointQuick))
                    {
                        Debug.Log($"final joint created");
                        MonitoringJointRotation = false;

                        if (GrabbedTarget.TrackingType == HVRGrabTracking.FixedJoint && !_fixedJointCreated)
                        {
                            //Debug.Log($"fixed up");
                            GrabbedTarget.SetupFixedJoint(this);
                            _fixedJointCreated = true;
                        }
                        else if (GrabbedTarget.TrackingType == HVRGrabTracking.ConfigurableJoint)
                        {
                            SetupConfigurableJoint(GrabbedTarget, rotation, true);
                        }
                    }
                }
            }
        }


        private void CheckBreakDistance()
        {
            if (GrabbedTarget)
            {
                if (Vector3.Distance(GrabbableJointAnchor, JointAnchorWorldPosition) > GrabbedTarget.BreakDistance)
                {
                    ForceRelease();
                }
            }
        }

        private void CheckParentHandModel()
        {
            if (!IsGrabbing || _hasHandModelParented || _movingToGrabbable || !GrabbedTarget)
                return;

            var angleDelta = 0f;
            if (GrabbedTarget.GrabType == HVRGrabType.Snap && !IgnoreParentingAngle)
            {
                angleDelta = Quaternion.Angle(GrabPointPoseRotation, HandModelRotator.rotation);
            }

            var distance = 0f;
            if (!IgnoreParentingDistance && _configurableJoint)
            {
                distance = Vector3.Distance(JointAnchorWorldPosition, GrabbableJointAnchor);
            }

            if ((IgnoreParentingAngle || angleDelta <= ParentingMaxAngleDelta) &&
                (IgnoreParentingDistance || distance <= ParentingMaxDistance) ||
                GrabbedTarget.ParentHandModelImmediately ||
                GrabbedTarget.GrabberCount > 1)
            {
                if (GrabbedTarget.ParentHandModel)
                {
                    ParentHandModel(GrabPoint, PosableGrabPoint ? PosableGrabPoint.HandPoser : FallbackPoser);
                }
                else
                {
                    _hasHandModelParented = true;
                    HandModel.transform.parent = _handOffset;
                    _handOffset.localPosition = Vector3.zero;
                    if (PosableGrabPoint)
                    {
                        _handOffset.localPosition = -PosableGrabPoint.GetPosePositionOffset(HandSide);
                    }
                    HandAnimator?.SetCurrentPoser(PosableGrabPoint ? PosableGrabPoint.HandPoser : FallbackPoser);
                }
            }
        }

        private void ParentHandModel(Transform parent, HVRHandPoser poser)
        {
            if (!parent)
                return;

            if (GrabbedTarget && !GrabbedTarget.ParentHandModel)
                return;

            var worldRotation = parent.rotation;
            var worldPosition = parent.position;

            var posableGrabPoint = parent.GetComponent<HVRPosableGrabPoint>();
            if (posableGrabPoint && posableGrabPoint.VisualGrabPoint)
            {
                parent = posableGrabPoint.VisualGrabPoint;
                parent.rotation = worldRotation;
                parent.position = worldPosition;
            }

            HandModel.transform.parent = parent;
            _hasHandModelParented = true;

            HandAnimator?.SetCurrentPoser(poser);

            var listener = parent.gameObject.AddComponent<HVRDestroyListener>();
            listener.Destroyed.AddListener(OnGrabPointDestroyed);
        }
        private void OnGrabPointDestroyed(HVRDestroyListener listener)
        {
            if (HandModel.parent == listener.transform)
            {
                Debug.Log($"grab point destroyed while parented.");
                ResetHandModel();
            }
        }

        public void OverrideHandSettings(HVRJointSettings settings)
        {
            JointHand.JointOverride = settings;
            if (settings)
            {
                //Debug.Log($"hand - {settings.name}");
            }
            else
            {
                //Debug.Log($"hand - reset");
            }
        }

        public override bool CanHover(HVRGrabbable grabbable)
        {
            if (grabbable.IsBeingHeld) return false;
            if (IsGrabbing) return false;
            if (GetClosestGrabbable() != grabbable) return false;
            return base.CanHover(grabbable);
        }

        public override bool CanGrab(HVRGrabbable grabbable)
        {
            if (!base.CanGrab(grabbable))
                return false;

            //todo reconsider how to prevent taking items from someone elses hands in multiplayer
            //this is prone to error if someone disconnects or the grab fails on the other side

            if ((!AllowMultiplayerSwap && !grabbable.AllowMultiplayerSwap) && grabbable.HoldType != HVRHoldType.ManyHands && grabbable.AnyGrabberNotMine())
            {
                return false;
            }

            if (grabbable.PrimaryGrabber && !grabbable.PrimaryGrabber.AllowSwap)
            {
                if (grabbable.HoldType == HVRHoldType.TwoHanded && grabbable.GrabberCount > 1)
                    return false;

                if (grabbable.HoldType == HVRHoldType.OneHand && grabbable.GrabberCount > 0)
                    return false;
            }

            if (PermanentGrabbable && grabbable != PermanentGrabbable) return false;
            //if (OverlappingGrabbables.ContainsKey(grabbable) && GrabPoint == null) return false;
            if (GrabbedTarget != null && GrabbedTarget != grabbable)
                return false;
            if (grabbable.PrimaryGrabber && grabbable.PrimaryGrabber is HVRSocket)
                return false;

            if (grabbable.RequireLineOfSight && !CheckLineOfSight(grabbable))
                return false;

            if (grabbable.RequiresGrabbable)
            {
                if (!grabbable.RequiredGrabbable.PrimaryGrabber || !grabbable.RequiredGrabbable.PrimaryGrabber.IsHandGrabber)
                    return false;
            }

            return true;
        }

        private bool CheckLineOfSight(HVRGrabbable grabbable)
        {
            var ray = new Ray();
            var anyHit = false;
            for (var i = 0; i < grabbable.Colliders.Length; i++)
            {
                var grabbableCollider = grabbable.Colliders[i];

                ray.origin = RaycastOrigin.position;
                ray.direction = grabbableCollider.bounds.center - ray.origin;

                if (Physics.Raycast(ray, out var hit, .75f, RaycastLayermask, QueryTriggerInteraction.Ignore))
                {
                    if (Equals(grabbableCollider, hit.collider))
                    {
                        anyHit = true;
                        break;
                    }
                }
            }

            for (var i = 0; i < grabbable.Triggers.Length; i++)
            {
                var grabbableCollider = grabbable.Triggers[i];

                ray.origin = RaycastOrigin.position;
                ray.direction = grabbableCollider.bounds.center - ray.origin;

                if (Physics.Raycast(ray, out var hit, .75f, RaycastLayermask, QueryTriggerInteraction.Collide))
                {
                    if (Equals(grabbableCollider, hit.collider))
                    {
                        anyHit = true;
                        break;
                    }
                }
            }

            if (!anyHit)
                return false;
            return true;
        }

        protected override void OnHoverEnter(HVRGrabbable grabbable)
        {
            base.OnHoverEnter(grabbable);

            if (IsMine && CanGrab(grabbable) && !Mathf.Approximately(0f, HapticsDuration))
                Controller.Vibrate(HapticsAmplitude, HapticsDuration);
        }

        protected override void OnBeforeGrabbed(HVRGrabArgs args)
        {
            if (args.Grabbable.GrabType == HVRGrabType.Snap)
            {
                DetermineGrabPoint(args.Grabbable);
            }
            base.OnBeforeGrabbed(args);
        }

        protected override void OnGrabbed(HVRGrabArgs args)
        {
            base.OnGrabbed(args);
            var grabbable = args.Grabbable;
            if (OverlappingGrabbables.TryGetValue(grabbable, out var routine))
            {
                if (routine != null) StopCoroutine(routine);
                OverlappingGrabbables.Remove(grabbable);
            }

            if (args.Grabbable.GrabType == HVRGrabType.Offset || !TryPhysicsGrab())
            {
                if (!GrabPoint || args.Grabbable.GrabType == HVRGrabType.Offset)
                {
                    TempGrabPoint = new GameObject(name + " OffsetGrabPoint");
                    TempGrabPoint.transform.position = JointAnchorWorldPosition;
                    TempGrabPoint.transform.parent = GrabbedTarget.transform;
                    TempGrabPoint.transform.localRotation = Quaternion.identity;
                    GrabPoint = TempGrabPoint.transform;
                    var grabPointRotation = Quaternion.Inverse(GrabPoint.rotation) * HandModel.rotation;

                    HandAnimator.SetCurrentPoser(null);
                    HandAnimator.Hand.Pose(FallbackPoser.PrimaryPose.Pose.GetPose(HandSide));
                    if (grabbable.ParentHandModel)
                    {
                        ParentHandModel(GrabPoint, null);
                    }

                    var rotation = transform.rotation * Quaternion.Inverse(grabPointRotation * Quaternion.Inverse(HandModelRotation));
                    Grab(grabbable, rotation);
                }
                else
                {
                    Grab(grabbable, SnapJointRotation);
                }
            }
        }

        public virtual void NetworkGrab(HVRGrabbable grabbable)
        {
            CommonGrab(grabbable, SnapJointRotation);
        }

        public virtual void NetworkPhysicsGrab(HVRGrabbable grabbable)
        {
            IsPhysicsPose = true;
            CommonGrab(grabbable, PhysicsPoserRotation);
            ParentHandModel(GrabPoint.transform, null);
        }

        public virtual void Grab(HVRGrabbable grabbable, Quaternion rotation)
        {
            CommonGrab(grabbable, rotation);
            Grabbed.Invoke(this, grabbable);
        }

        protected virtual void PhysicsGrab(HVRGrabbable grabbable, Quaternion rotation)
        {
            IsPhysicsPose = true;
            CommonGrab(grabbable, rotation);
            Grabbed.Invoke(this, grabbable);
        }

        private void CommonGrab(HVRGrabbable grabbable, Quaternion rotation)
        {
            SetupGrab(grabbable, rotation);

            IsClimbing = grabbable.GetComponent<HVRClimbable>();

            if (grabbable.HandGrabbedClip)
                SFXPlayer.Instance.PlaySFX(grabbable.HandGrabbedClip, transform.position);
        }

        public void SetupGrab(HVRGrabbable grabbable, Quaternion jointRotation)
        {

            if (grabbable.GrabType == HVRGrabType.Offset)
            {
                if (grabbable.TrackingType == HVRGrabTracking.ConfigurableJoint)
                {
                    SetupConfigurableJoint(grabbable, jointRotation);
                }
                else if (grabbable.TrackingType == HVRGrabTracking.FixedJoint)
                {
                    grabbable.SetupFixedJoint(this);
                }
            }
            else
            {
                //use configurable joint to rotate into place then apply fixed joint later
                if (grabbable.TrackingType == HVRGrabTracking.ConfigurableJoint || grabbable.TrackingType == HVRGrabTracking.FixedJoint)
                {
                    SetupConfigurableJoint(grabbable, jointRotation);
                    MonitoringJointRotation = true;
                    _jointTimer = 0f;
                    _fixedJointCreated = false;
                }
            }

            if (grabbable.TrackingType == HVRGrabTracking.ConfigurableJoint || grabbable.TrackingType == HVRGrabTracking.FixedJoint)
            {
                grabbable.Rigidbody.isKinematic = false;
                grabbable.Rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            if (GrabPoint)
            {
                grabbable.HeldGrabPoints.Add(GrabPoint);
            }

            DisableHandCollision(grabbable);

            if (grabbable.EnableInvisibleHand && InvisibleHand)
            {
                InvisibleHand.gameObject.SetActive(true);
                DisableInvisibleHandCollision(grabbable);
            }
        }

        private void SetupConfigurableJoint(HVRGrabbable grabbable, Quaternion jointRotation, bool final = false)
        {
            _poseDeltaAtJointCreation = Quaternion.Angle(GrabPointPoseRotation, HandModelRotator.rotation);
            _configurableJoint = grabbable.SetupConfigurableJoint(this, GrabPointAnchor, jointRotation);

            if (final)
            {
                if (HVRSettings.Instance.IgnoreLegacyGrabbableSettings)
                {
                    if (grabbable.JointOverride)
                    {
                        grabbable.JointOverride.ApplySettings(_configurableJoint);
                    }
                    else if (HVRSettings.Instance.DefaultJointSettings)
                    {
                        HVRSettings.Instance.DefaultJointSettings.ApplySettings(_configurableJoint);
                    }
                }
            }
            else if (PullingSettings)
            {
                PullingSettings.ApplySettings(_configurableJoint);
            }
        }

        protected override void OnReleased(HVRGrabbable grabbable)
        {
            base.OnReleased(grabbable);
            MonitoringJointRotation = false;
            _fixedJointCreated = false;
            IsPhysicsPose = false;
            ResetHandModel();
            grabbable.transform.SetLayerRecursive(HVRLayers.Grabbable, HandModel);
            HandModel.transform.SetLayerRecursive(HVRLayers.Hand);
            if (InvisibleHand)
                InvisibleHand.gameObject.SetActive(false);

            var routine = StartCoroutine(CheckReleasedOverlap(grabbable));
            OverlappingGrabbables[grabbable] = routine;

            GrabToggleActive = false;

            grabbable.HeldGrabPoints.Remove(GrabPoint);

            if (TempGrabPoint)
            {
                Destroy(TempGrabPoint.gameObject);
            }

            IsClimbing = false;

            if (grabbable.Rigidbody)
            {
                var throwVelocity = ComputeThrowVelocity(grabbable, out var angularVelocity, true);
                grabbable.Rigidbody.velocity = throwVelocity;
                grabbable.Rigidbody.angularVelocity = angularVelocity;
            }

            Released.Invoke(this, grabbable);
        }

        public Vector3 GetAverageVelocity(int seconds, int start, Vector3[] velocities = null)
        {
            return GetAverageVelocity(seconds, start, RecentVelocities, velocities);
        }

        public Vector3 GetAverageAngularVelocity(int seconds, int start, Vector3[] velocities = null)
        {
            return GetAverageVelocity(seconds, start, RecentAngularVelocities, velocities);
        }

        internal static Vector3 GetAverageVelocity(int frames, int start, CircularBuffer<Vector3> recentVelocities, Vector3[] velocities = null)
        {
            if (velocities != null)
            {
                for (int i = 0; i < velocities.Length; i++)
                {
                    velocities[i] = Vector3.zero;
                }
            }

            var sum = Vector3.zero;
            for (var i = start; i < start + frames; i++)
            {
                sum += recentVelocities[i];
                if (velocities != null && velocities.Length > i)
                {
                    velocities[i] = recentVelocities[i];
                }
            }

            if (Mathf.Approximately(frames, 0f))
                return Vector3.zero;

            var average = sum / frames;

            sum = Vector3.zero;

            for (var i = start; i < start + frames; i++)
            {
                //removing any vectors not going in the direction of the average vector
                var dot = Vector3.Dot(average.normalized, recentVelocities[i].normalized);
                if (dot < .2)
                {
                    //Debug.Log($"Filtered {average},{recentVelocities[i]},{dot}");
                    continue;
                }
                sum += recentVelocities[i];
            }

            return sum / frames;
        }



        public Vector3 ComputeThrowVelocity(HVRGrabbable grabbable, out Vector3 angularVelocity, bool isThrowing = false)
        {
            if (!grabbable.Rigidbody)
            {
                angularVelocity = Vector3.zero;
                return Vector3.zero;
            }

            var velocities = new Vector3[200];
            var angVelocities = new Vector3[200];

            var velocities2 = new Vector3[200];
            var angVelocities2 = new Vector3[200];

            var grabbableVelocity = grabbable.GetAverageVelocity(ThrowLookback, ThrowLookbackStart, velocities2);
            var grabbableAngular = grabbable.GetAverageAngularVelocity(ThrowLookback, ThrowLookbackStart, angVelocities2);

            var handVelocity = GetAverageVelocity(ThrowLookback, ThrowLookbackStart, velocities);
            var handAngularVelocity = GetAverageAngularVelocity(ThrowLookback, ThrowLookbackStart, angVelocities);

            var linearVelocity = ReleasedVelocityFactor * handVelocity + grabbableVelocity * grabbable.ReleasedVelocityFactor;
            var throwVelocity = linearVelocity;

            Vector3 centerOfMass;
            if (ThrowingCenterOfMass && ThrowingCenterOfMass.CenterOfMass)
            {
                centerOfMass = ThrowingCenterOfMass.CenterOfMass.position;
            }
            else
            {
                centerOfMass = Rigidbody.worldCenterOfMass;
            }

            //compute linear velocity from wrist rotation
            var grabbableCom = GrabPoint != null ? GrabPoint.position : grabbable.Rigidbody.worldCenterOfMass;

            //Debug.Log($"{handAngularVelocity.magnitude}");

            if (handAngularVelocity.magnitude > ReleasedAngularThreshold)
            {
                var cross = Vector3.Cross(handAngularVelocity, grabbableCom - centerOfMass) * grabbable.ReleasedAngularConversionFactor * ReleasedAngularConversionFactor;
                throwVelocity += cross;
            }

            angularVelocity = grabbableAngular * grabbable.ReleasedAngularFactor;

            if (isThrowing && DebugThrowing)
            {
                for (var i = ThrowLookbackStart; i < ThrowLookbackStart + ThrowLookback; i++)
                {
                    Debug.Log($"{velocities[i].magnitude},{angVelocities[i].magnitude}|{velocities2[i].magnitude},{angVelocities2[i].magnitude}");
                    var obj = new GameObject("average line " + i);
                    var line = obj.AddComponent<LineRenderer>();
                    line.widthCurve = new AnimationCurve(new Keyframe(0, .005f), new Keyframe(0, .005f));
                    line.SetPosition(0, RecentPositions[i]);
                    line.SetPosition(1, RecentPositions[i] + velocities[i]);
                }

                var obj2 = new GameObject("");
                var line2 = obj2.AddComponent<LineRenderer>();
                line2.widthCurve = new AnimationCurve(new Keyframe(0, .01f), new Keyframe(0, .03f));
                line2.SetPosition(0, RecentPositions[0]);
                line2.SetPosition(1, RecentPositions[0] + throwVelocity);

            }

            return throwVelocity;
        }


        private IEnumerator CheckReleasedOverlap(HVRGrabbable grabbable)
        {
            if (!OverlapSizer || !_overlapCollider)
            {
                yield break;
            }

            yield return new WaitForFixedUpdate();

            var elapsed = 0f;

            while (OverlappingGrabbables.ContainsKey(grabbable))
            {
                var count = Physics.OverlapSphereNonAlloc(OverlapSizer.transform.position, _overlapCollider.radius, _overlapColliders);
                if (count == 0) break;

                var match = false;
                for (int i = 0; i < count; i++)
                {
                    if (_overlapColliders[i].attachedRigidbody == grabbable.Rigidbody)
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                    break;

                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                if (!grabbable.RequireOverlapClearance && elapsed > grabbable.OverlapTimeout)
                {
                    break;
                }
            }

            EnableHandCollision(grabbable);
            EnableInvisibleHandCollision(grabbable);

            OverlappingGrabbables.Remove(grabbable);
        }

        private void EnableInvisibleHandCollision(HVRGrabbable grabbable)
        {
            if (InvisibleHandColliders == null || grabbable.Colliders == null)
            {
                return;
            }

            foreach (var handCollider in InvisibleHandColliders)
            {
                foreach (var grabbableCollider in grabbable.Colliders)
                {
                    if (grabbableCollider)
                        Physics.IgnoreCollision(handCollider, grabbableCollider, false);
                }

                foreach (var grabbableCollider in grabbable.AdditionalIgnoreColliders)
                {
                    Physics.IgnoreCollision(handCollider, grabbableCollider, false);
                }
            }
        }

        private void DisableInvisibleHandCollision(HVRGrabbable grabbable, Collider except = null)
        {
            if (InvisibleHandColliders == null || grabbable.Colliders == null)
            {
                return;
            }

            foreach (var handCollider in InvisibleHandColliders)
            {
                foreach (var grabbableCollider in grabbable.Colliders)
                {
                    if (grabbableCollider && except != grabbableCollider)
                        Physics.IgnoreCollision(handCollider, grabbableCollider);
                }

                foreach (var grabbableCollider in grabbable.AdditionalIgnoreColliders)
                {
                    Physics.IgnoreCollision(handCollider, grabbableCollider);
                }
            }
        }

        private void EnableHandCollision(HVRGrabbable grabbable)
        {
            if (HandColliders == null || grabbable.Colliders == null)
            {
                return;
            }

            foreach (var handCollider in HandColliders)
            {
                foreach (var grabbableCollider in grabbable.Colliders)
                {
                    if (grabbableCollider)
                        Physics.IgnoreCollision(handCollider, grabbableCollider, false);
                }

                foreach (var grabbableCollider in grabbable.AdditionalIgnoreColliders)
                {
                    Physics.IgnoreCollision(handCollider, grabbableCollider, false);
                }
            }
        }

        private void DisableHandCollision(HVRGrabbable grabbable, Collider except = null)
        {
            if (HandColliders == null || grabbable.Colliders == null)
            {
                return;
            }

            foreach (var handCollider in HandColliders)
            {
                foreach (var grabbableCollider in grabbable.Colliders)
                {
                    if (grabbableCollider && except != grabbableCollider)
                        Physics.IgnoreCollision(handCollider, grabbableCollider);
                }

                foreach (var grabbableCollider in grabbable.AdditionalIgnoreColliders)
                {
                    Physics.IgnoreCollision(handCollider, grabbableCollider);
                }
            }


        }

        private readonly Collider[] _physicsGrabColliders = new Collider[100];

        private bool TryPhysicsGrab()
        {
            if (!HandModel || !GrabbedTarget) return false;

            var tryPhysics = GrabbedTarget.GrabType == HVRGrabType.PhysicPoser ||
                             (GrabPoint == null && GrabbedTarget.PhysicsPoserFallback);

            if (!tryPhysics) return false;

            if (GrabbedTarget.Colliders.Length == 0)
            {
                return false;
            }

            var layer = HandSide == HVRHandSide.Left ? HVRLayers.LeftTarget : HVRLayers.RightTarget;
            var layerMask = LayerMask.GetMask(layer.ToString());
            try
            {
                GrabbedTarget.transform.SetLayerRecursive(layer);



                int count;

                if (!_isForceAutoGrab && _physicsGrabberCollider)
                {
                    count = Physics.OverlapSphereNonAlloc(
                        PhysicsGrabberSizer.transform.position,
                        _physicsGrabberCollider.radius,
                        _physicsGrabColliders,
                        layerMask,
                        QueryTriggerInteraction.Ignore);
                }
                else
                {
                    count = Physics.OverlapSphereNonAlloc(
                        Palm.transform.position,
                        .3f,
                        _physicsGrabColliders,
                        layerMask,
                        QueryTriggerInteraction.Ignore);
                }

                for (int i = 0; i < count; i++)
                {
                    var overlapped = _physicsGrabColliders[i];

                    if (GrabbedTarget.Colliders.Contains(overlapped))
                    {
                        var point = overlapped.ClosestPoint(Palm.transform.position);
                        _physicsGrabPoints.Add(new Tuple<Collider, Vector3, float>(overlapped, point, Vector3.Distance(point, Palm.transform.position)));
                    }
                }

                if (_physicsGrabPoints.Count == 0)
                    return false;

                _physicsGrabPoints.Sort((x, y) => x.Item3.CompareTo(y.Item3));

                StartCoroutine(SolvePhysicsGrab(_physicsGrabPoints[0], layerMask, layer, _isForceAutoGrab));

            }
            finally
            {
                _physicsGrabPoints.Clear();
            }

            return true;
        }

        private IEnumerator SolvePhysicsGrab(Tuple<Collider, Vector3, float> tuple, int layerMask, HVRLayers layer, bool isForceAutoGrab)
        {
            var solved = false;

            var grabbedTarget = GrabbedTarget;

            try
            {
                GrabbedTarget.transform.SetLayerRecursive(layer);

                EnableHandCollision(GrabbedTarget);
                DisableHandCollision(GrabbedTarget, tuple.Item1);

                var hand = new GameObject("Hand");
                hand.transform.position = transform.position;
                hand.transform.rotation = transform.rotation;

                var rb = hand.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                if (!GrabbedTarget.Rigidbody.isKinematic)
                    GrabbedTarget.Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var tracker = hand.AddComponent<HVRCollisionMonitor>();
                rb.useGravity = false;
                HandModel.parent = hand.transform;
                hand.transform.SetLayerRecursive(HVRLayers.Hand);

                var target = new GameObject("Target");
                target.transform.position = tuple.Item2;
                target.transform.parent = GrabbedTarget.transform;


                var delta = target.transform.position - PhysicsPoser.Palm.position;
                var palmDelta = Quaternion.FromToRotation(PhysicsPoser.Palm.forward, delta.normalized);
                hand.transform.rotation = palmDelta * hand.transform.rotation;

                var time = delta.magnitude / PhysicsPoserVelocity + .3f;
                rb.velocity = PhysicsPoserVelocity * delta.normalized;
                if (HandAnimator != null)
                    HandAnimator.SetCurrentPoser(null);

                _movingToGrabbable = true;
                PhysicsPoser.OpenFingers();
                var elapsed = 0f;
                var snapping = false;

                while (GrabbedTarget && elapsed < time)
                {
                    delta = target.transform.position - PhysicsPoser.Palm.position;
                    rb.velocity = delta.normalized * rb.velocity.magnitude;
                    palmDelta = Quaternion.FromToRotation(PhysicsPoser.Palm.forward, delta.normalized);
                    palmDelta.ToAngleAxis(out var angle, out var axis);
                    if (angle > 180.0f) angle -= 360.0f;

                    if (Mathf.Abs(angle) > 5)
                    {
                        rb.angularVelocity = (1f / Time.fixedDeltaTime * angle * axis * 0.01745329251994f * Mathf.Pow(1, 90f * Time.fixedDeltaTime));
                    }
                    else
                    {
                        rb.angularVelocity = Vector3.zero;
                    }


                    if (_isForceAutoGrab || snapping || grabbedTarget.Rigidbody.velocity.magnitude > InstantVelocityThreshold || grabbedTarget.Rigidbody.angularVelocity.magnitude > InstantAngularThreshold)
                    {
                        rb.velocity *= 1.2f;

                        grabbedTarget.Rigidbody.velocity *= .7f;
                        grabbedTarget.Rigidbody.angularVelocity *= .7f;


                        snapping = true;
                    }



                    if (tracker.Collided)
                    {
                        if (tracker.Collider != tuple.Item1)
                        {
                            tracker.Collided = false;
                            tracker.Collider = null;
                        }
                        else
                        {
                            //Debug.Log($"collided");
                            _movingToGrabbable = false;

                            TempGrabPoint = new GameObject(name + " PhysicsGrabPoint");
                            TempGrabPoint.transform.position = Palm.position;
                            TempGrabPoint.transform.parent = GrabbedTarget.transform;
                            TempGrabPoint.transform.localRotation = Quaternion.identity;
                            GrabPoint = TempGrabPoint.transform;

                            GrabbedTarget.Rigidbody.velocity = Vector3.zero;
                            GrabbedTarget.Rigidbody.angularVelocity = Vector3.zero;
                            PhysicsHandRotation = Quaternion.Inverse(GrabPoint.rotation) * HandModel.rotation;

                            PhysicsPoser.SimulateClose(layerMask);
                            ParentHandModel(GrabPoint.transform, null);
                            PhysicsGrab(grabbedTarget, PhysicsPoserRotation);


                            solved = true;
                            break;
                        }
                    }

                    yield return new WaitForFixedUpdate();
                    elapsed += Time.fixedDeltaTime;
                }

                if (!solved)
                {
                    Debug.Log($"unsolved reset hand model");
                    ResetHandModel();
                }

                Destroy(target);
                Destroy(hand);
            }
            finally
            {
                if (GrabbedTarget)
                    GrabbedTarget.transform.SetLayerRecursive(HVRLayers.Grabbable, HandModel);

                _movingToGrabbable = false;
            }

            if (!solved && GrabbedTarget)
            {
                Debug.Log($"unsolved force release");
                ForceRelease();
            }

        }

        public bool IsPhysicsPose { get; set; }
        private bool _isForceAutoGrab;
        internal Quaternion PhysicsPoserRotation => transform.rotation * Quaternion.Inverse(PhysicsHandRotation * Quaternion.Inverse(HandModelRotation));
        internal Quaternion PhysicsHandRotation { get; set; }
        internal ConfigurableJoint _configurableJoint;

        public bool TryAutoGrab(HVRGrabbable grabbable)
        {
            if (GrabTrigger == HVRButtonTrigger.Active && !Inputs.GetHoldActive(HandSide))
            {
                return false;
            }

            GrabPoint = grabbable.GetForceGrabPoint(HandSide);
            if (!PosableGrabPoint && grabbable.PhysicsPoserFallback)
                GrabPoint = null;

            _isForceAutoGrab = true;

            try
            {
                if (TryGrab(grabbable))
                {
                    if (GrabTrigger == HVRButtonTrigger.Toggle)
                        GrabToggleActive = true;
                    return true;
                }
            }
            finally
            {
                _isForceAutoGrab = false;
            }
            return false;
        }


        private void ResetHandModel()
        {
            if (!HandModel)
                return;

            if (HandModel.parent)
            {
                var listener = HandModel.parent.GetComponent<HVRDestroyListener>();
                if (listener)
                    listener.Destroyed.RemoveListener(OnGrabPointDestroyed);
            }

            _hasHandModelParented = false;
            HandModel.gameObject.SetActive(true);
            HandModel.parent = HandModelParent;
            HandModel.localPosition = HandModelPosition;
            HandModel.localRotation = HandModelRotation;
            HandModel.localScale = HandModelScale;

            HandAnimator.ResetToDefault();
        }

        private void OnJointBreak(float breakForce)
        {
            Debug.Log($"joint broken {breakForce}");
            ForceRelease();
        }

        //#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Rigidbody)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(Rigidbody.worldCenterOfMass, .02f);
            }
        }
        //#endif
    }
}
