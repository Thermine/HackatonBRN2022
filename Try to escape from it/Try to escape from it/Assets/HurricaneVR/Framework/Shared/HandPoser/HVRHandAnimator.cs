using System.Collections;
using System.Collections.Generic;
using HurricaneVR.Framework.Shared.HandPoser.Data;
using UnityEngine;
using Time = UnityEngine.Time;

namespace HurricaneVR.Framework.Shared.HandPoser
{
    public class HVRHandAnimator : MonoBehaviour
    {
        [Header("Components")]
        public HVRPhysicsPoser PhysicsPoser;
        public HVRPosableHand Hand;
        public HVRHandPoser DefaultPoser;
        
        [Header("Pose recording helpers")]
        public bool UsePhysicsPoser;
        public HVRButtons PhysicsGripButton;
     

        [Header("Debug View")]
        public HVRHandPose CurrentPose;
        public HVRHandPoser CurrentPoser;
        public HVRHandPoseData BlendedPrimary;
        public List<HVRHandPoseData> BlendedSecondarys = new List<HVRHandPoseData>();

        public bool IsMine { get; set; } = true;

        private Coroutine _animationCoroutine;


        void Start()
        {
            
            if (!PhysicsPoser)
            {
                PhysicsPoser = GetComponent<HVRPhysicsPoser>();
            }

            if (!DefaultPoser)
            {
                DefaultPoser = GetComponent<HVRHandPoser>();
            }

            if (!Hand)
            {
                Hand = GetComponent<HVRPosableHand>();
            }

            ResetToDefault();
        }

        private void Update()
        {

        }

        public void Enable()
        {
            enabled = true;
        }

        public void Disable()
        {
            enabled = false;
        }


        private void FixedUpdate()
        {
            if (PhysicsPoser && UsePhysicsPoser)
            {
                var buttonState = HVRController.GetButtonState(Hand.Side, PhysicsGripButton);

                if (buttonState.JustActivated)
                {
                    PhysicsPoser.SimulateClose(~LayerMask.GetMask("Hand"));
                }
                else if (buttonState.JustDeactivated)
                {
                    PhysicsPoser.Hand.Pose(PhysicsPoser.OpenPose);
                    PhysicsPoser.ResetHand();
                }

                //_previousGrip = gripAmount;
            }
            else
            {
                UpdatePoser();
            }
        }

        private void UpdatePoser()
        {
            if (CurrentPoser == null) return;

            UpdateBlends();
            ApplyBlending();
            Hand.Pose(BlendedPrimary);
        }

        private void ApplyBlending()
        {
            ApplyBlend(BlendedPrimary, CurrentPoser.PrimaryPose);

            if (CurrentPoser.Blends == null) return;
            var targetBlends = CurrentPoser.Blends;
            for (int i = 0; i < targetBlends.Count; i++)
            {
                var targetBlend = targetBlends[i];
                var currentPose = BlendedSecondarys[i];
                if (targetBlend.Disabled) continue;
                ApplyBlend(BlendedPrimary, targetBlend);
            }
        }

        private void ApplyBlend(HVRHandPoseData hand, HVRHandPoseBlend targetBlend)
        {
            var target = targetBlend.Pose.GetPose(Hand.IsLeft);
            var lerp = targetBlend.Value * targetBlend.Weight;

            if (targetBlend.Mask == HVRHandPoseMask.None || targetBlend.Mask.HasFlag(HVRHandPoseMask.Hand))
            {
                hand.Position = Vector3.Lerp(hand.Position, target.Position, lerp);
                hand.Rotation = Quaternion.Lerp(hand.Rotation, target.Rotation, lerp);
            }

            for (var i = 0; i < hand.Fingers.Length; i++)
            {
                var finger = hand.Fingers[i];
                var targetFinger = target.Fingers[i];

                HVRHandPoseMask mask;
                if (i == 0) mask = HVRHandPoseMask.Thumb;
                else if (i == 1) mask = HVRHandPoseMask.Index;
                else if (i == 2) mask = HVRHandPoseMask.Middle;
                else if (i == 3) mask = HVRHandPoseMask.Ring;
                else if (i == 4) mask = HVRHandPoseMask.Pinky;
                else continue;

                if (targetBlend.Mask == HVRHandPoseMask.None || targetBlend.Mask.HasFlag(mask))
                {
                    for (var j = 0; j < finger.Bones.Count; j++)
                    {
                        var bone = finger.Bones[j];
                        var targetBone = targetFinger.Bones[j];
                        bone.Position = Vector3.Lerp(bone.Position, targetBone.Position, lerp);
                        bone.Rotation = Quaternion.Lerp(bone.Rotation, targetBone.Rotation, lerp);
                    }
                }
            }
        }

        private void UpdateBlends()
        {
            if (!IsMine)
                return;

            UpdateBlend(CurrentPoser.PrimaryPose);

            if (CurrentPoser.Blends == null) return;
            var blends = CurrentPoser.Blends;
            for (int i = 0; i < blends.Count; i++)
            {
                var blend = blends[i];
                if (blend.Disabled) continue;
                UpdateBlend(blend);
            }
        }

        private void UpdateBlend(HVRHandPoseBlend blend)
        {
            var blendTarget = 0f;

            if (blend.Type == BlendType.Immediate)
            {
                blendTarget = 1f;
            }
            else if (blend.ButtonParameter)
            {
                var button = HVRController.GetButtonState(Hand.Side, blend.Button);
                if (blend.Type == BlendType.BooleanParameter)
                {
                    blendTarget = button.Active ? 1f : 0f;
                }
                else if (blend.Type == BlendType.FloatParameter)
                {
                    blendTarget = button.Value;
                }
            }
            else if (!string.IsNullOrWhiteSpace(blend.AnimationParameter) && blend.AnimationParameter != "None")
            {
                if (blend.Type == BlendType.BooleanParameter)
                {
                    blendTarget = HVRAnimationParameters.GetBoolParameter(Hand.Side, blend.AnimationParameter) ? 1f : 0f;
                }
                else if (blend.Type == BlendType.FloatParameter)
                {
                    blendTarget = HVRAnimationParameters.GetFloatParameter(Hand.Side, blend.AnimationParameter);
                }
            }

            if (blend.Speed > .1f)
            {
                blend.Value = Mathf.Lerp(blend.Value, blendTarget, Time.deltaTime * blend.Speed);
            }
            else
            {
                blend.Value = blendTarget;
            }

        }


        public void ResetToDefault()
        {
            if (DefaultPoser != null)
            {
                SetCurrentPoser(DefaultPoser);
            }
            else
            {
                Debug.Log($"Default poser not set.");
            }
        }

        public void SetCurrentPoser(HVRHandPoser poser)
        {
            CurrentPoser = poser;
            if (poser == null)
                return;

            if (poser.PrimaryPose == null)
            {
                return;
            }

            BlendedPrimary = poser.PrimaryPose.Pose.GetPose(Hand.IsLeft).DeepCopy();

            foreach (var blend in poser.Blends)
            {
                BlendedSecondarys.Add(blend.Pose.GetPose(Hand.IsLeft).DeepCopy());
            }

            if (poser.PrimaryPose.Type == BlendType.Immediate)
            {
                Hand.Pose(poser.PrimaryPose.Pose.GetPose(Hand.Side));
            }
        }

        public void SetPose(HVRHandPose handPose)
        {
            if (!Hand) return;
            BlendedPrimary = handPose.GetPose(Hand.IsLeft).DeepCopy();
            AnimatePose(5f);
        }

        public void AnimatePose(float animationTime = 0.1f)
        {
            if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
            _animationCoroutine = StartCoroutine(Animate(animationTime));
        }

        private IEnumerator Animate(float animationTime)
        {
            var elapsed = 0f;

            var start = Hand.CreateHandPose();

            if (animationTime < 1 / 60f)
            {
                AnimateHand(start, 1f);
                _animationCoroutine = null;
                yield break;
            }


            while (elapsed < animationTime)
            {
                var t = elapsed / animationTime;

                AnimateHand(start, t);

                yield return null;

                elapsed += Time.deltaTime;
                Debug.Log($"{Time.deltaTime}");
            }

            _animationCoroutine = null;
        }

        private void AnimateHand(HVRHandPoseData start, float t)
        {
            for (var i = 0; i < Hand.Fingers.Length; i++)
            {
                var finger = Hand.Fingers[i];
                var fingerStart = start.Fingers[i];
                var fingerTarget = BlendedPrimary.Fingers[i];
                AnimateFinger(finger, fingerStart, fingerTarget, t);
            }
        }

        private void AnimateFinger(HVRPosableFinger finger, HVRPosableFingerData start, HVRPosableFingerData target, float thing)
        {
            for (var i = 0; i < finger.Bones.Count; i++)
            {
                var bone = finger.Bones[i];
                var boneStart = start.Bones[i];
                var boneTarget = target.Bones[i];

                bone.Transform.localPosition = Vector3.Lerp(boneStart.Position, boneTarget.Position, thing);
                bone.Transform.localRotation = Quaternion.Lerp(boneStart.Rotation, boneTarget.Rotation, thing);
            }
        }

        //[Button("TestAnimation")]
        public void TestAnimation()
        {
            SetPose(CurrentPose);
        }


    }
}
