using System;

namespace HurricaneVR.Framework.Shared
{
    public enum HVRHoldType
    {
        OneHand, AllowSwap, TwoHanded, ManyHands
    }

    public enum HVRGrabType
    {
        Snap,
        PhysicPoser,
        Offset 
    }

    public enum HVRGrabTracking
    {
        ConfigurableJoint,
        FixedJoint,
        None
    }

    public enum HVRHandSide
    {
        Left, Right
    }

    public enum HVRSortMode
    {
        Distance, SquareMagnitude
    }

    public enum HVRButtonTrigger
    {
        Active, Toggle
    }

    //the order of these cannot change, they are used in serialization
    public enum HVRButtons
    {
        Grip,
        Trigger,
        Primary,
        PrimaryTouch,
        Secondary,
        SecondaryTouch,
        Menu,
        JoystickButton,
        TrackPadButton,
        JoystickTouch,
        TriggerTouch,
        ThumbTouch,
        TriggerNearTouch,
        ThumbNearTouch,
        TrackPadLeft,
        TrackPadRight,
        TrackPadUp,
        TrackPadDown
    }

    [Serializable]
    public struct HVRButtonState
    {
        public bool Active;
        public bool JustActivated;
        public bool JustDeactivated;
        public float Value;
    }

    public enum HVRLayers
    {
        Grabbable, Hand, LeftTarget, RightTarget, Player
    }

    public enum HVRAxis
    {
        X, Y, Z
    }

    public enum HVRXRInputFeatures
    {
        None = 0,
        MenuButton,
        Trigger,
        Grip,
        TriggerPressed,
        GripPressed,
        PrimaryButton,
        PrimaryTouch,
        SecondaryButton,
        SecondaryTouch,
        Primary2DAxisTouch,
        Primary2DAxisClick,
        Secondary2DAxisTouch,
        Secondary2DAxisClick,
        PrimaryAxis2DUp,
        PrimaryAxis2DDown,
        PrimaryAxis2DLeft,
        PrimaryAxis2DRight,
        SecondaryAxis2DUp,
        SecondaryAxis2DDown,
        SecondaryAxis2DLeft,
        SecondaryAxis2DRight
    };
}
