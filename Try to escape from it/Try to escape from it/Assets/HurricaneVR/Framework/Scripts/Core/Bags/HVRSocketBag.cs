using System;
using System.Collections.Generic;
using System.Linq;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Shared;
using UnityEngine;

namespace HurricaneVR.Framework.Core.Bags
{
    public class HVRSocketBag : MonoBehaviour
    {
        private readonly Dictionary<HVRSocket, HashSet<Collider>> _map = new Dictionary<HVRSocket, HashSet<Collider>>();

        void Start()
        {
            if (!Grabber)
                Grabber = GetComponentInParent<HVRHandGrabber>();

            if (Math.Abs(MaxDistanceAllowed) < .001)
            {
                var sphereCollider = GetComponent<SphereCollider>();
                if (sphereCollider)
                {
                    MaxDistanceAllowed = sphereCollider.radius;
                }

                MaxDistanceAllowed = 1.5f;
            }
        }

        public HVRSortMode hvrSortMode;

        private readonly HashSet<HVRSocket> AllSockets = new HashSet<HVRSocket>();
        public List<HVRSocket> ValidSockets = new List<HVRSocket>(1000);
        private readonly List<HVRSocket> SocketsToRemove = new List<HVRSocket>(1000);
        private Dictionary<HVRSocket, float> DistanceMap = new Dictionary<HVRSocket, float>();

        public HVRSocket ClosestSocket;
        public HVRHandGrabber Grabber;

        public float MaxDistanceAllowed;

        private Collider[] _colliders = new Collider[1000];

        private void FixedUpdate()
        {
            Calculate();
        }

        protected void AddSocket(HVRSocket socket)
        {
            if (AllSockets.Contains(socket))
            {
                return;
            }
            AllSockets.Add(socket);
        }


        protected void RemoveSocket(HVRSocket socket)
        {
            AllSockets.Remove(socket);
            if (_map.ContainsKey(socket))
            {
                _map.Remove(socket);
            }
        }

        protected void Calculate()
        {
            ValidSockets.Clear();
            SocketsToRemove.Clear();

            foreach (var socket in AllSockets)
            {
                if (hvrSortMode == HVRSortMode.Distance)
                {
                    DistanceMap[socket] = Vector3.Distance(socket.transform.position, Grabber.transform.position);
                }
                else if (hvrSortMode == HVRSortMode.SquareMagnitude)
                {
                    DistanceMap[socket] = (socket.transform.position - Grabber.transform.position).sqrMagnitude;
                }

                if (DistanceMap[socket] > MaxDistanceAllowed)
                {
                    SocketsToRemove.Add(socket);
                }
                else if (IsValid(socket))
                {
                    ValidSockets.Add(socket);
                }
            }

            for (var index = 0; index < SocketsToRemove.Count; index++)
            {
                var invalid = SocketsToRemove[index];
                RemoveSocket(invalid);
            }

            // x->y ascending sort
            ValidSockets.Sort((x, y) => DistanceMap[x].CompareTo(DistanceMap[y]));

            ClosestSocket = ValidSockets.FirstOrDefault();
        }

        protected bool IsValid(HVRSocket Socket)
        {
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var Socket = other.GetComponent<HVRSocket>();

            if (Socket)
            {
                if (!_map.TryGetValue(Socket, out var colliders))
                {
                    colliders = new HashSet<Collider>();
                    _map[Socket] = colliders;
                }

                if (colliders.Count == 0)
                {
                    AddSocket(Socket);
                }

                colliders.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var Socket = other.GetComponent<HVRSocket>();

            if (Socket)
            {
                if (_map.TryGetValue(Socket, out var colliders))
                {
                    colliders.Remove(other);
                }

                if (colliders == null || colliders.Count == 0)
                {
                    RemoveSocket(Socket);
                }
            }
        }

        //private void OnTriggerStay(Collider other)
        //{
        //    var Socket = other.GetComponent<VRSocket>();

        //    if (Socket)
        //    {
        //        if (!_map.TryGetValue(Socket, out var colliders))
        //        {
        //            colliders = new HashSet<Collider>();
        //            _map[Socket] = colliders;
        //        }

        //        if (colliders.Count == 0)
        //        {
        //            AddSocket(Socket);
        //        }

        //        colliders.Add(other);
        //    }
        //}
    }
}