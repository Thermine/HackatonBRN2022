using System.Collections.Generic;
using UnityEngine;

namespace HurricaneVR.Framework.Shared
{
    public static class HVRUtilities
    {
        public static void CreateConeRays(List<Vector3> rays, int layers, float largestRadius, int countPerLayer, Vector3 direction)
        {
            rays.Clear();

            for (var layer = 0; layer < layers; layer++)
            {
                var radius = largestRadius / (layer + 1);

                var angle = 0f;

                for (var subLayer = 0; subLayer < countPerLayer / (layer + 1); subLayer++)
                {
                    angle += 360f / (countPerLayer / (layer + 1f)) * Mathf.Deg2Rad;

                    var y = radius * Mathf.Sin(angle);
                    var x = radius * Mathf.Cos(angle);

                    var v = new Vector3(x, y, 1);

                    v = Quaternion.FromToRotation(Vector3.forward, direction) * v;

                    rays.Add(v);
                }
            }
        }
    }

    
}