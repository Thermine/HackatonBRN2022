﻿using UnityEngine;
using UnityEngine.Serialization;

namespace HurricaneVR.Framework.Shared
{
    [CreateAssetMenu(menuName = "HurricaneVR/Recoil Settings", fileName = "RecoilSettings")]
    public class HVRRecoilSettings : ScriptableObject
    {
        [FormerlySerializedAs("Torque")]
        [Header("Recoil Forces")]
        [Tooltip("Recoil applied about the X axis.")]
        public float UpForce = 1f;
        
        [Tooltip("Recoil applied at the BackRecoil transform position in the Z direction.")]
        public float BackwardsForce = 1f;

        [FormerlySerializedAs("TwoHandTorque")] public float TwoHandUpForce;
        public float TwoHandBackwardsForce;
        public bool UseTwoHandRecoilForce;

        public bool ImpulseForce;

        [Header("Side To Side Recoil")]
        public bool RandomSideToSideRecoil = false;

        public float SideToSideMin;
        public float SideToSideMax;

        

        public float TwoHandSideToSideMin;
        public float TwoHandSideToSideMax;
        

        [Header("Limits")]
        public bool LimitRecoilForce = true;
        [Tooltip("Maximum constant force applied to the up recoil.")]
        public float MaxUpForce = 10f;
        [Tooltip("Maximum constant force applied to the back recoil")]
        public float MaxBackForce = 10f;

        [Tooltip("Maximum constant torque applied for side to side recoil")]
        public float MaxSideForce = 10f;

        [Header("Recovery")]
        public float RecoveryDelay = .2f;
        public float TwoHandedRecoveryDelay = .1f;

        public float RecoveryTime = .1f;
        public float TwoHandedRecoveryTime = .05f;
    }
}