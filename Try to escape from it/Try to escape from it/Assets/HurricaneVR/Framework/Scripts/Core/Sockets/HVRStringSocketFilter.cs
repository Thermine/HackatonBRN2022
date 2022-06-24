namespace HurricaneVR.Framework.Core.Sockets
{
    public class HVRStringSocketFilter : HVRSocketFilter
    {
        public string SocketType;

        public override bool IsValid(HVRSocketable filter)
        {
            if (string.IsNullOrWhiteSpace(SocketType)) return false;
            if (!filter) return false;
            var stringFilter = filter as HVRStringSocketable;
            if (stringFilter == null) return false;
            if (string.IsNullOrWhiteSpace(stringFilter.SocketType)) return false;
            return SocketType.ToLowerInvariant().Equals(stringFilter.SocketType.ToLowerInvariant());
        }
    }
}