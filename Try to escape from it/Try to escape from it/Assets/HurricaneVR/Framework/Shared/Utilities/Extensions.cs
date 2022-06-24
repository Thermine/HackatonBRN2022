using UnityEngine;

namespace HurricaneVR.Framework.Shared.Utilities
{
    public static class Extensions
    {

        public static Bounds GetRendererBounds(this Transform transform, bool requireEnabled = true)
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            var renderers = transform.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (!requireEnabled || r.enabled)
                    bounds.Encapsulate(r.bounds);
            }

            return bounds;
        }
    }
}

