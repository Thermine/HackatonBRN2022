using System;
using System.Collections;
using HurricaneVR.Framework.ControllerInput;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace HurricaneVR.Framework.Core.Player
{
    public class HVRCameraRig : MonoBehaviour
    {
        public Transform Camera;
        public TrackingOriginModeFlags TrackingSpace;
        private TrackingOriginModeFlags _previousTrackingSpace;

        public Transform FloorOffset;
        public float CameraYOffset = 1.5f;
        public float PlayerControllerYOffset = 0f;
        public float AdjustedCameraHeight;

        public float StartingHeightSpeed = .05f;
        public float StartingHeight = 1.5f;
        public bool ForceStartingHeight;

        public bool IsMine { get; set; } = true;

        [Tooltip("If true, use up and down arrow to change YOffset to help with testing.")]
        public bool DebugKeyboardOffset;

        void Start()
        {
            XRSettings.eyeTextureResolutionScale = 1.5f;
            _previousTrackingSpace = TrackingSpace;
            Setup();
            if (ForceStartingHeight)
                StartCoroutine(EnforceStartingHeight());
        }

        private IEnumerator EnforceStartingHeight()
        {
            yield return null;

            while (Mathf.Abs(StartingHeight - AdjustedCameraHeight) > .05f)
            {
                var delta = StartingHeight - AdjustedCameraHeight;
                CameraYOffset += StartingHeightSpeed * Mathf.Sign(delta);
                yield return new WaitForFixedUpdate();
            }
        }

        void Update()
        {
            if (TrackingSpace != _previousTrackingSpace)
            {
                Setup();
                _previousTrackingSpace = TrackingSpace;
            }

            if (FloorOffset)
            {
                var pos = FloorOffset.transform.localPosition;
                var intendedOffset = CameraYOffset + PlayerControllerYOffset;
                var intendedCameraHeight = intendedOffset + Camera.localPosition.y;
                //if (intendedCameraHeight < 0)
                //{
                //    intendedOffset += (0 - intendedCameraHeight);
                //}
                FloorOffset.transform.localPosition = new Vector3(pos.x, intendedOffset, pos.z);
            }

            AdjustedCameraHeight = FloorOffset.transform.localPosition.y + Camera.localPosition.y;

            if (IsMine)
            {

                if (DebugKeyboardOffset && UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))// || HVRInputManager.Instance.LeftController.PrimaryButtonState.JustActivated)
                {
                    CameraYOffset += -.25f;
                }
                else if (DebugKeyboardOffset && UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))// || HVRInputManager.Instance.LeftController.SecondaryButtonState.JustActivated)
                {
                    CameraYOffset += .25f;
                }
            }
        }

        private void Setup()
        {
            var offset = CameraYOffset;

            StartCoroutine(UpdateTrackingOrigin(TrackingSpace));

            if (FloorOffset)
            {
                var pos = FloorOffset.transform.localPosition;
                FloorOffset.transform.localPosition = new Vector3(pos.x, offset, pos.z);
            }
        }


        IEnumerator UpdateTrackingOrigin(TrackingOriginModeFlags originFlags)
        {
            yield return null;

#if USING_XR_MANAGEMENT
             var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances<XRInputSubsystem>(subsystems);
            Debug.Log("Found " + subsystems.Count + " input subsystems.");

            for (int i = 0; i < subsystems.Count; i++)
            {
                if (subsystems[i].TrySetTrackingOriginMode(originFlags))
                    Debug.Log("Successfully set TrackingOriginMode to Floor");
                else
                    Debug.Log("Failed to set TrackingOriginMode to Floor");
            }

#elif !UNITY_2020_1_OR_NEWER


            if (originFlags == TrackingOriginModeFlags.Floor)
            {
                if (XRDevice.SetTrackingSpaceType(TrackingSpaceType.RoomScale))
                {
                    Debug.Log("Tracking change to RoomScale.");
                }
                else
                {
                    Debug.Log("Failed Tracking change to RoomScale.");
                }
            }
            else
            {
                XRDevice.SetTrackingSpaceType(TrackingSpaceType.Stationary);
                Debug.Log("Tracking change to stationary.");
            }

#endif

        }
    }
}