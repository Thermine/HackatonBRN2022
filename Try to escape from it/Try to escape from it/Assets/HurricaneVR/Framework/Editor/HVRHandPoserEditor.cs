using System;
using System.Collections.Generic;
using HurricaneVR.Framework.Shared;
using HurricaneVR.Framework.Shared.HandPoser;
using HurricaneVR.Framework.Shared.HandPoser.Data;
using HurricaneVR.Framework.Shared.Utilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HurricaneVR.Editor
{
    [CustomEditor(typeof(HVRHandPoser))]
    public class HVRHandPoserEditor : UnityEditor.Editor
    {
        private SerializedProperty SP_LeftHandPreview;
        private SerializedProperty SP_RightHandPreview;

        private SerializedProperty SP_PrimaryPose;
        private SerializedProperty SP_Blends;


        private SerializedProperty SP_PreviewLeft;
        private SerializedProperty SP_PreviewRight;
        private SerializedProperty SP_PoseNames;
        private SerializedProperty SP_SelectionIndex;

        private VisualElement _root;
        private VisualTreeAsset _tree;
        private HVRHandPoser Poser;

        private int _previousIndex;

        private IntegerField _selectionIndexField;
        private string _leftInstanceId;
        private string _rightInstanceId;

        public ObjectField SelectedPoseField { get; set; }

        public ListView PosesListView { get; set; }
        public Toggle PreviewRight { get; set; }

        public Toggle PreviewLeft { get; set; }

        protected int SelectedIndex
        {
            get => SP_SelectionIndex?.intValue ?? 0;
            set
            {
                if (SP_SelectionIndex != null) SP_SelectionIndex.intValue = value;
                serializedObject.ApplyModifiedProperties();
            }
        }

        protected GameObject LeftHandPreview
        {
            get
            {
                if (SP_LeftHandPreview == null || SP_LeftHandPreview.objectReferenceValue == null) return null;
                return SP_LeftHandPreview.objectReferenceValue as GameObject;
            }
            set
            {
                if (SP_LeftHandPreview != null) SP_LeftHandPreview.objectReferenceValue = value;
            }
        }

        protected GameObject RightHandPreview
        {
            get
            {
                if (SP_RightHandPreview == null || SP_RightHandPreview.objectReferenceValue == null) return null;
                return SP_RightHandPreview.objectReferenceValue as GameObject;
            }
            set
            {
                if (SP_RightHandPreview != null) SP_RightHandPreview.objectReferenceValue = value;
            }
        }

        private HVRHandPoseBlend PrimaryPose
        {
            get
            {
                return Poser.PrimaryPose;
            }
            set
            {
                Poser.PrimaryPose = value;
                serializedObject.ApplyModifiedProperties();
            }
        }

        public HVRHandPose SelectedPose
        {
            get
            {
                return SelectedBlendPose?.Pose;
            }
            set
            {
                if (SelectedBlendPose == null) return;
                SelectedBlendPose.Pose = value;
            }
        }

        public HVRHandPoseBlend SelectedBlendPose
        {
            get
            {
                if (CurrentPoseIndex <= PrimaryIndex) return Poser.PrimaryPose;
                return Poser.Blends[BlendIndex];
            }
        }

        public SerializedProperty SerializedSelectedPose
        {
            get
            {
                if (CurrentPoseIndex <= PrimaryIndex) return SP_PrimaryPose;
                return SP_Blends.GetArrayElementAtIndex(BlendIndex);
            }
        }

        public int PrimaryIndex => 0;

        public int CurrentPoseIndex
        {
            get => PosesListView?.selectedIndex ?? 0;
        }

        public int BlendIndex
        {
            get => CurrentPoseIndex - 1 - PrimaryIndex;
        }

        private void OnEnable()
        {
            Poser = target as HVRHandPoser;

            _leftInstanceId = "LeftPreview_" + target.GetInstanceID();
            _rightInstanceId = "RightPreview_" + target.GetInstanceID();

            SP_LeftHandPreview = serializedObject.FindProperty("LeftHandPreview");
            SP_RightHandPreview = serializedObject.FindProperty("RightHandPreview");

            SP_PrimaryPose = serializedObject.FindProperty("PrimaryPose");

            SP_Blends = serializedObject.FindProperty("Blends");

            SP_PreviewLeft = serializedObject.FindProperty("PreviewLeft");
            SP_PreviewRight = serializedObject.FindProperty("PreviewRight");

            SP_SelectionIndex = serializedObject.FindProperty("SelectionIndex");
            SP_PoseNames = serializedObject.FindProperty("PoseNames");


            if (PrimaryPose == null)
            {
                PrimaryPose = new HVRHandPoseBlend();
                PrimaryPose.SetDefaults();
                serializedObject.ApplyModifiedProperties();
            }

            if (PrimaryPose.Pose == null && HVRSettings.Instance.OpenHandPose)
            {
                var poseProperty = SP_PrimaryPose.FindPropertyRelative("Pose");
                var clone = poseProperty.objectReferenceValue = HVRSettings.Instance.OpenHandPose.DeepCopy();
                clone.name = "Unsaved!";
                serializedObject.ApplyModifiedProperties();
            }

            CheckBadParameters();

            _root = new VisualElement();
            _tree = UnityEngine.Resources.Load<VisualTreeAsset>("HVRHandPoserEditor");
        }

        private void CheckBadParameters()
        {
            if (PrimaryPose.AnimationParameter == null || !HVRSettings.Instance.AnimationParameters.Contains(PrimaryPose.AnimationParameter))
            {
                SP_PrimaryPose.FindPropertyRelative("AnimationParameter").stringValue = HVRHandPoseBlend.DefaultParameter;
            }

            for (var i = 0; i < SP_Blends.arraySize; i++)
            {
                var paramProperty = SP_Blends.GetArrayElementAtIndex(i).FindPropertyRelative("AnimationParameter");

                if (!HVRSettings.Instance.AnimationParameters.Contains(paramProperty.stringValue))
                {
                    paramProperty.stringValue = HVRHandPoseBlend.DefaultParameter;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        public override VisualElement CreateInspectorGUI()
        {

            _root.Clear();
            _tree.CloneTree(_root);

            blendEditorRoot = _root.Q<BindableElement>("BlendEditorRoot");
            blendEditorRoot.Q<ObjectField>("Pose").objectType = typeof(HVRHandPose);
            var paramContainer = blendEditorRoot.Q<VisualElement>("ParameterContainer");

            var parameterChoices = new List<string> { HVRHandPoseBlend.DefaultParameter };
            parameterChoices.AddRange(HVRSettings.Instance.AnimationParameters);

            var paramField = new PopupField<string>(parameterChoices, HVRHandPoseBlend.DefaultParameter);
            paramField.bindingPath = "AnimationParameter";
            paramContainer.Add(paramField);

            _root.Add(blendEditorRoot);

            SetupAddButton();
            SetupDeleteButton();
            SetupSelectedPose(blendEditorRoot);
            SetupPosesListView();
            SetupNewButton();
            SetupSaveAsButton();
            SetupSaveButton();
            SetupMirrorButtons();
            SetupHandButtons();
            //BindBlendContainer();

            _selectionIndexField = new IntegerField("SelectedIndex");
            _selectionIndexField.bindingPath = "SelectionIndex";
            _selectionIndexField.RegisterValueChangedCallback(evt =>
            {
                if (PosesListView.selectedIndex != evt.newValue) PosesListView.selectedIndex = evt.newValue;
            });
            _root.Add(_selectionIndexField);

            PreviewLeft = _root.Q<Toggle>("PreviewLeft");
            PreviewLeft.BindProperty(SP_PreviewLeft);
            PreviewRight = _root.Q<Toggle>("PreviewRight");
            PreviewRight.BindProperty(SP_PreviewRight);

            PreviewLeft.RegisterValueChangedCallback(OnPreviewLeftChanged);
            PreviewRight.RegisterValueChangedCallback(OnPreviewRightChanged);

            _previousIndex = SelectedIndex;

            if (SelectedIndex >= PosesListView.itemsSource.Count + PrimaryIndex)
            {
                Debug.Log($"Stored SelectedIndex is higher than pose count.");
                SelectedIndex = PosesListView.itemsSource.Count - PrimaryIndex - 1;
                serializedObject.ApplyModifiedProperties();
            }

            PosesListView.selectedIndex = SelectedIndex;

            SP_PreviewLeft.boolValue = SP_LeftHandPreview.objectReferenceValue != null;
            SP_PreviewRight.boolValue = SP_RightHandPreview.objectReferenceValue != null;

            if (!SP_PreviewLeft.boolValue)
            {
                FindPreview(true, out var left);
                if (left)
                {
                    SP_PreviewLeft.boolValue = true;
                    SP_LeftHandPreview.objectReferenceValue = left;
                }
            }

            if (!SP_PreviewRight.boolValue)
            {
                FindPreview(false, out var right);
                if (right)
                {
                    SP_PreviewRight.boolValue = true;
                    SP_RightHandPreview.objectReferenceValue = right;
                }
            }

            serializedObject.ApplyModifiedProperties();

            UpdatePreview(false, SP_PreviewRight.boolValue, null);
            UpdatePreview(true, SP_PreviewLeft.boolValue, null);

            return _root;
        }

        private void BindBlendContainer()
        {
            //for some reason binding to a custom serialized class doesn't update the binding paths

            var previousPath = blendEditorRoot.bindingPath;

            blendEditorRoot.Unbind();
            blendEditorRoot.bindingPath = SerializedSelectedPose.propertyPath;

            var newPath = SerializedSelectedPose.propertyPath;

            if (previousPath != null && previousPath != newPath)
            {
                //Debug.Log($"{previousPath} to {newPath}");
                FixPath(previousPath, newPath, blendEditorRoot);
            }

            blendEditorRoot.Bind(SerializedSelectedPose.serializedObject);
        }

        private void FixPath(string previous, string newPath, VisualElement element)
        {
            var bindable = element as IBindable;
            if (bindable != null)
            {
                if (bindable.bindingPath != null && bindable.bindingPath.StartsWith(previous))
                {
                    bindable.bindingPath = bindable.bindingPath.Replace(previous, newPath);
                }
            }

            VisualElement.Hierarchy hierarchy = element.hierarchy;
            int childCount = hierarchy.childCount;
            for (int index = 0; index < childCount; ++index)
            {
                hierarchy = element.hierarchy;
                FixPath(previous, newPath, hierarchy[index]);
            }
        }


        private void SetupDeleteButton()
        {
            var deleteButton = _root.Q<Button>("DeleteBlendPose");

            deleteButton.clickable.clicked += () =>
            {
                if (PosesListView.selectedIndex == PrimaryIndex)
                {
                    Debug.Log($"Cannot remove primary pose.");
                    return;
                }

                if (PosesListView.selectedIndex <= 0) return;
                //deleting an item with a reference doesn't actually delete it, it sets it to null
                //if (SP_Blends.GetArrayElementAtIndex(PosesListView.selectedIndex - 1).objectReferenceValue != null)
                //{
                //    SP_Blends.DeleteArrayElementAtIndex(PosesListView.selectedIndex - 1);
                //}

                var isLast = SP_Blends.arraySize == PosesListView.selectedIndex;

                SP_Blends.DeleteArrayElementAtIndex(PosesListView.selectedIndex - 1);
                serializedObject.ApplyModifiedProperties();

                if (isLast)
                {
                    PosesListView.selectedIndex--;
                }

                PopulatePoses();
            };
        }


        private void SetupNewButton()
        {
            var button = _root.Q<Button>("NewPose");

            button.clickable.clicked += () =>
            {
                var folder = HVRSettings.Instance.PosesDirectory;
                string path;

                if (string.IsNullOrWhiteSpace(folder))
                {
                    path = EditorUtility.SaveFilePanelInProject("Save New Pose", "pose", "asset", "Message");
                }
                else
                {
                    path = EditorUtility.SaveFilePanelInProject("Save New Pose", "pose", "asset", "Message", folder);
                }

                if (!string.IsNullOrEmpty(path))
                {
                    HVRHandPose pose;

                    if (LeftHandPreview && !RightHandPreview)
                    {
                        var hand = LeftHandPreview.GetComponent<HVRPosableHand>();
                        pose = hand.CreateFullHandPose(Poser.MirrorAxis);
                    }
                    else if (RightHandPreview && !LeftHandPreview)
                    {
                        var hand = RightHandPreview.GetComponent<HVRPosableHand>();
                        pose = hand.CreateFullHandPose(Poser.MirrorAxis);
                    }
                    else if (RightHandPreview && LeftHandPreview)
                    {
                        pose = CreateInstance<HVRHandPose>();
                        var lefthand = LeftHandPreview.GetComponent<HVRPosableHand>();
                        var righthand = RightHandPreview.GetComponent<HVRPosableHand>();
                        pose.LeftHand = lefthand.CreateHandPose();
                        pose.RightHand = righthand.CreateHandPose();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error!", "Preview hands required.", "Ok!");
                        return;
                    }

                    SerializedSelectedPose.FindPropertyRelative("Pose").objectReferenceValue = AssetUtils.CreateOrReplaceAsset(pose, path);

                    SelectedPose = pose;
                    serializedObject.ApplyModifiedProperties();
                }
            };
        }

        private void SetupSaveAsButton()
        {
            var button = _root.Q<Button>("SaveAsPose");

            button.clickable.clicked += () =>
            {
                var folder = HVRSettings.Instance.PosesDirectory;
                string path;

                if (string.IsNullOrWhiteSpace(folder))
                {
                    path = EditorUtility.SaveFilePanelInProject("Save New Pose", "pose", "asset", "Message");
                }
                else
                {
                    path = EditorUtility.SaveFilePanelInProject("Save New Pose", "pose", "asset", "Message", folder);
                }

                if (!string.IsNullOrEmpty(path))
                {
                    var left = SelectedPose.LeftHand;
                    var right = SelectedPose.RightHand;

                    if (LeftHandPreview)
                    {
                        var hand = LeftHandPreview.GetComponent<HVRPosableHand>();
                        left = hand.CreateHandPose();
                    }

                    if (RightHandPreview)
                    {
                        var hand = RightHandPreview.GetComponent<HVRPosableHand>();
                        right = hand.CreateHandPose();
                    }

                    var clone = Instantiate(SelectedPose);
                    clone.LeftHand = left;
                    clone.RightHand = right;

                    SerializedSelectedPose.FindPropertyRelative("Pose").objectReferenceValue = AssetUtils.CreateOrReplaceAsset(clone, path);

                    serializedObject.ApplyModifiedProperties();
                    //PopulatePoses();
                }
            };
        }

        private void SetupSaveButton()
        {
            var button = _root.Q<Button>("SavePose");

            button.clickable.clicked += () =>
            {
                if (LeftHandPreview)
                {
                    var hand = LeftHandPreview.GetComponent<HVRPosableHand>();
                    var pose = hand.CreateHandPose();
                    SelectedPose.LeftHand = pose;
                }

                if (RightHandPreview)
                {
                    var hand = RightHandPreview.GetComponent<HVRPosableHand>();
                    var pose = hand.CreateHandPose();
                    SelectedPose.RightHand = pose;
                }

                EditorUtility.SetDirty(SelectedPose);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        private void SetupAddButton()
        {
            var addNewButton = _root.Q<Button>("AddBlendPose");

            addNewButton.clickable.clicked += () =>
            {
                var i = SP_Blends.arraySize;
                SP_Blends.InsertArrayElementAtIndex(i);
                var test = SP_Blends.GetArrayElementAtIndex(i).FindPropertyRelative("Pose");
                var clone = test.objectReferenceValue = PrimaryPose.Pose.DeepCopy();
                clone.name = "Unsaved!";

                SP_Blends.GetArrayElementAtIndex(i).FindPropertyRelative("AnimationParameter").stringValue = HVRHandPoseBlend.DefaultParameter;
                SP_Blends.GetArrayElementAtIndex(i).FindPropertyRelative("Weight").floatValue = 1f;
                SP_Blends.GetArrayElementAtIndex(i).FindPropertyRelative("Speed").floatValue = 16f;

                serializedObject.ApplyModifiedProperties();
                PopulatePoses();
            };
        }

        private void SetupMirrorButtons()
        {
            var mirrorRight = _root.Q<Button>("ButtonMirrorRight");

            mirrorRight.clickable.clicked += () =>
            {
                if (LeftHandPreview && RightHandPreview)
                {
                    var leftHand = LeftHandPreview.GetComponent<HVRPosableHand>();
                    var rightHand = RightHandPreview.GetComponent<HVRPosableHand>();
                    var right = leftHand.Mirror(Poser.MirrorAxis);

                    Undo.RegisterFullObjectHierarchyUndo(RightHandPreview, "Mirror left to right");

                    rightHand.Pose(right);
                }
            };

            var mirrorLeft = _root.Q<Button>("ButtonMirrorLeft");

            mirrorLeft.clickable.clicked += () =>
            {
                if (LeftHandPreview && RightHandPreview)
                {
                    var leftHand = LeftHandPreview.GetComponent<HVRPosableHand>();
                    var rightHand = RightHandPreview.GetComponent<HVRPosableHand>();
                    var left = rightHand.Mirror(Poser.MirrorAxis);

                    Undo.RegisterFullObjectHierarchyUndo(LeftHandPreview, "Mirror right to left");

                    leftHand.Pose(left);
                }
            };
        }

        private void SetupHandButtons()
        {
            var focusLeft = _root.Q<Button>("ButtonFocusLeft");

            focusLeft.clickable.clicked += () =>
            {
                if (LeftHandPreview) Selection.activeGameObject = LeftHandPreview;
            };

            var focusRight = _root.Q<Button>("ButtonFocusRight");

            focusRight.clickable.clicked += () =>
            {
                if (RightHandPreview) Selection.activeGameObject = RightHandPreview;
            };

            var leftExpand = _root.Q<Button>("LeftExpand");

            leftExpand.clickable.clicked += () =>
            {
                if (LeftHandPreview) LeftHandPreview.SetExpandedRecursive(true);
            };

            var leftCollapse = _root.Q<Button>("LeftCollapse");

            leftCollapse.clickable.clicked += () =>
            {
                if (LeftHandPreview) LeftHandPreview.SetExpandedRecursive(false);
            };

            var rightExpand = _root.Q<Button>("RightExpand");

            rightExpand.clickable.clicked += () =>
            {
                if (RightHandPreview) RightHandPreview.SetExpandedRecursive(true);
            };

            var rightCollapse = _root.Q<Button>("RightCollapse");

            rightCollapse.clickable.clicked += () =>
            {
                if (RightHandPreview) RightHandPreview.SetExpandedRecursive(false);
            };
        }

        private void SetupSelectedPose(VisualElement container)
        {
            SelectedPoseField = container.Q<ObjectField>("Pose");
            SelectedPoseField.objectType = typeof(HVRHandPose);
            SelectedPoseField.bindingPath = "Pose";
            SelectedPoseField.RegisterValueChangedCallback(OnSelectedPoseChanged);
        }

        private void SetupPosesListView()
        {
            PopulatePoses();
            PosesListView = _root.Q<ListView>("Poses");
            //PosesListView.itemsSource = Poser.PoseNames;
            PosesListView.bindingPath = "PoseNames";
            PosesListView.makeItem = MakePoseListItem;
            PosesListView.bindItem = BindItem;
            PosesListView.selectionType = SelectionType.Single;
            PosesListView.onSelectionChanged += OnPoseListIndexChanged;
            PosesListView.itemHeight = (int)EditorGUIUtility.singleLineHeight;
            PosesListView.style.height = EditorGUIUtility.singleLineHeight * 5;
            PosesListView.Bind(serializedObject);
        }

        public ListView BlendListView;
        private BindableElement blendEditorRoot;
        private SerializedObject blendObj;
        private bool _leftTracking;
        private bool _selectedPoseHandled;

        private void OnPreviewRightChanged(ChangeEvent<bool> evt)
        {
            if (SelectedPose == null)
            {
                PreviewRight.SetValueWithoutNotify(false);
                return;
            }

            //binding multiple editors to this object, they're bouncing off each other.
            if (evt.newValue && RightHandPreview)
            {
                return;
            }

            if (!evt.newValue && RightHandPreview)
            {
                DestroyImmediate(RightHandPreview);
                RightHandPreview = null;
                serializedObject.ApplyModifiedProperties();
            }
            else
                UpdatePreview(false, evt.newValue);
        }

        private void OnPreviewLeftChanged(ChangeEvent<bool> evt)
        {
            if (SelectedPose == null)
            {
                PreviewLeft.SetValueWithoutNotify(false);
                return;
            }

            //binding multiple editors to this object, they're bouncing off each other.
            if (evt.newValue && LeftHandPreview)
            {
                return;
            }

            if (!evt.newValue && LeftHandPreview)
            {
                DestroyImmediate(LeftHandPreview);
                LeftHandPreview = null;
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                UpdatePreview(true, evt.newValue);
            }
        }

        private void UpdatePreview(bool isLeft, bool preview, HVRHandPoseData pose = null)
        {
            var previewHandProperty = isLeft ? SP_LeftHandPreview : SP_RightHandPreview;
            var handPrefab = isLeft ? HVRSettings.Instance.LeftHand : HVRSettings.Instance.RightHand;
            if (pose == null) pose = isLeft ? SelectedPose?.LeftHand : SelectedPose?.RightHand;


            if (!preview || !handPrefab || pose == null)
            {
                return;
            }

            if (previewHandProperty.objectReferenceValue != null)
            {
                return;
            }

            var previewName = FindPreview(isLeft, out var existing);

            //binding multiple editors to this object, they're bouncing off each other.
            //safety net
            if (existing)
            {
                if (isLeft && SP_LeftHandPreview.objectReferenceValue != existing)
                {
                    SP_LeftHandPreview.objectReferenceValue = existing;
                    serializedObject.ApplyModifiedProperties();
                }
                else if (!isLeft && SP_RightHandPreview.objectReferenceValue != existing)
                {
                    SP_RightHandPreview.objectReferenceValue = existing;
                    serializedObject.ApplyModifiedProperties();
                }

                return;
            }

            var previewObj = previewHandProperty.objectReferenceValue as GameObject;
            if (!previewObj)
            {
                previewObj = Instantiate(handPrefab, Poser.transform, false);
                previewObj.name = previewName;
                previewHandProperty.objectReferenceValue = previewObj;
            }

            var hand = previewObj.GetComponent<HVRPosableHand>();
            serializedObject.ApplyModifiedProperties();
            if (hand != null)
            {
                hand.Pose(pose);
                SceneView.RepaintAll();
            }
            else
            {
                Debug.Log($"Preview hand is missing VRPosableHand");
            }
        }

        private string FindPreview(bool isLeft, out GameObject existing)
        {
            var previewName = isLeft ? _leftInstanceId : _rightInstanceId;
            existing = GameObject.Find(previewName);
            return previewName;
        }


        private void OnSelectedPoseChanged(ChangeEvent<Object> evt)
        {

            //if (evt.previousValue != null && (PreviewLeft.value || PreviewRight.value))
            //{
            //    var flavor = evt.newValue == null ? "Delete" : "Switch";
            //    if (!EditorUtility.DisplayDialog("Warning!", $"Preview hands are enabled. {flavor} pose and lose changes?", "Yes", "No"))
            //    {
            //        evt.StopImmediatePropagation();
            //        return;
            //    }
            //}




            //Debug.Log($"Selected pose changed from {evt.previousValue?.name} to  {evt.newValue?.name}");
            if (evt.newValue != null)
            {
                var newPose = evt.newValue as HVRHandPose;
                UpdatePreview(true, PreviewLeft.value, newPose.LeftHand);
                UpdatePreview(false, PreviewRight.value, newPose.RightHand);
            }
            else
            {
                PreviewLeft.SetValueWithoutNotify(false);
                PreviewRight.SetValueWithoutNotify(false);

                if (LeftHandPreview) DestroyImmediate(LeftHandPreview);
                if (RightHandPreview) DestroyImmediate(RightHandPreview);
            }

            _root.schedule.Execute(PopulatePoses);
        }



        private void OnPoseListIndexChanged(List<object> p)
        {
            //if (PreviewLeft.value || PreviewRight.value)
            //{
            //    if (!EditorUtility.DisplayDialog("Warning!", $"Preview hands are enabled. Switch pose and lose changes?", "Yes", "No"))
            //    {
            //        try
            //        {
            //            PosesListView.onSelectionChanged -= OnPoseListIndexChanged;
            //            PosesListView.selectedIndex = _previousIndex;
            //        }
            //        finally
            //        {
            //            PosesListView.onSelectionChanged += OnPoseListIndexChanged;
            //        }


            //        return;
            //    }
            //}

            if (PosesListView.selectedIndex >= Poser.PoseNames.Count)
            {
                PosesListView.selectedIndex = Poser.PoseNames.Count - 1;
                return;
            }

            if (SelectedBlendPose != null && string.IsNullOrWhiteSpace(SelectedBlendPose.AnimationParameter))
            {
                SelectedBlendPose.AnimationParameter = "None";
            }

            SelectedIndex = PosesListView.selectedIndex;
            _previousIndex = CurrentPoseIndex;

            BindBlendContainer();
        }

        private void BindItem(VisualElement visual, int index)
        {
            var label = visual as Label;
            if (index == PrimaryIndex)
            {
                label.AddToClassList("primarypose");

            }

            if (index < Poser.PoseNames.Count)
                label.text = Poser.PoseNames[index];
        }

        private VisualElement MakePoseListItem()
        {
            return new Label();
        }

        public void PopulatePoses()
        {
            SP_PoseNames.ClearArray();
            //Poser.PoseNames.Clear();
            var primaryName = PrimaryPose == null || PrimaryPose.Pose == null ? "Primary Not Set" : "Primary: " + PrimaryPose.Pose.name;
            SP_PoseNames.InsertArrayElementAtIndex(0);
            SP_PoseNames.GetArrayElementAtIndex(0).stringValue = primaryName;
            //Poser.PoseNames.Add(primaryName);
            for (var i = 0; i < SP_Blends.arraySize; i++)
            {
                string poseName;
                var blendablePose = Poser.Blends[i];
                if (blendablePose == null || blendablePose.Pose == null)
                {
                    poseName = "Not Set";
                }
                else
                {
                    poseName = blendablePose.Pose.name;
                }

                SP_PoseNames.InsertArrayElementAtIndex(i + 1);
                SP_PoseNames.GetArrayElementAtIndex(i + 1).stringValue = poseName;
            }
            //PosesListView.Bind();
            serializedObject.ApplyModifiedProperties();


            PosesListView?.Refresh();
        }
    }

    public class ArrayInspectorElement : BindableElement, INotifyValueChanged<int>
    {
        private readonly SerializedObject boundObject;
        private readonly string m_ArrayPropertyPath;

        public Func<string, int, VisualElement> makeItem { get; set; }

        public override VisualElement contentContainer => m_Container;

        private readonly VisualElement m_Container;

        public ArrayInspectorElement(SerializedProperty arrayProperty, Func<string, int, VisualElement> makeItem)
        {
            var header = new VisualElement();

            header.Add(new Label(arrayProperty.displayName));

            var addButton = new Button(() =>
            {
                arrayProperty.InsertArrayElementAtIndex(0);
                arrayProperty.serializedObject.ApplyModifiedProperties();
            });
            addButton.text = "+";
            header.Add(addButton);

            // This belongs in uss
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;


            //We use a content container so that array size = child count    
            // And the child management becomes easier
            m_Container = new VisualElement() { name = "array-contents" };
            this.hierarchy.Add(header);
            this.hierarchy.Add(m_Container);

            m_ArrayPropertyPath = arrayProperty.propertyPath;
            boundObject = arrayProperty.serializedObject;
            this.makeItem = makeItem;

            var property = arrayProperty.Copy();
            var endProperty = property.GetEndProperty();

            //We prefill the container since we know we will need this
            property.NextVisible(true); // Expand the first child.
            do
            {
                if (SerializedProperty.EqualContents(property, endProperty))
                    break;
                if (property.propertyType == SerializedPropertyType.ArraySize)
                {
                    arraySize = property.intValue;
                    bindingPath = property.propertyPath;
                    break;
                }
            }
            while (property.NextVisible(false)); // Never expand children.

            UpdateCreatedItems();
            //we assume we don't need to Bind here
        }

        VisualElement AddItem(string propertyPath, int index)
        {
            VisualElement child;
            if (makeItem != null)
            {
                child = makeItem(propertyPath, index);
            }
            else
            {
                var pf = new PropertyField();
                pf.bindingPath = propertyPath;
                child = pf;
            }

            Add(child);
            return child;
        }

        bool UpdateCreatedItems()
        {
            int currentSize = childCount;

            int targetSize = this.arraySize;

            if (targetSize < currentSize)
            {
                for (int i = currentSize - 1; i >= targetSize; --i)
                {
                    RemoveAt(i);
                }
            }
            else if (targetSize > currentSize)
            {
                for (int i = currentSize; i < targetSize; ++i)
                {
                    AddItem($"{m_ArrayPropertyPath}.Array.data[{i}]", i);
                }

                return true; //we created new Items
            }

            return false;
        }

        private int arraySize = 0;
        public void SetValueWithoutNotify(int newSize)
        {
            this.arraySize = newSize;

            if (UpdateCreatedItems())
            {
                //We rebind the array
                this.Bind(boundObject);
            }
        }

        public int value
        {
            get => arraySize;
            set
            {
                if (arraySize == value) return;

                if (panel != null)
                {
                    using (ChangeEvent<int> evt = ChangeEvent<int>.GetPooled(arraySize, value))
                    {
                        evt.target = this;

                        // The order is important here: we want to update the value, then send the event,
                        // so the binding writes and updates the serialized object
                        arraySize = value;
                        SendEvent(evt);

                        //Then we remove or create + bind the needed items
                        SetValueWithoutNotify(value);
                    }
                }
                else
                {
                    SetValueWithoutNotify(value);
                }
            }
        }
    }
}