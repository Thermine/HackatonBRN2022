using UnityEngine;

namespace HurricaneVR.Framework.Shared.HandPoser
{
    public class HVRPosableGrabPoint : MonoBehaviour
    {
        public Transform VisualGrabPoint;

        public HVRHandPoser HandPoser;
        [Range(0f, 360f)]
        public float AllowedAngleDifference = 360f;

        public bool LeftHand = true;
        public bool RightHand = true;

        public Quaternion LeftPoseOffset { get; private set; }
        public Quaternion RightPoseOffset { get; private set; }

        public Vector3 LeftPosePositionOffset { get; private set; }

        public Vector3 RightPosePositionOffset { get; private set; }

        public Vector3 jointOffset;
        public Quaternion JointOffset { get; private set; }

        public bool IsSecondaryLookAt;

        void Start()
        {
            HandPoser = GetComponent<HVRHandPoser>();

            LeftPoseOffset = Quaternion.identity;
            RightPoseOffset = Quaternion.identity;

            JointOffset = Quaternion.Euler(jointOffset);

            if (HandPoser)
            {
                if (HandPoser && HandPoser.PrimaryPose != null && HandPoser.PrimaryPose.Pose && HandPoser.PrimaryPose.Pose.RightHand != null)
                {
                    RightPoseOffset = Quaternion.Euler(HandPoser.PrimaryPose.Pose.RightHand.Rotation.eulerAngles);
                    RightPosePositionOffset = HandPoser.PrimaryPose.Pose.RightHand.Position;
                }
                else if(RightHand)
                {
                    Debug.LogWarning($"Right Hand pose missing! {this.name}");
                }

                if (HandPoser && HandPoser.PrimaryPose != null && HandPoser.PrimaryPose.Pose && HandPoser.PrimaryPose.Pose.LeftHand != null && LeftHand)
                {
                    LeftPoseOffset = Quaternion.Euler(HandPoser.PrimaryPose.Pose.LeftHand.Rotation.eulerAngles);
                    LeftPosePositionOffset = HandPoser.PrimaryPose.Pose.LeftHand.Position;
                }
                else if(LeftHand)
                {
                    Debug.LogWarning($"Left Hand pose missing! {this.name}");
                }
            }
        }

        private void Update()
        {
            JointOffset = Quaternion.Euler(jointOffset);
        }

        public Vector3 GetPosePositionOffset(HVRHandSide side)
        {
            if (side == HVRHandSide.Left)
                return LeftPosePositionOffset;
            return RightPosePositionOffset;
        }

        public Quaternion GetPoseRotationOffset(HVRHandSide side)
        {
            if (side == HVRHandSide.Left)
                return LeftPoseOffset;
            return RightPoseOffset;
        }

        public Quaternion GetJointRotationOffset(HVRHandSide side)
        {
            return Quaternion.Inverse(JointOffset) * GetPoseRotationOffset(side);
        }

        public Quaternion GetPoseRotation(HVRHandSide side)
        {
            if (side == HVRHandSide.Left) return transform.rotation * LeftPoseOffset;
            return transform.rotation * RightPoseOffset;
        }

        public Quaternion GetPoseWithJointRotation(HVRHandSide side)
        {
            return transform.rotation * GetJointRotationOffset(side);
        }

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            // Show Grip Points
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.015f);
        }

#endif
    }
}
