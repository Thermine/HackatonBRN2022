using System.Collections;
using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace HurricaneVR.Framework.Core.Player
{

    //this code is a derivation of public domain tutorials found at the following links
    //https://codingchronicles.com/unity-vr-development/day-45-to-58-creating-locomotion-in-vr
    //https://github.com/chang47/VRLocomotionExamples/tree/master/Assets/Scripts

    [RequireComponent(typeof(CharacterController))]
    public class HVRTeleporter : MonoBehaviour
    {

        public LineRenderer LineRenderer;
        public LineRenderer DownRenderer;
        public int LineSegments = 20;
        public float LineSegmentScale = 1;
        public float Strength = 500f;
        public float StartingAngle = 65;
        public float MaxAngle = 75;
        public LayerMask LayerMask;
        public float MaxDistance = 4f;
        public float FloorOffset = .05f;

        public bool Dash;
        public float DashSpeed = 15f;

        public Transform TeleportLineSourceLeft;
        public Transform TeleportLineSourceRight;
        public GameObject TeleportMarker;

        public Transform TeleportLineSource => PlayerInputs.TeleportHandSide == HVRHandSide.Left ? TeleportLineSourceLeft : TeleportLineSourceRight;

        private Collider _hitObject;
        public Collider hitObject { get { return _hitObject; } }


        private Vector3[] _points;

        private bool _isTeleportValid;
        private Vector3 _teleportDestination;
        private bool _canTeleport;
        private bool _isTeleporting;
        private Collider[] _dummy = new Collider[1];

        public bool IsAiming { get; private set; }

        public HVRPlayerInputs PlayerInputs { get; private set; }

        public CharacterController CharacterController { get; private set; }

        public UnityEvent BeforeTeleport = new UnityEvent();
        public UnityEvent AfterTeleport = new UnityEvent();

        private void Start()
        {
            PlayerInputs = GetComponent<HVRPlayerInputs>();
            _points = new Vector3[LineSegments];
            CharacterController = GetComponent<CharacterController>();
            _canTeleport = true;
        }

        private void Update()
        {
            CheckTeleport();
        }

        public void Enable()
        {
            _canTeleport = true;
        }

        public void Disable()
        {
            IsAiming = false;
            _canTeleport = false;
            ToggleGraphics(false);
        }

        private void ToggleGraphics(bool toggle)
        {
            LineRenderer.enabled = toggle;
            DownRenderer.enabled = toggle;
            if (TeleportMarker)
                TeleportMarker.SetActive(toggle);
        }

        private void CheckTeleport()
        {
            if (!_canTeleport || _isTeleporting)
                return;

            if (IsTeleportActivated())
            {
                ToggleGraphics(true);
                IsAiming = true;
            }
            else if (IsTeleportDeactivated())
            {
                if (IsAiming)
                {
                    if (_isTeleportValid)
                    {
                        if (Dash)
                        {
                            StartCoroutine(DashTeleport());
                        }
                        else
                        {
                            StartCoroutine(Teleport());
                        }

                        _isTeleportValid = false;
                    }
                }

                ToggleGraphics(false);
                IsAiming = false;
            }
        }

        protected virtual bool IsTeleportDeactivated()
        {
            return PlayerInputs.IsTeleportDeactivated;
        }

        protected virtual bool IsTeleportActivated()
        {
            return PlayerInputs.IsTeleportActivated;
        }

        private IEnumerator DashTeleport()
        {
            try
            {
                _isTeleporting = true;
                BeforeTeleport.Invoke();

                CharacterController.enabled = false;

                while (Vector3.Distance(CharacterController.transform.position, _teleportDestination) > .3)
                {
                    CharacterController.transform.position = Vector3.MoveTowards(CharacterController.transform.position, _teleportDestination, DashSpeed * Time.deltaTime);
                    yield return null;
                }
            }
            finally
            {
                _isTeleporting = false;
                CharacterController.enabled = true;
                AfterTeleport.Invoke();
            }
        }

        private IEnumerator Teleport()
        {

            try
            {
                _isTeleporting = true;
                BeforeTeleport.Invoke();

                CharacterController.enabled = false;

                CharacterController.transform.position = _teleportDestination;

                yield return null;

                CharacterController.enabled = true;

            }
            finally
            {
                _isTeleporting = false;
                CharacterController.enabled = true;
                AfterTeleport.Invoke();
            }


        }

        void FixedUpdate()
        {
            if (IsAiming)
            {
                CalculateTrajectory();
            }
        }

        private void CalculateTrajectory()
        {
            _points[0] = TeleportLineSource.position;

            var rotation = Quaternion.AngleAxis(-StartingAngle, TeleportLineSource.forward);
            var velocity = rotation * TeleportLineSource.forward * Strength * Time.deltaTime;

            _hitObject = null;
            _isTeleportValid = false;


            var hitVector = Vector3.zero;
            var lastValidIndex = 1;
            var lastValidPoint = _points[0];
            for (var i = 1; i < LineSegments; i++)
            {
                if (_hitObject != null)
                {
                    _points[i] = hitVector;
                    continue;
                }

                var segTime = Mathf.Approximately(velocity.sqrMagnitude, 0f) ? 0f : LineSegmentScale / velocity.magnitude;
                velocity = velocity + Physics.gravity * segTime;

                if (Physics.Raycast(_points[i - 1], velocity, out var hit, LineSegmentScale, LayerMask, QueryTriggerInteraction.Ignore))
                {
                    _hitObject = hit.collider;
                    _points[i] = _points[i - 1] + velocity.normalized * hit.distance;
                    velocity = velocity - Physics.gravity * (LineSegmentScale - hit.distance) / velocity.magnitude;
                    velocity = Vector3.Reflect(velocity, hit.normal);
                    hitVector = _points[i];
                }
                else
                {
                    _points[i] = _points[i - 1] + velocity * segTime;

                    if (Physics.Raycast(_points[i], Vector3.down, out hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var distanceToSphereCenter = CharacterController.height * .5f - CharacterController.radius;
                        var offset = new Vector3(0f, FloorOffset, 0f);
                        var p1 = offset + hit.point + CharacterController.center + Vector3.up * distanceToSphereCenter;
                        var p2 = offset + hit.point + CharacterController.center - Vector3.up * distanceToSphereCenter;

                        if (Physics.OverlapCapsuleNonAlloc(p1, p2, CharacterController.radius, _dummy, ~0, QueryTriggerInteraction.Ignore) == 0)
                        {
                            _teleportDestination = hit.point + offset;
                            TeleportMarker.transform.position = hit.point;
                            DownRenderer.SetPosition(0, _points[i]);
                            DownRenderer.SetPosition(1, hit.point);
                            lastValidIndex = i;
                            lastValidPoint = _points[i];
                            _isTeleportValid = true;
                        }

                    }
                }
            }

            if (TeleportMarker)
            {
                var target = transform.position + 20f * TeleportLineSource.forward.normalized;
                target.y = TeleportMarker.transform.position.y;
                TeleportMarker.transform.LookAt(target);
            }

            for (var i = LineSegments - 1; i >= lastValidIndex; i--)
            {
                _points[i] = lastValidPoint;
            }

            LineRenderer.positionCount = _points.Length;
            LineRenderer.SetPositions(_points);

        }
    }
}
