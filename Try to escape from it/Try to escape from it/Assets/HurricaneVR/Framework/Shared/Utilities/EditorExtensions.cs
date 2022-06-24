#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HurricaneVR.Framework.Shared.Utilities
{
    public static class EditorExtensions
    {

        public static string GetName(this ScriptableObject so)
        {
            var path = AssetDatabase.GetAssetPath(so.GetInstanceID());
            return Path.GetFileNameWithoutExtension(path);
        }

        public static void SetExpandedRecursive(this GameObject go, bool expand)
        {
            try
            {
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                var methodInfo = type.GetMethod("SetExpandedRecursive");

                EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                var window = EditorWindow.focusedWindow;

                methodInfo.Invoke(window, new object[] { go.GetInstanceID(), expand });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

        }
    }
}

#endif