using System;
using System.Collections.Generic;
using UnityEngine;

namespace HurricaneVR.Framework.Core.Bags
{
    public class HVRTriggerGrabbableBag : HVRGrabbableBag
    {
        private readonly Dictionary<HVRGrabbable, HashSet<Collider>> _map = new Dictionary<HVRGrabbable, HashSet<Collider>>();

        [Tooltip("If true it will use Collider.ClosestPoint method to determine the closest grabbable.")]
        public bool UseColliderDistance = true;

        protected override void Start()
        {
            base.Start();

            if (Math.Abs(MaxDistanceAllowed) < .001)
            {
                //Debug.Log($"{gameObject.name}: MaxDistanceAllowed too low, setting to 1.5f");
                MaxDistanceAllowed = 1.5f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var grabbable = other.GetComponent<HVRGrabbable>();
            var childGrabbable = other.GetComponent<HVRGrabbableChild>();
            if (!grabbable && childGrabbable && childGrabbable.ParentGrabbable)
            {
                grabbable = childGrabbable.ParentGrabbable;
            }

            if (grabbable)
            {
                if (!_map.TryGetValue(grabbable, out var colliders))
                {
                    colliders = new HashSet<Collider>();
                    _map[grabbable] = colliders;
                }

                if (colliders.Count == 0)
                {
                    AddGrabbable(grabbable);
                }

                colliders.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {


            var grabbable = other.GetComponent<HVRGrabbable>();
            var childGrabbable = other.GetComponent<HVRGrabbableChild>();
            if (!grabbable && childGrabbable && childGrabbable.ParentGrabbable)
            {
                grabbable = childGrabbable.ParentGrabbable;
            }

            if (grabbable)
            {
                if (_map.TryGetValue(grabbable, out var colliders))
                {
                    colliders.Remove(other);
                }

                if (colliders == null || colliders.Count == 0)
                {
                    RemoveGrabbable(grabbable);
                }
            }
        }

        public override float DistanceToGrabbable(HVRGrabbable grabbable)
        {
            if (UseColliderDistance && _map.TryGetValue(grabbable, out var colliders))
            {
                var distance = float.MaxValue;
                foreach (var c in colliders)
                {
                    var point = c.ClosestPoint(Grabber.transform.position);
                    var current = Vector3.Distance(point, Grabber.transform.position);
                    if (current < distance) distance = current;
                }

                return distance;
            }

            return base.DistanceToGrabbable(grabbable);
        }

    }
}