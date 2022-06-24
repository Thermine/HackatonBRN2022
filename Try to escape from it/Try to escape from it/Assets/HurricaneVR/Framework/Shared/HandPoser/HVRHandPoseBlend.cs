using System;
using UnityEngine;

namespace HurricaneVR.Framework.Shared.HandPoser
{
    [Serializable]
    public class HVRHandPoseBlend
    {
        public const string DefaultParameter = "None";

        public HVRHandPose Pose;
        [Range(0, 1)] public float Weight = 1f;
        public HVRHandPoseMask Mask = HVRHandPoseMask.None;
        public bool Disabled;
        public BlendType Type = BlendType.Immediate;
        public float Speed = 16;
        public string AnimationParameter;
        public bool ButtonParameter;
        public HVRButtons Button;
        

        [NonSerialized]
        public float Value;

        //[NonSerialized]
        //public float ElapsedTime;

        public HVRHandPoseBlend()
        {
            if (AnimationParameter == null || string.IsNullOrWhiteSpace(AnimationParameter))
            {
                AnimationParameter = DefaultParameter;
            }
        }

        public void SetDefaults()
        {
            Speed = 16f;
            AnimationParameter = DefaultParameter;
            Weight = 1f;
            Mask = HVRHandPoseMask.None;
            Type = BlendType.Immediate;
            ButtonParameter = false;
        }
    }

    public enum BlendType
    {
        Immediate, Manual, FloatParameter, BooleanParameter
    }
}