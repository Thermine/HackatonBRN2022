using System.Linq;
using UnityEngine;

namespace HurricaneVR.Framework.Core
{
    public class HVRHandPhysics : MonoBehaviour
    {
        public Transform PhysicsHand;
        public Collider[] HandColliders { get; private set; }

        // Start is called before the first frame update
        void Start()
        {
            HandColliders = PhysicsHand.gameObject.GetComponentsInChildren<Collider>().Where(e => !e.isTrigger).ToArray();
        }

        public void DisableCollision()
        {
            foreach (var handCollider in HandColliders)
            {
                handCollider.enabled = false;
            }
        }

        public void SetAllToTrigger()
        {
            foreach (var handCollider in HandColliders)
            {
                handCollider.isTrigger = true;
            }
        }

        public void ResetToNonTrigger()
        {
            foreach (var handCollider in HandColliders)
            {
                handCollider.isTrigger = false;
            }
        }

        public void EnableCollision()
        {
            foreach (var handCollider in HandColliders)
            {
                handCollider.enabled = true;
            }
        }
    }
}
