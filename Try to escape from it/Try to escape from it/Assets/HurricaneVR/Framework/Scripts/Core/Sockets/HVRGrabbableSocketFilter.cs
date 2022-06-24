using System.Collections.Generic;

namespace HurricaneVR.Framework.Core.Sockets
{
    public class HVRGrabbableSocketFilter : HVRSocketFilter
    {
        public List<HVRGrabbable> ValidGrabbables = new List<HVRGrabbable>();

        public override bool IsValid(HVRSocketable filter)
        {
            return ValidGrabbables.Contains(filter.Grabbable);
        }
    }
}