using System;
using UnityEngine;
using UnityEngine.Events;

namespace HurricaneVR.Framework.Components
{
    public class HVRCollisionEvents : MonoBehaviour
    {
        public UnityEvent ThresholdMet = new UnityEvent();

        public CollisionEventType CollisionType = CollisionEventType.Impulse;

        public float ForceThreshold;
        public float VelocityThreshold;

        public float LastImpulse;
        public float LastVelocity;

        public float MaxImpulse;
        public float MaxVelocity;

        private void OnCollisionEnter(Collision other)
        {
            LastImpulse =  other.impulse.magnitude;
            LastVelocity = other.relativeVelocity.magnitude;

            MaxImpulse = Mathf.Max(MaxImpulse, LastImpulse);
            MaxVelocity = Mathf.Max(MaxVelocity, LastVelocity);

            if (CollisionType == CollisionEventType.Impulse && LastImpulse > ForceThreshold ||
                CollisionType == CollisionEventType.Velocity && LastVelocity > VelocityThreshold)
            {
                ThresholdMet.Invoke();
            }
        }
    }

    [Serializable]
    public enum CollisionEventType
    {
        Impulse, Velocity
    }

}
