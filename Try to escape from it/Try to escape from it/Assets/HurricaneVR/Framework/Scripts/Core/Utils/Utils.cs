using System;
using System.Collections;
using System.Reflection;
using HurricaneVR.Framework.Shared;
using UnityEngine;

namespace HurricaneVR.Framework.Core.Utils
{
    public static class VRUtilities
    {


        public static void SetLayerRecursive(this Transform transform, HVRLayers layer, Transform except = null)
        {
            var newLayer = LayerMask.NameToLayer(layer.ToString());

            SetLayerRecursive(transform, newLayer, except);
        }

        public static void SetLayerRecursive(this Transform transform,  int newLayer, Transform except = null)
        {
            if (!transform || transform == except)
                return;

            transform.gameObject.layer = newLayer;
            for (int i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursive(transform.GetChild(i), newLayer, except);
            }
        }

        public static IEnumerator SetLayerTimeout(Transform transform, HVRLayers layer, float timeout)
        {
            yield return new WaitForSeconds(timeout);
            transform.SetLayerRecursive(layer);
        }

        public static Transform FindChildRecursive(this Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(name))
                    return child;

                var result = child.FindChildRecursive(name);
                if (result != null)
                    return result;
            }
            return null;
        }

        public static Rigidbody GetRigidbody(this MonoBehaviour b)
        {
            return b.GetComponent<Rigidbody>();
        }

        public static Rigidbody GetRigidbody(this GameObject b)
        {
            return b.GetComponent<Rigidbody>();
        }

        public static T AddCopyOf<T>(this GameObject go, T toCopy) where T : Component
        {
            var type = toCopy.GetType();
            return go.AddComponent(type).GetCopyOf(toCopy);
        }

        public static T GetCopyOf<T>(this Component newComponent, T toCopy) where T : Component
        {
            Type type = newComponent.GetType();

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;
            PropertyInfo[] pinfos = type.GetProperties(flags);
            foreach (var pinfo in pinfos)
            {
                if (pinfo.CanWrite)
                {
                    try
                    {
                        pinfo.SetValue(newComponent, pinfo.GetValue(toCopy, null), null);
                    }
                    catch { } // In case of NotImplementedException being thrown. For some reason specifying that exception didn't seem to catch it, so I didn't catch anything specific.
                }
            }
            FieldInfo[] finfos = type.GetFields(flags);
            foreach (var finfo in finfos)
            {
                finfo.SetValue(newComponent, finfo.GetValue(toCopy));
            }
            return newComponent as T;
        }
    }
}
