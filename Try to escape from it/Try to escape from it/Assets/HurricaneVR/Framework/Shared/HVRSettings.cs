using System;
using System.Collections.Generic;
using System.IO;
using HurricaneVR.Framework.Shared.HandPoser;
using HurricaneVR.Framework.Shared.HandPoser.Data;
using HurricaneVR.Framework.Shared.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HurricaneVR.Framework.Shared
{
    public class HVRSettings : ScriptableObject
    {
        private static HVRSettings _instance;

        public static HVRSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<HVRSettings>(HandPoserSettings);

                    //Asset doesn't exist, create it
                    if (_instance == null)
                    {
                        _instance = HVRSettings.CreateInstance<HVRSettings>();

#if UNITY_EDITOR
                        _instance.Setup(_instance);
                        _instance.AddAssetToResource(_instance, HandPoserSettingsFileName);
#endif
                    }
                }

                return _instance;
            }
        }

        public string RootDirectory;
        public string ResourcesDirectory;
        public string ReferencePoseDirectory;
        public string RuntimePosesDirectory;

        public string LocalEditorRootDirectory;
        public string LocalRootDirectory;
        public string LocalResourcesDirectory;
        public string LocalReferencePoseDirectory;
        public string LocalRuntimePosesDirectory;


        public string PosesDirectory;
        public string LocalPosesDirectory;

        public const string HandPoserSettings = "HVRSettings";
        public const string HandPoserSettingsFileName = HandPoserSettings + ".asset";

        public const string DefaultOculusOpen = "OculusCustomHandOpen";

        public const string OculusCustomHandLeft = "HVR_CustomHandLeft";
        public const string OculusCustomHandRight = "HVR_CustomHandRight";

        public GameObject LeftHand;
        public GameObject RightHand;

        public HVRHandPose OpenHandPose;

        public List<HVRHandPose> ReferencePoses = new List<HVRHandPose>();

        public List<string> AnimationParameters = new List<string>();

        [Tooltip("If true we will use the new scriptable object joint settings for configurable joints on grabbables")]
        public bool IgnoreLegacyGrabbableSettings = true;

        [Tooltip("Default joint settings when grabbing an object.")]
        public HVRJointSettings DefaultJointSettings;

        public GameObject GetPoserHand(HVRHandSide side)
        {
            if (side == HVRHandSide.Left) return LeftHand;
            return RightHand;
        }
#if UNITY_EDITOR

        private void Setup(HVRSettings settings)
        {
            try
            {
                TryFindRoot();
                TryFindResources();

                if (!DefaultJointSettings)
                {
                    DefaultJointSettings = FindJointSettings("HVR_GrabbableSettings");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            TryCreateReferencePoseFolder();
            TryCreateRuntimePoseFolder();

            SetupDefaultHands(settings);
        }

        private static void SetupDefaultHands(HVRSettings settings)
        {
            try
            {
                if (settings.LeftHand == null)
                {
                    settings.LeftHand = FindPrefab(OculusCustomHandLeft);
                }

                if (settings.RightHand == null)
                {
                    settings.RightHand = FindPrefab(OculusCustomHandRight);
                }

                if (settings.OpenHandPose == null)
                {
                    settings.OpenHandPose = FindAsset<HVRHandPose>(DefaultOculusOpen);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void TryFindRoot()
        {
            try
            {

                var root = ScriptableObject.CreateInstance<HVRRootFinder>();
                var rootScript = UnityEditor.MonoScript.FromScriptableObject(root);
                var rootPath = UnityEditor.AssetDatabase.GetAssetPath(rootScript);
                var rootFileInfo = new FileInfo(rootPath);

                var path = rootFileInfo.Directory.FullName.Replace("Shared", "");

                RootDirectory = path;

                //Debug.Log($"RootPath={RootDirectory}");

                System.IO.DirectoryInfo assetsDirectoryInfo = new DirectoryInfo(Application.dataPath);
                LocalRootDirectory = RootDirectory.Substring(assetsDirectoryInfo.Parent.FullName.Length + 1);
                LocalEditorRootDirectory = LocalRootDirectory + "Editor" + Path.DirectorySeparatorChar;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void TryFindResources()
        {
            try
            {
                var path = Path.Combine(RootDirectory, "Resources");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                ResourcesDirectory = path + Path.DirectorySeparatorChar;
                LocalResourcesDirectory = LocalRootDirectory + "Resources" + Path.DirectorySeparatorChar;
                //Debug.Log($"HVRSettings.ResourcesPath={ResourcesDirectory}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }


        private void TryCreateReferencePoseFolder()
        {
            try
            {
                if (string.IsNullOrEmpty(LocalResourcesDirectory)) return;

                if (string.IsNullOrEmpty(ReferencePoseDirectory))
                {
                    LocalReferencePoseDirectory = LocalResourcesDirectory + "ReferencePoses" + Path.DirectorySeparatorChar;
                    ReferencePoseDirectory = Path.Combine(ResourcesDirectory, "ReferencePoses") + Path.DirectorySeparatorChar;
                    Directory.CreateDirectory(ReferencePoseDirectory);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void TryCreateRuntimePoseFolder()
        {
            try
            {
                if (string.IsNullOrEmpty(LocalResourcesDirectory)) return;

                if (string.IsNullOrEmpty(LocalRuntimePosesDirectory))
                {
                    LocalRuntimePosesDirectory = LocalResourcesDirectory + "RuntimePoses" + Path.DirectorySeparatorChar;
                }


                if (string.IsNullOrEmpty(RuntimePosesDirectory))
                {
                    RuntimePosesDirectory = Path.Combine(ResourcesDirectory, "RuntimePoses") + Path.DirectorySeparatorChar;
                }

                Directory.CreateDirectory(RuntimePosesDirectory);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [InspectorButton("ShowPosesFolderChooser")]
        public string ChosePosesDirectory = "Choose Pose Directory";

        [InspectorButton("ShowReferencePosesFolderChooser", 300)]
        public string ChoseReferencePosesDirectory = "Choose Reference Poses Directory";

        [InspectorButton("ShowRuntimePosesFolderChooser", 300)]
        public string ChoseRunTimePosesDirectory = "Choose RunTime Poses Directory";

        public void ShowPosesFolderChooser()
        {
            PosesDirectory = EditorUtility.OpenFolderPanel("Choose Pose Directory", null, null);
            LocalPosesDirectory = PosesDirectory.Substring(PosesDirectory.IndexOf("Assets"));
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void ShowRuntimePosesFolderChooser()
        {
            RuntimePosesDirectory = EditorUtility.OpenFolderPanel("Choose Pose Directory", null, null);
            LocalRuntimePosesDirectory = RuntimePosesDirectory.Substring(PosesDirectory.IndexOf("Assets"));
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void ShowReferencePosesFolderChooser()
        {
            ReferencePoseDirectory = EditorUtility.OpenFolderPanel("Choose Pose Directory", null, null);
            LocalReferencePoseDirectory = ReferencePoseDirectory.Substring(PosesDirectory.IndexOf("Assets"));
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [InspectorButton("ReloadGlobalsMethod")]
        public string ReloadGlobals = "Reload Globals";
        public void ReloadGlobalsMethod()
        {
            if (Instance)
            {
                Setup(Instance);
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }


        public void SaveRunTimePose(HVRHandPose pose, string fileName, string directory = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    var folder = Path.Combine(RuntimePosesDirectory, directory);
                    Directory.CreateDirectory(folder);
                }

                if (!fileName.EndsWith(".asset"))
                {
                    fileName += ".asset";
                }

                string path;
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    path = Path.Combine(Path.Combine(LocalRuntimePosesDirectory, directory), fileName);
                }
                else
                {
                    path = Path.Combine(LocalRuntimePosesDirectory, fileName);
                }

                pose = AssetUtils.CreateOrReplaceAsset(pose, path);
                Debug.Log($"Saved {fileName} to {LocalRuntimePosesDirectory}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

        }

        public HVRHandPose SavePoseToDefault(HVRHandPose pose, string fileName, string directory = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(PosesDirectory) || string.IsNullOrEmpty(LocalPosesDirectory))
                {
                    //Debug.Log($"Setup PosesDirectory and LocalPosesDirectory.");
                    SaveRunTimePose(pose, fileName, null);
                }

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    var folder = Path.Combine(PosesDirectory, directory);
                    Directory.CreateDirectory(folder);
                }

                if (!fileName.EndsWith(".asset"))
                {
                    fileName += ".asset";
                }

                string path;
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    path = Path.Combine(Path.Combine(LocalPosesDirectory, directory), fileName);
                }
                else
                {
                    path = Path.Combine(LocalPosesDirectory, fileName);
                }

                return  AssetUtils.CreateOrReplaceAsset(pose, path);
                //Debug.Log($"Saved {fileName} to {LocalPosesDirectory}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return null;
        }

        public void SaveReferencePose(HVRHandPose pose, string name)
        {
            try
            {
                if (!name.EndsWith(".asset"))
                {
                    name += ".asset";
                }

                var path = Path.Combine(LocalReferencePoseDirectory, name);
                AssetUtils.CreateOrReplaceAsset(pose, path);
                Debug.Log($"Saved {name} to {LocalReferencePoseDirectory}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void AddAssetToResource<T>(T asset, string name) where T : Object
        {
            try
            {
                if (!name.EndsWith(".asset"))
                {
                    name += ".asset";
                }

                var path = Path.Combine(LocalResourcesDirectory, name);
                AssetUtils.CreateOrReplaceAsset<T>(asset, path);

                Debug.Log($"Saved {name} to {LocalResourcesDirectory}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static TAssetType FindAsset<TAssetType>(string name) where TAssetType : UnityEngine.Object
        {

            string[] defaultPaths = UnityEditor.AssetDatabase.FindAssets(name);
            if (defaultPaths != null && defaultPaths.Length > 0)
            {
                string defaultGUID = defaultPaths[0];
                string defaultPath = UnityEditor.AssetDatabase.GUIDToAssetPath(defaultGUID);
                var defaultAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TAssetType>(defaultPath);

                if (defaultAsset == null)
                    Debug.LogError($"Unable to find asset of {name}. Found path: " + defaultPath);

                return defaultAsset;
            }

            return null;
        }

        public static HVRJointSettings FindJointSettings(string name)
        {
            return FindAsset<HVRJointSettings>($"t:hvrjointsettings {name}");
        }

        public static GameObject FindPrefab(string name)
        {
            return FindAsset<GameObject>(string.Format("t:Prefab {0}", name));
        }
#endif
        public void OnValidate()
        {

        }
    }
}
