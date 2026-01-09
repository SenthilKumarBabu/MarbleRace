using UnityEngine;
using Unity.Cinemachine;

namespace Marble
{
    public class MarbleRaceCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MarbleRaceController raceController;
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private CinemachineFollow followComponent;
        [SerializeField] private CinemachineRotationComposer rotationComposer;

        [Header("Follow Settings")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 3f, -6f);
        [SerializeField] private float followDamping = 1f;

        [Header("Look-Ahead")]
        [SerializeField] private float lookAheadDistance = 5f;
        [SerializeField] private float lookAheadSmoothing = 0.5f;

        [Header("Target Transition")]
        [SerializeField] private float targetTransitionSpeed = 2f;

        private Transform _currentTarget;
        private Transform _pendingTarget;
        private byte _currentLeaderId = 255;
        private Vector3 _lookAheadOffset;
        private Vector3 _smoothLookAhead;

        private Transform _cameraTarget;
        private bool _createdTarget;

        private void Awake()
        {
            _cameraTarget = cameraTarget;
            _createdTarget = false;
        }

        private void Start()
        {
            if (virtualCamera == null)
            {
                Debug.LogError("[MarbleRaceCamera] CinemachineCamera not assigned!");
                return;
            }

            followComponent.FollowOffset = followOffset;
            followComponent.TrackerSettings.PositionDamping = Vector3.one * followDamping;

            virtualCamera.Follow = _cameraTarget;
            virtualCamera.LookAt = _cameraTarget;

            if (raceController != null && raceController.PositionTracker != null)
            {
                raceController.PositionTracker.OnPositionsChanged += HandlePositionsChanged;
            }
        }

        private void OnDestroy()
        {
            if (raceController != null && raceController.PositionTracker != null)
            {
                raceController.PositionTracker.OnPositionsChanged -= HandlePositionsChanged;
            }

            if (_createdTarget && _cameraTarget != null)
            {
                Destroy(_cameraTarget.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (raceController == null) return;

            UpdateLeaderTarget();

            if (_currentTarget != null)
            {
                UpdateCameraTarget();
            }
        }

        private void UpdateLeaderTarget()
        {
            if (raceController.PositionTracker == null) return;

            byte leaderId = raceController.PositionTracker.GetMarbleAtPosition(0);

            bool shouldUpdateTarget = leaderId != _currentLeaderId || _currentTarget == null;

            if (shouldUpdateTarget)
            {
                _currentLeaderId = leaderId;
                Transform newTarget = GetMarbleTransform(leaderId);

                if (newTarget != null)
                {
                    _pendingTarget = newTarget;

                    if (_currentTarget == null)
                    {
                        _currentTarget = newTarget;
                        _cameraTarget.position = _currentTarget.position;
                        Debug.Log($"[Camera] Initial target set to marble {leaderId} at {_currentTarget.position}");
                    }
                }
            }

            if (_pendingTarget != null && _pendingTarget != _currentTarget)
            {
                _currentTarget = _pendingTarget;
            }
        }

        private void UpdateCameraTarget()
        {
            Vector3 targetPos = _currentTarget.position;

            Vector3 velocity = GetMarbleVelocity(_currentLeaderId);
            if (velocity.sqrMagnitude > 0.1f)
            {
                _lookAheadOffset = velocity.normalized * lookAheadDistance;
            }

            _smoothLookAhead = Vector3.Lerp(_smoothLookAhead, _lookAheadOffset, Time.deltaTime / lookAheadSmoothing);

            Vector3 lookAtPos = targetPos + _smoothLookAhead;

            _cameraTarget.position = Vector3.Lerp(
                _cameraTarget.position,
                targetPos,
                Time.deltaTime * targetTransitionSpeed
            );
        }

        private Transform GetMarbleTransform(byte marbleId)
        {
            var controllers = raceController.PhysicsControllers;
            if (controllers != null && marbleId < controllers.Length && controllers[marbleId] != null)
            {
                Debug.Log($"[Camera] Found marble {marbleId} via PhysicsController at {controllers[marbleId].transform.position}");
                return controllers[marbleId].transform;
            }

            var visuals = raceController.PhysicsVisuals;
            if (visuals != null && marbleId < visuals.Length && visuals[marbleId] != null)
            {
                Debug.Log($"[Camera] Found marble {marbleId} via PhysicsVisual at {visuals[marbleId].transform.position}");
                return visuals[marbleId].transform;
            }

            if ((controllers != null && controllers.Length > 0) || (visuals != null && visuals.Length > 0))
            {
                Debug.LogWarning($"[Camera] Could NOT find marble {marbleId}! Controllers: {controllers?.Length ?? 0}, Visuals: {visuals?.Length ?? 0}");
            }
            return null;
        }

        private Vector3 GetMarbleVelocity(byte marbleId)
        {
            var controllers = raceController.PhysicsControllers;
            if (controllers != null && marbleId < controllers.Length && controllers[marbleId] != null)
            {
                return controllers[marbleId].Velocity;
            }

            var visuals = raceController.PhysicsVisuals;
            if (visuals != null && marbleId < visuals.Length && visuals[marbleId] != null)
            {
                var rb = visuals[marbleId].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    return rb.linearVelocity;
                }
            }

            return Vector3.forward;
        }

        private void HandlePositionsChanged(byte[] newPositions)
        {
        }

        public void SetFollowTarget(byte marbleId)
        {
            Transform target = GetMarbleTransform(marbleId);
            if (target != null)
            {
                _currentTarget = target;
                _currentLeaderId = marbleId;
            }
        }

        public void FollowLeader()
        {
            _currentLeaderId = 255;
        }

        public void SnapToTarget()
        {
            if (_currentTarget != null)
            {
                _cameraTarget.position = _currentTarget.position;
                _smoothLookAhead = _lookAheadOffset;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (followComponent != null)
            {
                followComponent.FollowOffset = followOffset;
                followComponent.TrackerSettings.PositionDamping = Vector3.one * followDamping;
            }
        }
#endif
    }
}