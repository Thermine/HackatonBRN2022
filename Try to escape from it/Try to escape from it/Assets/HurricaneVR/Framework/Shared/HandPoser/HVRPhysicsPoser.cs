using System.Collections.Generic;
using System.Diagnostics;
using HurricaneVR.Framework.Shared.HandPoser.Data;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HurricaneVR.Framework.Shared.HandPoser
{
    public class HVRPhysicsPoser : MonoBehaviour
    {
        public HVRPosableHand Hand;

        public HVRHandPose OpenPose;
        public HVRHandPose ClosedPose;
        public Transform Palm;

        public int RaysPerBone = 3;
        public float SphereRadius = .008f;
        public bool DrawSpheres;
        public bool DrawTips;
        public float MaxFingerBend = 1f;
        public List<Vector3> CollisionPoints = new List<Vector3>();
        public int CurrentIteration = 0;

        public Dictionary<Transform, List<Vector3>> SphereMap = new Dictionary<Transform, List<Vector3>>();
        public HashSet<Transform> CollidedBones = new HashSet<Transform>();
        public int[] FingerBends = new int[5];

        public HVRHandPoseData OpenPoseData => OpenPose.GetPose(Hand.IsLeft);
        public HVRHandPoseData ClosedPoseData => ClosedPose.GetPose(Hand.IsLeft);


        void Start()
        {
            if (!Hand)
            {
                Hand = GetComponent<HVRPosableHand>();
            }
            SetupCollision();
        }

        void Update()
        {
        }



        public void SetupCollision()
        {
            SphereMap.Clear();
            foreach (var finger in Hand.Fingers)
            {
                SetupCollision(finger);
            }
        }

        public void SetupCollision(HVRPosableFinger finger)
        {
            for (var i = 0; i < finger.Bones.Count; i++)
            {
                var bone = finger.Bones[i];
                SphereMap[bone.Transform] = new List<Vector3>();

                var current = Vector3.zero;
                var next = i == finger.Bones.Count - 1 ? finger.Tip.localPosition : finger.Bones[i + 1].Transform.localPosition;

                for (var j = 0; j < RaysPerBone; j++)
                {
                    var point = Vector3.Lerp(current, next, (j + 1f) / RaysPerBone);
                    SphereMap[bone.Transform].Add(point);
                }
            }
        }


        public int _fingerIndex;
        public HVRPosableFinger _currentFinger;
        private Collider[] colliders = new Collider[1];
        public int Iterations = 100;
        public LayerMask CurrentMask;


        public void OpenFingers()
        {
            if (Hand && OpenPose)
            {
                Hand.PoseFingers(OpenPose);
            }
        }

        public void TestClose()
        {
            var watch = Stopwatch.StartNew();
            ResetHand();
            SimulateClose(~LayerMask.GetMask("Hand"), true);
            watch.Stop();
            //Debug.Log(watch.ElapsedMilliseconds);
        }


        public void SimulateClose(LayerMask mask, bool editor = false)
        {
            CurrentMask = mask;
            ResetHand();
            MaxFingerBend = 1;
            CollisionPoints.Clear();

            foreach (var finger in Hand.Fingers)
            {
                _currentFinger = finger;
                StepFinger(editor);
                _fingerIndex++;
            }
        }

        public void SimulateToBlend(float blend, bool editor = false)
        {
            ResetHand();

            var iteration = (int)(blend * Iterations);

            for (var i = 0; i < Hand.Fingers.Length; i++)
            {
                var finger = Hand.Fingers[i];
                if (iteration == FingerBends[i])
                {
                    continue;
                }

                FingerBends[i] = iteration;

                _currentFinger = finger;
                Iterate(finger, iteration, editor);
                _fingerIndex++;
            }
        }

        public void NextFinger()
        {
            if (_fingerIndex + 1 < 5)
            {
                CurrentIteration = 0;
                _fingerIndex++;
                _currentFinger = Hand.Fingers[_fingerIndex];
            }
        }


        public void StepFinger(bool editor)
        {
            CurrentIteration = 0;
            for (int j = 0; j < Iterations; j++)
            {
                CurrentIteration++;
                if (Iterate(_currentFinger, CurrentIteration, editor))
                {
                    break;
                }
            }
        }

        public void StepIteration()
        {
            Iterate(_currentFinger, ++CurrentIteration, true);
        }

        public void BackStepIteration()
        {
            Iterate(_currentFinger, --CurrentIteration, true);
        }

        private bool Iterate(HVRPosableFinger currentFinger, int j, bool editor)
        {
            if (j >= Iterations || _fingerIndex > 4)
            {
                return true;
            }

            if (OpenPoseData.Fingers.Length != 5 || OpenPoseData.Fingers[_fingerIndex].Bones.Count != currentFinger.Bones.Count ||
                ClosedPoseData.Fingers.Length != 5 || ClosedPoseData.Fingers[_fingerIndex].Bones.Count != currentFinger.Bones.Count)
            {
                Debug.LogWarning($"Pose data missing matching bone structure.");
                return true;
            }

            for (var x = 0; x < currentFinger.Bones.Count; x++)
            {
                var currentBone = currentFinger.Bones[x];

                if (CollidedBones.Contains(currentBone.Transform)) continue;
                if (!SphereMap.TryGetValue(currentBone.Transform, out var points)) continue;

                var currentPosition = currentBone.Transform.localPosition;
                var currentRotation = currentBone.Transform.localRotation;

                var openBone = OpenPoseData.Fingers[_fingerIndex].Bones[x];
                var closedBone = ClosedPoseData.Fingers[_fingerIndex].Bones[x];

                currentBone.Transform.localPosition = Vector3.Lerp(openBone.Position, closedBone.Position, (float)j / Iterations);
                currentBone.Transform.localRotation = Quaternion.Lerp(openBone.Rotation, closedBone.Rotation, (float)j / Iterations);

                for (var i = 0; i < points.Count; i++)
                {
                    var point = points[i];

                    var world = currentBone.Transform.TransformPoint(point);

                    var hits = 0;
                    //is there a better way to know if we're in scene or prefab view ? this is required to work in prefab view.
                    if (editor)
                    {
                        hits = gameObject.scene.GetPhysicsScene().OverlapSphere(world, SphereRadius, colliders, CurrentMask, QueryTriggerInteraction.Ignore);
                    }
                    else
                    {
                        hits = Physics.OverlapSphereNonAlloc(world, SphereRadius, colliders, CurrentMask, QueryTriggerInteraction.Ignore);
                    }


                    if (hits > 0)
                    {
                        //stop moving this and any higher bones
                        for (var y = x; y >= 0; y--)
                        {
                            CollidedBones.Add(_currentFinger.Bones[y].Transform);
                        }

                        CollisionPoints.Add(world);
                        //Debug.Log(currentBone.Transform.name + " collided.");
                        currentBone.Transform.localPosition = currentPosition;
                        currentBone.Transform.localRotation = currentRotation;
                        break;
                    }
                }
            }

            return false;
        }

        public void ResetHand()
        {
            CurrentIteration = 0;
            CollidedBones.Clear();
            CollisionPoints.Clear();
            _fingerIndex = 0;
            _currentFinger = Hand.Fingers[0];
        }

        private void OnDrawGizmos()
        {
            if (!Hand)
            {
                return;
            }

            foreach (var finger in Hand.Fingers)
            {
                if (DrawSpheres)
                {
                    foreach (var bone in finger.Bones)
                    {
                        if (!SphereMap.TryGetValue(bone.Transform, out var points)) continue;

                        for (var i = 0; i < points.Count; i++)
                        {
                            var point = points[i];
                            var worldPosition = bone.Transform.TransformPoint(point);
                            Gizmos.color = Color.cyan;
                            Gizmos.DrawWireSphere(worldPosition, SphereRadius);

                        }
                    }
                }

                if (DrawTips)
                {
                    if (finger.Tip)
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireSphere(finger.Tip.position, SphereRadius);
                    }
                }
            }


            foreach (var point in CollisionPoints)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(point, SphereRadius);
            }
        }
    }
}
