using UnityEngine;

namespace HurricaneVR.Framework.Core.Utils
{
    public class AxisLines : MonoBehaviour
    {
        public bool DrawX = true;
        public bool DrawY = true;
        public bool DrawZ = true;

        void Start()
        {

        }

        void Update()
        {
            if (DrawZ)
                Debug.DrawLine(transform.position, transform.position + (transform.forward * 1), Color.blue);
            if (DrawX)
                Debug.DrawLine(transform.position, transform.position + (transform.right * 1), Color.red);
            if (DrawY)
                Debug.DrawLine(transform.position, transform.position + (transform.up * 1), Color.green);
        }
    }
}
