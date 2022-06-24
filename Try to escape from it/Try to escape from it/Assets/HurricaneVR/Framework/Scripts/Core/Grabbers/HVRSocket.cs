using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HurricaneVR.Framework.Core.Bags;
using HurricaneVR.Framework.Core.Sockets;
using HurricaneVR.Framework.Core.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HurricaneVR.Framework.Core.Grabbers
{
    public class HVRSocket : HVRGrabberBase
    {

        [Tooltip("If supplied, this object will be cloned when one is removed.")]
        public GameObject AutoSpawnPrefab;

        [Tooltip("Prioritized SFX to play for anything socketed")]
        public AudioClip AudioGrabbedOverride;

        [Tooltip("Prioritized SFX to play for anything released")]
        public AudioClip AudioReleasedOverride;

        [Tooltip("Fallback grabbed sfx to play if the socketable doesn't have one.")]
        public AudioClip AudioGrabbedFallback;
        [Tooltip("Fallback released sfx to play if the socketable doesn't have one.")]
        public AudioClip AudioReleasedFallback;

        [Tooltip("If true item's must be placed with a hand grabber.")]
        public bool GrabbableMustBeHeld = true;

        [Tooltip("If true will snatch from a hand on hover.")]
        public bool GrabsFromHand;

        [Tooltip("Actions to apply when the socket is being hovered by a grabbable. Auto populates if empty")]
        public HVRSocketHoverAction[] HoverActions;
        [Tooltip("Actions to apply when the socket is being hovered by a hand.")]
        public HVRSocketHoverAction[] HandGrabActions;

        [Tooltip("If parent grabbable is socketed, disable grabbing.")]
        public bool ParentDisablesGrab;
        [Tooltip("Parent grabbable used with ParentDisablesGrab.")]
        public HVRGrabbable ParentGrabbable;

        [Tooltip("If false then you can't remove the grabbable via hand grab.")]
        public bool CanRemoveGrabbable = true;

        [Tooltip("Scales the grabbable down to fit based on Size and the model bounds.")]
        public bool ScaleGrabbable = true;

        [Tooltip("Grabbable scales down to this size along its longest extent.")]
        public float Size;

        [Tooltip("Filters to filter out socketables.")]
        public HVRSocketFilter[] SocketFilters;

        [Tooltip("If multiple filters are in use, must all be valid or just one?")]
        public SocketCondition FilterCondition = SocketCondition.AND;

        [Tooltip("Fires when an AutoSpawnedPrefab is instantiated.")]
        public SocketSpawnEvent SpawnedPrefab = new SocketSpawnEvent();

        private Transform _previousParent;
        private Vector3 _previousScale;
        private Bounds _modelBounds;
        private bool _appQuitting;


        public HVRGrabbable LinkedGrabbable { get; internal set; }

        public override bool IsGrabActive => !IsGrabbing;
        public override bool IsHoldActive => IsGrabbing;

        public override bool AllowSwap => true;

        public virtual bool CanInteract { get; set; } = true;

        public override bool IsSocket => true;


        public bool CanGrabbableBeRemoved
        {
            get
            {
                if (!CanRemoveGrabbable)
                    return false;
                if (!CanInteract)
                    return false;
                if (ParentDisablesGrab && ParentGrabbable && ParentGrabbable.IsSocketed)
                    return false;
                return true;
            }
        }

        public bool CanAddGrabbable
        {
            get
            {
                if (!CanInteract)
                    return false;
                if (ParentDisablesGrab && ParentGrabbable && ParentGrabbable.IsSocketed)
                    return false;
                return true;
            }
        }

        protected override void Start()
        {
            base.Start();

            if (GrabBags.Count == 0)
            {
                var bag = gameObject.AddComponent<HVRTriggerGrabbableBag>();
                GrabBags.Add(bag);
                bag.Grabber = this;
            }

            SocketFilters = GetComponents<HVRSocketFilter>();
            if (HoverActions == null || HoverActions.Length == 0)
                HoverActions = GetComponents<HVRSocketHoverAction>();

            StartCoroutine(WaitForUpdate(CheckAutoSpawn));
        }

        private IEnumerator WaitForUpdate(Action action)
        {
            yield return null;
            action();
        }

        private void CheckAutoSpawn()
        {
            if (AutoSpawnPrefab)
            {
                var clone = Instantiate(AutoSpawnPrefab);
                var cloneGrabbable = clone.GetComponent<HVRGrabbable>();
                if (cloneGrabbable)
                {
                    TryGrab(cloneGrabbable, true);
                    SpawnedPrefab.Invoke(this, clone);
                }
                else
                    Debug.Log($"Socket {name} has a AutoSpawnPrefab without an HVRGrabbable component");
            }
        }

        private void OnApplicationQuit()
        {
            _appQuitting = true;
        }

        protected override void Update()
        {
            base.Update();
            //UpdateScale(GrabbedTarget);
        }

        protected override bool CheckHover()
        {
            if (base.CheckHover()) return true;

            //take over another invalid socket if we are valid
            for (var g = 0; g < GrabBags.Count; g++)
            {
                var grabBag = GrabBags[g];
                for (var i = 0; i < grabBag.ValidGrabbables.Count; i++)
                {
                    var grabbable = grabBag.ValidGrabbables[i];

                    if (!grabbable.SocketHoverer)
                        continue;

                    if (!grabbable.SocketHoverer.IsValid(grabbable) && IsValid(grabbable))
                    {
                        UnhoverGrabbable(grabbable.SocketHoverer, grabbable);
                        HoverGrabbable(this, grabbable);
                        return true;
                    }
                }
            }

            return false;
        }

        public override bool CanHover(HVRGrabbable grabbable)
        {
            if (!CanInteract)
                return false;
            if (IsGrabbing)
                return false;
            if (grabbable.SocketHoverer && grabbable.SocketHoverer != this)
                return false;
            if (GrabbableMustBeHeld && grabbable.GrabberCount != 1)
                return false;
            var handGrabber = grabbable.PrimaryGrabber as HVRHandGrabber;
            if (handGrabber == null)
                return false;

            return base.CanHover(grabbable);
        }

        protected override void OnHoverEnter(HVRGrabbable grabbable)
        {
            if (GrabsFromHand)
            {
                if (CanAddGrabbable && grabbable.IsBeingHeld && grabbable.GrabberCount == 1 &&
                    grabbable.PrimaryGrabber.IsHandGrabber && CanGrabEx(grabbable))
                {
                    grabbable.PrimaryGrabber.ForceRelease();
                    TryGrab(grabbable, true);
                }

                return;
            }

            grabbable.Released.AddListener(OnHoverGrabbableReleased);



            base.OnHoverEnter(grabbable);
            if (HoverActions != null)
            {
                foreach (var action in HoverActions)
                {
                    action.OnHoverEnter(this, grabbable, IsValid(grabbable));
                }
            }
        }

        public void OnHandGrabberEntered()
        {
            if (HandGrabActions != null)
            {
                foreach (var action in HandGrabActions)
                {
                    action.OnHoverEnter(this, GrabbedTarget, true);
                }
            }
        }

        public void OnHandGrabberExited()
        {
            if (HandGrabActions != null)
            {
                foreach (var action in HandGrabActions)
                {
                    action.OnHoverExit(this, GrabbedTarget, true);
                }
            }
        }

        private void OnHoverGrabbableReleased(HVRGrabberBase grabber, HVRGrabbable grabbable)
        {
            UnhoverGrabbable(grabber, grabbable);
            //drop could have been a hand swap or some other swapping action
            //so we wait til next frame and see if it's not grabbed by something else this frame
            StartCoroutine(TryGrabGrabbable(grabbable));
        }

        private IEnumerator TryGrabGrabbable(HVRGrabbable grabbable)
        {
            yield return new WaitForFixedUpdate();

            if (CanAddGrabbable && TryGrab(grabbable))
            {

            }
        }

        protected override void OnHoverExit(HVRGrabbable grabbable)
        {
            grabbable.Released.RemoveListener(OnHoverGrabbableReleased);
            base.OnHoverExit(grabbable);
            if (HoverActions != null)
            {
                foreach (var action in HoverActions)
                {
                    action.OnHoverExit(this, grabbable, IsValid(grabbable));
                }
            }
        }

        protected override void CheckGrab()
        {
            if (GrabbableMustBeHeld) return;
            base.CheckGrab();
        }

        public override bool CanGrab(HVRGrabbable grabbable)
        {
            if (grabbable.IsBeingHeld && grabbable != GrabbedTarget && !grabbable.PrimaryGrabber.AllowSwap)
                return false;

            return CanGrabEx(grabbable);
        }

        private bool CanGrabEx(HVRGrabbable grabbable)
        {
            if (LinkedGrabbable && LinkedGrabbable != grabbable)
                return false;
            if (grabbable.IsBeingForcedGrabbed)
                return false;
            if (!IsValid(grabbable))
                return false;

            return base.CanGrab(grabbable) && (!GrabbedTarget || GrabbedTarget == grabbable);
        }

        public bool IsValid(HVRGrabbable grabbable)
        {
            if (SocketFilters != null)
            {
                if (!grabbable.Socketable)
                    return false;

                var anyValid = false;
                foreach (var filter in SocketFilters)
                {
                    if (filter.IsValid(grabbable.Socketable) && FilterCondition == SocketCondition.OR)
                    {
                        anyValid = true;
                        break;
                    }

                    if (!filter.IsValid(grabbable.Socketable) && FilterCondition == SocketCondition.AND)
                        return false;
                }

                return anyValid || FilterCondition == SocketCondition.AND;
            }

            return true;
        }

        protected internal override void OnBeforeHover(HVRGrabbable grabbable)
        {
            base.OnBeforeHover(grabbable);
            grabbable.SocketHoverer = this;
        }

        protected internal override void OnAfterHover(HVRGrabbable grabbable)
        {
            base.OnAfterHover(grabbable);
            grabbable.SocketHoverer = null;
        }

        protected override void OnGrabbed(HVRGrabArgs args)
        {
            base.OnGrabbed(args);

            var grabbable = args.Grabbable;
            _previousParent = grabbable.transform.parent;
            _previousScale = grabbable.transform.localScale;
            grabbable.transform.parent = transform;
            if (grabbable.Rigidbody)
            {
                grabbable.Rigidbody.useGravity = false;
                grabbable.Rigidbody.velocity = Vector3.zero;
                grabbable.Rigidbody.angularVelocity = Vector3.zero;
                grabbable.Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                grabbable.Rigidbody.isKinematic = true;
            }

            grabbable.transform.localPosition = Vector3.zero;
            grabbable.transform.localRotation = Quaternion.identity;
            grabbable.SetAllToTrigger();

            var socketable = grabbable.Socketable;

            _modelBounds = grabbable.ModelBounds;

            UpdateScale(grabbable);

            Vector3 offSet = Vector3.zero;

            if (socketable.SocketOrientation)
            {
                offSet = -socketable.SocketOrientation.localPosition;
                var rotationOffset = GetRotationOffset(grabbable);
                var delta = grabbable.transform.localRotation * Quaternion.Inverse(rotationOffset);
                grabbable.transform.localRotation *= delta;
                offSet = delta * offSet;
            }

            offSet.x *= grabbable.transform.localScale.x;
            offSet.y *= grabbable.transform.localScale.y;
            offSet.z *= grabbable.transform.localScale.z;

            grabbable.transform.localPosition += offSet;

            PlaySFX(socketable);

            if (args.RaiseEvents)
            {
                Grabbed.Invoke(this, grabbable);
            }
        }

        private void PlaySFX(HVRSocketable socketable)
        {
            if (AudioGrabbedOverride)
            {
                SFXPlayer.Instance.PlaySFX(AudioGrabbedOverride, transform.position);
            }
            else if (socketable.SocketedClip)
            {
                SFXPlayer.Instance.PlaySFX(socketable.SocketedClip, transform.position);
            }
            else if (AudioGrabbedFallback)
            {
                SFXPlayer.Instance.PlaySFX(AudioGrabbedFallback, transform.position);
            }
        }

        protected virtual Quaternion GetRotationOffset(HVRGrabbable grabbable)
        {
            if (!grabbable || !grabbable.Socketable || !grabbable.Socketable.SocketOrientation)
                return Quaternion.identity;
            return grabbable.Socketable.SocketOrientation.localRotation;
        }

        private void UpdateScale(HVRGrabbable grabbable)
        {
            if (!grabbable || !ScaleGrabbable)
                return;
            var extents = _modelBounds.extents;
            var axis = extents.x;
            if (extents.y > axis) axis = extents.y;
            if (extents.z > axis) axis = extents.z;
            axis *= 2;
            var ratio = Size / axis;
            ratio *= grabbable.Socketable.SocketScale;
            var counterScale = grabbable.Socketable.CounterScale;
            grabbable.transform.localScale = new Vector3(ratio * counterScale.x, ratio * counterScale.y, ratio * counterScale.z);
        }

        protected override void OnReleased(HVRGrabbable grabbable)
        {
            if (_appQuitting)
            {
                return;
            }

            base.OnReleased(grabbable);

            grabbable.ResetToNonTrigger();
            grabbable.transform.parent = _previousParent;
            grabbable.transform.localScale = _previousScale;
            _previousParent = null;

            if (AudioReleasedOverride)
            {
                SFXPlayer.Instance.PlaySFX(AudioReleasedOverride, transform.position);
            }
            else if (grabbable.Socketable.UnsocketedClip)
            {
                SFXPlayer.Instance.PlaySFX(grabbable.Socketable.UnsocketedClip, transform.position);
            }
            else if (AudioReleasedFallback)
            {
                SFXPlayer.Instance.PlaySFX(AudioReleasedFallback, transform.position);
            }

            CheckAutoSpawn();
        }
    }

    [Serializable]
    public class SocketSpawnEvent : UnityEvent<HVRSocket, GameObject>
    {

    }

    public enum SocketCondition
    {
        AND,
        OR
    }
}