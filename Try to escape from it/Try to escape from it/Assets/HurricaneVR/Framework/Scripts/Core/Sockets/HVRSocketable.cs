using UnityEngine;

namespace HurricaneVR.Framework.Core.Sockets
{
    public class HVRSocketable : MonoBehaviour
    {
        public HVRGrabbable Grabbable { get; private set; }
        public Transform SocketOrientation;
        public float SocketScale = 1f;
        
        [Tooltip("If your grabbable model is not at 1,1,1 scale. ")]
        public Vector3 CounterScale = Vector3.one;

        public AudioClip SocketedClip;
        public AudioClip UnsocketedClip;


        private void Start()
        {
            Grabbable = GetComponent<HVRGrabbable>();
        }
    }
}