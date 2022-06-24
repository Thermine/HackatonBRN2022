using System;
using System.Collections.Generic;
using HurricaneVR.Framework.Core.Bags;
using HurricaneVR.Framework.Shared;
using UnityEngine;


namespace HurricaneVR.Framework.Core.Grabbers
{
    //[RequireComponent(typeof(Rigidbody))]
    public abstract class HVRGrabberBase : MonoBehaviour
    {
        public VRGrabberEvent BeforeGrabbed = new VRGrabberEvent();
        public VRGrabberEvent Grabbed = new VRGrabberEvent();
        public VRGrabberEvent Released = new VRGrabberEvent();
        public VRGrabberEvent HoverEnter = new VRGrabberEvent();
        public VRGrabberEvent HoverExit = new VRGrabberEvent();


        [Header("Grabbable Bags")]
        [SerializeField] private HVRGrabbableBag _grabBag;
        public List<HVRGrabbableBag> GrabBags = new List<HVRGrabbableBag>();

        public virtual Quaternion ControllerRotation { get; set; } = Quaternion.identity;

        public bool AllowHovering { get; set; }
        public virtual bool AllowGrabbing { get; set; }
        public bool IsGrabbing => GrabbedTarget;
        public bool IsHovering => HoverTarget;

        public HVRGrabbable HoverTarget { get; private set; }
        public HVRGrabbable GrabbedTarget { get; internal set; }

        public virtual bool IsGrabActive { get; protected set; }

        public virtual bool IsHoldActive { get; protected set; }

        public virtual bool IsHandGrabber => false;
        public virtual bool IsSocket => false;

        public virtual bool AllowSwap => false;

        public Rigidbody Rigidbody { get; private set; }

        protected Transform _grabPoint;
        public virtual Transform GrabPoint
        {
            get => _grabPoint;
            set
            {
                _grabPoint = value;
            }
        }


        public virtual Vector3 JointAnchorWorldPosition { get; }
        public Vector3 Velocity { get; protected set; }

        public virtual bool IsMine { get; set; } = true;
        public virtual bool PerformUpdate { get; set; } = true;

        public bool MonitoringJointRotation { get; set; }

        protected virtual void OnEnable()
        {

        }

        protected virtual void OnDisable()
        {
            HVRManager.Instance.UnregisterGrabber(this);
        }

        protected virtual void OnDestroy()
        {
            HVRManager.Instance.UnregisterGrabber(this);
        }

        protected virtual void Start()
        {
            HVRManager.Instance.RegisterGrabber(this);
            Rigidbody = GetComponent<Rigidbody>();
            AllowGrabbing = true;
            AllowHovering = true;

            if (_grabBag)
            {
                if (!GrabBags.Contains(_grabBag))
                {
                    GrabBags.Add(_grabBag);
                }
            }

            foreach (var bag in GrabBags)
            {
                bag.Grabber = this;
            }
        }

        protected virtual void Update()
        {
        }

        protected virtual void FixedUpdate()
        {
            if (PerformUpdate)
            {
                CheckRelease();
            }

            CheckUnHover();

            if (PerformUpdate)
            {
                CheckGrab();
            }

            CheckHover();
        }


        protected virtual void CheckRelease()
        {
            if (GrabbedTarget)// && (!IsHoldActive))// || !CanGrab(GrabbedTarget)))
            {
                if (!IsHoldActive)
                    ReleaseGrabbable(this, GrabbedTarget);
            }
        }

        public void ForceRelease()
        {
            //Debug.Log("Force Releasing.");
            if (GrabbedTarget)
            {
                ReleaseGrabbable(this, GrabbedTarget);
            }
            else
            {
                //Debug.Log("Nothing to force release.");
            }
        }

        internal static void ReleaseGrabbable(HVRGrabberBase grabber, HVRGrabbable grabbable, bool raiseEvents = true)
        {
            grabber.OnReleased(grabbable);
            grabbable.InternalOnReleased(grabber);

            if (raiseEvents)
            {
                grabbable.Released.Invoke(grabber, grabbable);
            }
        }

        public virtual bool ForceGrabCheck()
        {
            for (var g = 0; g < GrabBags.Count; g++)
            {
                var grabBag = GrabBags[g];
                for (var i = 0; i < grabBag.ValidGrabbables.Count; i++)
                {
                    var grabbable = grabBag.ValidGrabbables[i];
                    if (CanGrab(grabbable))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public virtual bool IsValidGrabbable(HVRGrabbable grabbable)
        {
            for (var i = 0; i < GrabBags.Count; i++)
            {
                var bag = GrabBags[i];
                if (bag.ValidGrabbables.Contains(grabbable))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the closest grabbable in the grabbable bag.
        /// </summary>
        public virtual HVRGrabbable GetClosestGrabbable()
        {
            for (var i = 0; i < GrabBags.Count; i++)
            {
                var bag = GrabBags[i];
                if (bag.ClosestGrabbable)
                    return bag.ClosestGrabbable;
            }

            return null;
        }

        /// <summary>
        /// Returns the closest grabbable in the grabbable bag that satisfies canGrab delegate.
        /// </summary>
        public virtual HVRGrabbable GetClosestGrabbable(Predicate<HVRGrabbable> canGrab)
        {
            for (var i = 0; i < GrabBags.Count; i++)
            {
                var bag = GrabBags[i];
                if (bag.ClosestGrabbable && canGrab(bag.ClosestGrabbable))
                    return bag.ClosestGrabbable;
            }

            return null;
        }

        protected virtual void CheckGrab()
        {
            if (!IsGrabActive || !AllowGrabbing)
            {
                return;
            }

            for (var g = 0; g < GrabBags.Count; g++)
            {
                var grabBag = GrabBags[g];
                for (var i = 0; i < grabBag.ValidGrabbables.Count; i++)
                {
                    var grabbable = grabBag.ValidGrabbables[i];
                    if (TryGrab(grabbable))
                        break;
                }
            }
        }


        public bool TryGrab(HVRGrabbable grabbable, bool force = false)
        {
            if (force || CanGrab(grabbable) && GrabbedTarget != grabbable)
            {
                CheckForceRelease(grabbable);
                GrabGrabbable(this, grabbable);
                return true;
            }

            return false;
        }

        public virtual void CheckForceRelease(HVRGrabbable grabbable)
        {
            if (grabbable.IsBeingForcedGrabbed ||
                grabbable.PrimaryGrabber && grabbable.PrimaryGrabber.AllowSwap)
            {
                grabbable.PrimaryGrabber.ForceRelease();
                return;
            }

            var handSwap = grabbable.HoldType == HVRHoldType.AllowSwap && grabbable.PrimaryGrabber is HVRHandGrabber && this is HVRHandGrabber;

            if (handSwap)
            {
                grabbable.PrimaryGrabber.ForceRelease();
                return;
            }
        }

        internal static void GrabGrabbable(HVRGrabberBase grabber, HVRGrabbable grabbable, bool raiseEvents = true)
        {
            if (raiseEvents)
            {
                grabber.BeforeGrabbed.Invoke(grabber, grabbable);
            }

            grabbable.InternalOnBeforeGrabbed(grabber);
            var args = new HVRGrabArgs(grabbable);
            args.RaiseEvents = raiseEvents;
            

            grabber.OnBeforeGrabbed(args);

            if (args.Cancel)
            {
                grabbable.InternalOnGrabCanceled(grabber);
                return;
            }

            grabber.GrabbedTarget = grabbable;
            grabber.OnGrabbed(args);

            if (args.Cancel)
            {
                grabber.GrabbedTarget = null;
                grabbable.InternalOnGrabCanceled(grabber);
            }
            else
            {
                grabbable.InternalOnGrabbed(grabber);
                grabber.InternalOnAfterGrabbed(grabbable);
            }
        }

        internal virtual void InternalOnGrabbed(HVRGrabArgs args)
        {
            OnGrabbed(args);
        }

        protected virtual void OnBeforeGrabbed(HVRGrabArgs args)
        {

        }

        protected virtual void OnGrabbed(HVRGrabArgs args)
        {
            //if (!grabbable.SameScale)
            //{
            //    Debug.LogError($"{grabbable.name} not same scale. {grabbable.transform.localScale},{grabbable.StartingScale}");
            //}

            args.Grabbable.Destroyed.AddListener(OnGrabbableDestroyed);
        }

        internal virtual void InternalOnAfterGrabbed(HVRGrabbable grabbable)
        {
            OnAfterGrabbed(grabbable);
        }

        protected virtual void OnAfterGrabbed(HVRGrabbable grabbable)
        {

        }

        protected virtual void CheckUnHover()
        {
            if (!HoverTarget)
                return;

            var closestValid = ClosestValidHover();

            if (!CanHover(HoverTarget) || closestValid != HoverTarget)
            {
                UnhoverGrabbable(this, HoverTarget);
            }
        }

        protected HVRGrabbable ClosestValidHover()
        {
            for (var g = 0; g < GrabBags.Count; g++)
            {
                var grabBag = GrabBags[g];
                for (var i = 0; i < grabBag.ValidGrabbables.Count; i++)
                {
                    var grabbable = grabBag.ValidGrabbables[i];
                    if (CanHover(grabbable))
                    {
                        return grabbable;
                    }
                }
            }

            return null;
        }


        protected virtual bool CheckHover()
        {
            if (IsHovering || !AllowHovering)
            {
                return true;
            }

            var closestValid = ClosestValidHover();
            if (closestValid == null)
                return false;

            HoverGrabbable(this, closestValid);
            return true;
        }

        protected internal virtual void OnBeforeHover(HVRGrabbable grabbable)
        {

        }

        protected internal virtual void OnAfterHover(HVRGrabbable grabbable)
        {

        }

        protected void HoverGrabbable(HVRGrabberBase grabber, HVRGrabbable grabbable)
        {
            if (grabber.IsHovering)
                return;

            OnBeforeHover(grabbable);

            grabber.HoverTarget = grabbable;

            grabbable.InternalOnHoverEnter(grabber);
            grabber.OnHoverEnter(grabbable);
            grabbable.HoverEnter.Invoke(grabber, grabbable);
            grabber.HoverEnter.Invoke(grabber, grabbable);
        }

        protected void UnhoverGrabbable(HVRGrabberBase grabber, HVRGrabbable grabbable)
        {
            try
            {
                if (grabbable && grabber)
                {
                    grabbable.InternalOnHoverExit(grabber);
                    grabbable.HoverExit.Invoke(grabber, grabbable);
                }

                if (grabber.HoverTarget)
                {
                    grabber.OnHoverExit(grabbable);
                    grabber.HoverExit.Invoke(grabber, grabbable);
                }
            }
            finally
            {
                if (grabber)
                    grabber.HoverTarget = null;

                OnAfterHover(grabbable);
            }
        }

        public virtual bool CanGrab(HVRGrabbable grabbable)
        {
            return AllowGrabbing;
        }

        public virtual bool CanHover(HVRGrabbable grabbable)
        {
            return AllowHovering;
        }



        protected virtual void OnReleased(HVRGrabbable grabbable)
        {
            GrabbedTarget = null;
            grabbable.Destroyed.RemoveListener(OnGrabbableDestroyed);
        }

        private void OnGrabbableDestroyed(HVRGrabbable grabbable)
        {
            GrabbedTarget = null;
            OnReleased(grabbable);
            Released.Invoke(this, grabbable); //hmmmm...grabbable is destroyed...
        }

        protected virtual void OnHoverEnter(HVRGrabbable grabbable)
        {
        }

        protected virtual void OnHoverExit(HVRGrabbable grabbable)
        {

        }

    }

    public class HVRGrabArgs
    {
        public HVRGrabArgs(HVRGrabbable grabbable)
        {
            Grabbable = grabbable;
        }

        public bool Cancel;
        public HVRGrabbable Grabbable;
        public bool RaiseEvents = true;
    }

}