using UnityEngine;

namespace HurricaneVR.Framework.Components
{
    public abstract class HVRGrabbableHoverBase : MonoBehaviour
    {
        protected virtual void Start()
        {

        }

        protected virtual void Update()
        {

        }

        public abstract void Hover();

        public abstract void Unhover();

        public abstract void Enable();
        public abstract void Disable();
    }
}