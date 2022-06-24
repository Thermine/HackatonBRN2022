using System;
using System.Collections.Generic;

namespace HurricaneVR.Framework.Core.Sockets
{
    public class HVREnumSocketFilter<TEnum> : HVRSocketFilter where TEnum : Enum
    {
        public TEnum SocketType;

        public override bool IsValid(HVRSocketable filter)
        {
            if (!filter) return false;
            var enumFilter = filter as HVREnumSocketable<TEnum>;
            if (enumFilter == null) return false;
            return EqualityComparer<TEnum>.Default.Equals(SocketType, enumFilter.SocketType);
        }
    }
}