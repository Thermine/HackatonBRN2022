using System;
using HurricaneVR.Framework.Core.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace HurricaneVR.Framework.Components
{
    [RequireComponent(typeof(Rigidbody))]
    public class HVRButton : MonoBehaviour
    {
        public VRButtonEvent ButtonDown = new VRButtonEvent();
        public VRButtonEvent ButtonUp = new VRButtonEvent();


        public Vector3 Axis;

        public float Threshold;
        public float UpThreshold;
        public Vector3 StartPosition;
        public bool IsPressed = false;

        public AudioClip AudioButtonDown;
        public AudioClip AudioButtonUp;

        public Rigidbody Rigidbody { get; private set; }

        protected virtual void Awake()
        {
            StartPosition = transform.localPosition;
            Rigidbody = GetComponent<Rigidbody>();
        }

        void Update()
        {

        }

        private void FixedUpdate()
        {
            var distance = (StartPosition - transform.localPosition).magnitude;

            if (!IsPressed && distance >= Threshold)
            {
                IsPressed = true;
                OnButtonDown();
            }
            else if (IsPressed && distance < UpThreshold)
            {
                IsPressed = false;
                OnButtonUp();
            }

            ClampBounds();
        }

        private void ClampBounds()
        {
            var test = new Vector3(transform.localPosition.x * Axis.x, transform.localPosition.y * Axis.y, transform.localPosition.z * Axis.z);

            if (test.x > StartPosition.x || test.y > StartPosition.y || test.z > StartPosition.z)
            {
                transform.localPosition = StartPosition;
                Rigidbody.velocity = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            ClampBounds();
        }

        protected virtual void OnButtonDown()
        {
            if (AudioButtonDown)
                SFXPlayer.Instance.PlaySFX(AudioButtonDown, transform.position);

            ButtonDown.Invoke(this);
        }

        protected virtual void OnButtonUp()
        {
            if (AudioButtonUp)
                SFXPlayer.Instance.PlaySFX(AudioButtonUp, transform.position);
            ButtonUp.Invoke(this);
        }
    }

    [Serializable]
    public class VRButtonEvent : UnityEvent<HVRButton> { }

}
