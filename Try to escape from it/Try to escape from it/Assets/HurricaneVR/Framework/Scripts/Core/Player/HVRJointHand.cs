using HurricaneVR.Framework.Core.Utils;
using HurricaneVR.Framework.Shared;
using HurricaneVR.Framework.Weapons;
using UnityEngine;
using UnityEngine.Events;

namespace HurricaneVR.Framework.Core.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class HVRJointHand : MonoBehaviour
    {
        public UnityEvent MaxDistanceReached = new UnityEvent();
        public UnityEvent ReturnedToController = new UnityEvent();

        public HVRHandPhysics HandPhysics;
        public Rigidbody ParentRigidBody;
        public Rigidbody RigidBody;
        public Transform Controller;
        public Transform TrackedController;


        public float MaxDistance = .8f;
        public bool DisablePhysicsOnReturn;
        public bool IsReturningToController;
        public Vector3 PreviousControllerPosition;
        public float Power;
        private Quaternion _startingRotation;

        public float MaximumVelocity = 3f;

        public float Spring = 360000;
        public float Damper = 120000;
        public float MaxForce = 1200;

        public float SlerpSpring = 40000;
        public float SlerpDamper = 1000;
        public float SlerpMaxForce = 150f;

        public HVRJointSettings JointSettings;

        public ConfigurableJoint Joint { get; private set; }
        public HVRJointSettings JointOverride { get; set; }

        public float DamperFactor
        {
            get
            {
                if (JointOverride)
                    return JointOverride.DamperDelayPower;
                return Power;
            }
        }

        private void Awake()
        {
            SetupJoint();
        }

        public void Disable()
        {
            RigidBody.isKinematic = true;
        }

        public void Enable()
        {
            RigidBody.isKinematic = false;
        }

        private void SetupJoint()
        {
            _startingRotation = transform.localRotation;



            //Debug.Log($"{name} joint created.");
            //this joint needs to be created before any offsets are applied to the controller target
            //due to how joints snapshot their initial rotations on creation
            Joint = gameObject.AddComponent<ConfigurableJoint>();
            Joint.connectedBody = ParentRigidBody;
            Joint.autoConfigureConnectedAnchor = false;
            Joint.anchor = Vector3.zero;

            if (JointSettings)
            {
                JointSettings.ApplySettings(Joint);
            }
            else
            {
                Joint.enableCollision = false;
                Joint.enablePreprocessing = false;

                var drive = new JointDrive();
                drive.positionSpring = Spring;
                drive.positionDamper = Damper;
                drive.maximumForce = MaxForce;
                var slerpDrive = new JointDrive();
                slerpDrive.positionSpring = SlerpSpring;
                slerpDrive.positionDamper = SlerpDamper;
                slerpDrive.maximumForce = SlerpMaxForce;
                Joint.rotationDriveMode = RotationDriveMode.Slerp;
                Joint.xDrive = Joint.yDrive = Joint.zDrive = drive;
                Joint.slerpDrive = slerpDrive;
            }

        }

        void Start()
        {
            RigidBody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            UpdateJointAnchors();
            UpdateTargetVelocity();

            UpdateJoint();

            Joint.SetTargetRotationLocal(Controller.localRotation, _startingRotation);

            if (Vector3.Distance(transform.position, Controller.position) > MaxDistance)
            {
                if (!IsReturningToController)
                {
                    IsReturningToController = true;
                    MaxDistanceReached.Invoke();

                    if (HandPhysics && DisablePhysicsOnReturn)
                    {
                        HandPhysics.SetAllToTrigger();
                    }
                }

                if (!HandPhysics || !DisablePhysicsOnReturn)
                {
                    transform.position = Vector3.MoveTowards(transform.position, Controller.position, MaxDistance / 2f);
                }
            }
            else if (IsReturningToController)
            {
                if (HandPhysics && DisablePhysicsOnReturn)
                {
                    HandPhysics.ResetToNonTrigger();
                }
                IsReturningToController = false;
                ReturnedToController.Invoke();
            }
            var velocity = RigidBody.velocity;
            velocity.x = Mathf.Clamp(velocity.x, -MaximumVelocity, MaximumVelocity);
            velocity.y = Mathf.Clamp(velocity.y, -MaximumVelocity, MaximumVelocity);
            velocity.z = Mathf.Clamp(velocity.z, -MaximumVelocity, MaximumVelocity);
            RigidBody.velocity = velocity;


        }

        private void UpdateJoint()
        {
            if (JointOverride != null)
            {
                JointOverride.ApplySettings(Joint);
            }
            else if (JointSettings != null)
            {
                JointSettings.ApplySettings(Joint);
            }
            else
            {
                var drive = Joint.xDrive;
                drive.positionSpring = Spring;
                drive.positionDamper = Damper;
                drive.maximumForce = MaxForce;
                var slerpDrive = Joint.slerpDrive;
                slerpDrive.positionSpring = SlerpSpring;
                slerpDrive.positionDamper = SlerpDamper;
                slerpDrive.maximumForce = SlerpMaxForce;
                Joint.xDrive = Joint.yDrive = Joint.zDrive = drive;
                Joint.slerpDrive = slerpDrive;

            }
        }

        private void UpdateJointAnchors()
        {
            Joint.connectedAnchor = ParentRigidBody.transform.InverseTransformPoint(Controller.position);
        }

        public void UpdateTargetVelocity()
        {
            var worldVelocity = (Controller.position - PreviousControllerPosition) / Time.fixedDeltaTime;
            PreviousControllerPosition = Controller.position;
            var targetVelocity = (-worldVelocity - (Controller.position - transform.position) * DamperFactor);
            Joint.targetVelocity = transform.InverseTransformDirection(targetVelocity);
        }

        public void LookAt(Transform lookat)
        {
            var rotation = Quaternion.LookRotation(lookat.position - TrackedController.position, TrackedController.transform.up);
            Controller.transform.rotation = rotation;
        }

        public void ResetLookAt()
        {
            Controller.transform.localRotation = Quaternion.identity;
        }


        private void OnDrawGizmos()
        {
            if (RigidBody)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(RigidBody.worldCenterOfMass, .017f);
            }
        }
    }
}
