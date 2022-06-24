using System.Collections.Generic;
using System.Linq;
using HurricaneVR.Framework.Core.Grabbers;
using UnityEngine;

namespace HurricaneVR.Framework.Core.Sockets
{
    public class HVRSocketContainer : MonoBehaviour
    {
        [Tooltip("Adds all sockets found on or below this object.")]
        public bool AutoPopulate = true;

        [Tooltip("Sockets in this container")]
        public List<HVRSocket> Sockets = new List<HVRSocket>();

        void Start()
        {
            if (AutoPopulate)
                Sockets = GetComponentsInChildren<HVRSocket>().ToList();
        }

        void Update()
        {

        }

        public bool HasAvailableSocket()
        {
            for (var i = 0; i < Sockets.Count; i++)
            {
                var e = Sockets[i];
                if (!e.IsGrabbing) return true;
            }

            return false;
        }

        public bool HasAvailableSocket(HVRGrabbable grabbable)
        {
            return TryFindAvailableSocket(grabbable, out var socket);
        }

        public bool TryFindAvailableSocket(HVRGrabbable grabbable, out HVRSocket socket)
        {
            socket = Sockets.FirstOrDefault(e => !e.IsGrabbing && e.IsValid(grabbable));
            if (socket == null)
                return false;
            return true;
        }

        public bool TryAddGrabbable(HVRGrabbable grabbable)
        {
            if (TryFindAvailableSocket(grabbable, out var socket))
                return socket.TryGrab(grabbable);
            return false;
        }
    }
}
