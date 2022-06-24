using System;
using HurricaneVR.Framework.Core.Utils;

namespace HurricaneVR.Framework.Core.Sockets
{
    public class HVREnumFlagsSocketFilter<TEnum> : HVRSocketFilter where TEnum : Enum
    {
        [EnumFlag]
        public TEnum SocketType;

        public override bool IsValid(HVRSocketable filter)
        {
            if (!filter) 
                return false;
            var enumFilter = filter as HVREnumFlagsSocketable<TEnum>;
            if (enumFilter == null) 
                return false;
            if ((int)(object)enumFilter.SocketType == 0)
                return false;
            return enumFilter.SocketType.HasFlag(SocketType);
            //return SocketType.HasFlag(enumFilter.SocketType);
        }
    }
}