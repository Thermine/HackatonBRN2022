using System.Collections.Generic;

namespace HurricaneVR.Framework.Core.Sockets
{
    public class HVRGrabbableSocketExcluder : HVRSocketFilter
    {
        public List<HVRGrabbable> Excluded = new List<HVRGrabbable>();
        private readonly HashSet<HVRGrabbable> _excludedSet = new HashSet<HVRGrabbable>();

        private void Start()
        {
            foreach (var g in Excluded)
            {
                _excludedSet.Add(g);
            }
        }

        public override bool IsValid(HVRSocketable filter)
        {
            if (!filter || !filter.Grabbable) return false;
            return !_excludedSet.Contains(filter.Grabbable);
        }
    }
}
