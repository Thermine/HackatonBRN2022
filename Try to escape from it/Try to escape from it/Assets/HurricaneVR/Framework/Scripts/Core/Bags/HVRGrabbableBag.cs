using System.Collections.Generic;
using System.Linq;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Shared;
using UnityEngine;

namespace HurricaneVR.Framework.Core.Bags
{
    public class HVRGrabbableBag : MonoBehaviour
    {
        public VRGrabbableEvent GrabbableRemoved = new VRGrabbableEvent();

        [Tooltip("If true, grabbed objects will be penalized with the sorting.")]
        public bool PenalizeGrabbed = true;

        public HVRSortMode hvrSortMode;
        public List<HVRGrabbable> ValidGrabbables = new List<HVRGrabbable>(1000);
        public HVRGrabbable ClosestGrabbable;
        public HVRGrabberBase Grabber;
        public List<HVRGrabbable> IgnoredGrabbables = new List<HVRGrabbable>();
        public float MaxDistanceAllowed;

        private readonly List<HVRGrabbable> _allGrabbables = new List<HVRGrabbable>();
        private readonly HashSet<HVRGrabbable> _distinctGrabbables = new HashSet<HVRGrabbable>();
        private readonly List<HVRGrabbable> _grabbablesToRemove = new List<HVRGrabbable>(1000);
        private readonly Dictionary<HVRGrabbable, float> DistanceMap = new Dictionary<HVRGrabbable, float>();
        private readonly HashSet<HVRGrabbable> _ignoredGrabbables = new HashSet<HVRGrabbable>();

        protected virtual void Start()
        {
            IgnoredGrabbables.ForEach(g => _ignoredGrabbables.Add(g));
        }

        private void FixedUpdate()
        {
            Calculate();
        }

        protected void AddGrabbable(HVRGrabbable grabbable)
        {
            if (_distinctGrabbables.Contains(grabbable))
            {
                return;
            }
            _distinctGrabbables.Add(grabbable);
            _allGrabbables.Add(grabbable);
            grabbable.Destroyed.AddListener(OnGrabbableDestroyed);
        }

        private void OnGrabbableDestroyed(HVRGrabbable grabbable)
        {
            RemoveGrabbable(grabbable);
        }


        protected virtual void RemoveGrabbable(HVRGrabbable grabbable)
        {
            if (_distinctGrabbables.Contains(grabbable))
            {
                _allGrabbables.Remove(grabbable);
            }
            _distinctGrabbables.Remove(grabbable);
            grabbable.Destroyed.RemoveListener(OnGrabbableDestroyed);
            GrabbableRemoved.Invoke(grabbable);
        }

        protected virtual void Calculate()
        {
            ValidGrabbables.Clear();
            _grabbablesToRemove.Clear();

            var anyDestroyedOrDisabled = false;

            for (var i = 0; i < _allGrabbables.Count; i++)
            {
                var grabbable = _allGrabbables[i];
                if (!grabbable || !grabbable.gameObject.activeSelf || !grabbable.enabled)
                {
                    anyDestroyedOrDisabled = true;
                    continue;
                }

                DistanceMap[grabbable] = DistanceToGrabbable(grabbable);

                if (DistanceMap[grabbable] > MaxDistanceAllowed)
                {
                    _grabbablesToRemove.Add(grabbable);
                }
                else if (IsValid(grabbable))
                {
                    if (PenalizeGrabbed && grabbable.IsBeingHeld)
                    {
                        DistanceMap[grabbable] += 1000f;
                    }

                    ValidGrabbables.Add(grabbable);
                }
            }

            if (anyDestroyedOrDisabled)
            {
                _distinctGrabbables.RemoveWhere(e => e == null || !e.gameObject.activeSelf || !e.enabled);
                _allGrabbables.RemoveAll(e => e == null || !e.gameObject.activeSelf || !e.enabled);
            }

            for (var index = 0; index < _grabbablesToRemove.Count; index++)
            {
                var invalid = _grabbablesToRemove[index];
                RemoveGrabbable(invalid);
            }

            // x->y ascending sort
            ValidGrabbables.Sort((x, y) => DistanceMap[x].CompareTo(DistanceMap[y]));

            ClosestGrabbable = ValidGrabbables.FirstOrDefault();
        }


        public virtual float DistanceToGrabbable(HVRGrabbable grabbable)
        {
            if (hvrSortMode == HVRSortMode.Distance)
                return grabbable.GetDistanceToGrabber(Grabber);
            return grabbable.GetSquareDistanceToGrabber(Grabber);
        }

        protected virtual bool IsValid(HVRGrabbable grabbable)
        {
            if (grabbable.RequiresGrabbable)
            {
                if (!grabbable.RequiredGrabbable || !grabbable.RequiredGrabbable.IsBeingHeld)
                {
                    return false;
                }
            }
            return grabbable.CanBeGrabbed && !_ignoredGrabbables.Contains(grabbable);
        }
    }
}