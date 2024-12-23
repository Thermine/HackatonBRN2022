﻿using System;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Shared.HandPoser;
using UnityEngine.Events;

namespace HurricaneVR.Framework.Core
{
    [Serializable]
    public class VRGrabberEvent : UnityEvent<HVRGrabberBase, HVRGrabbable> { }

    [Serializable]
    public class VRGrabbableEvent : UnityEvent<HVRGrabbable> { }

    [Serializable]
    public class VRHandPoseEvent : UnityEvent<HVRHandPoser> { }

    [Serializable]
    public class DialSteppedEvent : UnityEvent<int> { }

    [Serializable]
    public class DialTurnedEvent : UnityEvent<float, float, float> { }

    [Serializable]
    public class LeverMovedEvent : UnityEvent<float, float, float>
    {

    }

    [Serializable]
    public class LeverSteppedEvent : UnityEvent<int>
    {

    }

}
