using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using HurricaneVR.Framework.Core;
using UnityEngine;

namespace HurricaneVR.Samples
{
    public class Backpack : MonoBehaviour
    {
        [Tooltip("Used to ignore collision with grabbable colliders.")]
        public List<Collider> Colliders = new List<Collider>();

        void Start()
        {
            if (Colliders.Count > 0)
                StartCoroutine(IgnoreColliders());
        }

        public IEnumerator IgnoreColliders()
        {
            yield return null;

            //this will only ignore grabbables at the start of the game.
            //you would need to ignore the collision yourself if you instantiate grabbables after.

            var watch = Stopwatch.StartNew();
            var grabbables = FindObjectsOfType<HVRGrabbable>();

            foreach (var grabbable in grabbables)
            {
                IgnoreCollision(grabbable);
            }
            watch.Stop();
            //Debug.Log($"Backpack colliders ignore took : {watch.ElapsedMilliseconds} ms.");
        }

        public void IgnoreCollision(HVRGrabbable grabbable)
        {
            foreach (var c in grabbable.Colliders)
            {
                foreach (var ourCollider in Colliders)
                {
                    if (!c)
                        continue;
                    Physics.IgnoreCollision(c, ourCollider);
                }
            }
        }
    }
}
