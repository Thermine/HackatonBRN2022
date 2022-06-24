using UnityEngine;

namespace HurricaneVR.Framework.Core.Bags
{
    public class HVRForceGrabberBag : HVRTriggerGrabbableBag
    {
        public Transform RayCastOrigin;
        public LayerMask LayerMask;


        protected override void Start()
        {
            base.Start();
        }

        protected override void Calculate()
        {
            base.Calculate();
        }


        protected override bool IsValid(HVRGrabbable grabbable)
        {
            if (!base.IsValid(grabbable))
                return false;

            var ray = new Ray();

            for (var i = 0; i < grabbable.Colliders.Length; i++)
            {
                var grabbableCollider = grabbable.Colliders[i];

                ray.origin = RayCastOrigin.position;
                ray.direction = grabbableCollider.bounds.center - ray.origin;

                if (Physics.Raycast(ray, out var hit, MaxDistanceAllowed, LayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (Equals(grabbableCollider, hit.collider))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}