using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Plinko
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlinkoBoard board;
        [SerializeField] private PlinkoUI plinkoUI;
        [SerializeField] private GameObject ballPrefab;

        [Header("Target Slots")]
        [SerializeField] private int[] targetSlots = { 0, 5, 8, 15, 3 };
        [SerializeField] private bool useControlledOutcome = true;

        [Header("Drop Settings")]
        [SerializeField] private float SpawnDelay = 0.5f;
        [SerializeField] private float dropInterval = 1.0f;

        [Header("Object Pool")]
        [SerializeField] private int poolSize = 10;
        [SerializeField] private Transform poolParent;

            private int currentTargetIndex;
        private bool isBallInPlay;
        private bool isMultiDropInProgress;
        private int ballsInPlayCount;
        private List<BallResult> results;
        private PlinkoBall currentBall;
        private List<PlinkoBall> activeBalls;
        private Queue<PlinkoBall> ballPool;

        // Cached for GC optimization
        private WaitForSeconds spawnWait;
        private WaitForSeconds intervalWait;

        public event Action<int, float> OnBallResult;
        public event Action<bool> OnBallStateChanged;
        public event Action OnTargetsCompleted;

        public bool IsBallInPlay => isBallInPlay;
        public bool IsMultiDropInProgress => isMultiDropInProgress;
        public int CurrentTargetSlot => useControlledOutcome && currentTargetIndex < targetSlots.Length
            ? targetSlots[currentTargetIndex] : -1;
        public int RemainingTargets => Mathf.Max(0, targetSlots.Length - currentTargetIndex);
        public List<BallResult> Results => results;
        public bool UseControlledOutcome => useControlledOutcome;

        public void SetControlledOutcome(bool controlled)
        {
            Debug.Log($"[PLINKO] Controlled outcome set to: {controlled}");
            useControlledOutcome = controlled;
        }

        [Serializable]
        public struct BallResult
        {
            public int targetSlot;
            public int actualSlot;
            public float multiplier;
            public bool wasControlled;
            public bool hitTarget;
        }

        private void Awake()
        {
            Debug.Log("[PLINKO] GameManager Awake");
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PLINKO] Duplicate GameManager detected, destroying");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Pre-allocate collections
            results = new List<BallResult>(targetSlots.Length);
            ballPool = new Queue<PlinkoBall>(poolSize);
            activeBalls = new List<PlinkoBall>(10);
            spawnWait = new WaitForSeconds(SpawnDelay);
            intervalWait = new WaitForSeconds(dropInterval);
        }

        private void Start()
        {
            Debug.Log("[PLINKO] GameManager Start - Initializing game");
            InitializePool();

            board.GenerateBoard();
            Debug.Log($"[PLINKO] Board generated with {board.PegRows} rows and {board.SlotCount} slots");

            Slot.OnBallLanded += HandleBallLanded;
            Debug.Log("[PLINKO] GameManager initialization complete");
        }

        private void OnDestroy()
        {
            Slot.OnBallLanded -= HandleBallLanded;
        }

        private void InitializePool()
        {
            Debug.Log($"[PLINKO] Initializing ball pool with {poolSize} balls");
            for (int i = 0; i < poolSize; i++)
            {
                PlinkoBall ball = CreateBallForPool();
                ballPool.Enqueue(ball);
            }
            Debug.Log($"[PLINKO] Ball pool initialized: {ballPool.Count} balls ready");
        }

        private PlinkoBall CreateBallForPool()
        {
            GameObject ballObj = Instantiate(ballPrefab, poolParent);
            ballObj.TryGetComponent(out PlinkoBall ball);
            ballObj.SetActive(false);
            return ball;
        }

        private PlinkoBall GetBallFromPool()
        {
            PlinkoBall ball;

            if (ballPool.Count > 0)
            {
                ball = ballPool.Dequeue();
            }
            else
            {
                ball = CreateBallForPool();
            }

            ball.gameObject.SetActive(true);
            return ball;
        }

        public void ReturnBallToPool(PlinkoBall ball)
        {
            if (ball == null) return;

            ball.ResetBall();
            ball.transform.SetParent(poolParent);
            ball.gameObject.SetActive(false);
            ballPool.Enqueue(ball);
        }

        public void SetTargetSlots(int[] slots)
        {
            Debug.Log($"[PLINKO] Setting target slots: [{string.Join(", ", slots)}]");
            targetSlots = slots;
            currentTargetIndex = 0;
            results.Clear();
        }

        public void DropBall()
        {
            Debug.Log("[PLINKO] DropBall called");
            if (isBallInPlay)
            {
                Debug.LogWarning("[PLINKO] Ball already in play, ignoring drop request");
                return;
            }

            if (useControlledOutcome && currentTargetIndex >= targetSlots.Length)
            {
                Debug.Log("[PLINKO] All targets completed");
                OnTargetsCompleted?.Invoke();
                return;
            }

            Debug.Log($"[PLINKO] Starting ball drop (target index: {currentTargetIndex}, controlled: {useControlledOutcome})");
            StartCoroutine(DropBallCoroutine());
        }

        private IEnumerator DropBallCoroutine()
        {
            isBallInPlay = true;
            OnBallStateChanged?.Invoke(true);

            yield return spawnWait;

            SpawnBall();
        }

        public void Drop10Balls()
        {
            Debug.Log("[PLINKO] Drop10Balls called");
            if (isMultiDropInProgress)
            {
                Debug.LogWarning("[PLINKO] Multi-drop already in progress, ignoring request");
                return;
            }

            if (isBallInPlay)
            {
                Debug.LogWarning("[PLINKO] Ball already in play, ignoring drop request");
                return;
            }

            StartCoroutine(Drop10BallsCoroutine());
        }

        private IEnumerator Drop10BallsCoroutine()
        {
            isMultiDropInProgress = true;
            isBallInPlay = true;
            OnBallStateChanged?.Invoke(true);
            Debug.Log("[PLINKO] Starting multi-drop sequence: 10 balls");

            // Drop all 10 balls with interval delay
            for (int i = 0; i < 10; i++)
            {
                Debug.Log($"[PLINKO] Dropping ball {i + 1}/10");

                yield return spawnWait;

                SpawnBallForMultiDrop();
                ballsInPlayCount++;

                // Wait for interval before dropping next ball
                if (i < 9)
                {
                    yield return intervalWait;
                }
            }

            Debug.Log("[PLINKO] All 10 balls dropped, waiting for them to land...");

            // Wait for all balls to land
            while (ballsInPlayCount > 0)
            {
                yield return null;
            }

            isMultiDropInProgress = false;
            isBallInPlay = false;
            OnBallStateChanged?.Invoke(false);
            Debug.Log("[PLINKO] Multi-drop sequence completed");
        }

        private void SpawnBallForMultiDrop()
        {
            Vector3 dropPos = board.GetDropPosition();
            dropPos.x = 0f;
            Debug.Log($"[PLINKO] Spawning ball at position: {dropPos}");

            PlinkoBall ball = GetBallFromPool();
            ball.transform.SetParent(null);
            ball.transform.position = dropPos;
            ball.transform.rotation = Quaternion.identity;

            activeBalls.Add(ball);

            float slotY = board.GetSlotYPosition();
            float pegSpace = board.PegSpacing;
            float rowSpace = board.RowSpacing;

            if (useControlledOutcome && currentTargetIndex < targetSlots.Length)
            {
                int targetSlot = targetSlots[currentTargetIndex];
                int maxSlot = board.SlotCount - 1;

                if (targetSlot < 0 || targetSlot > maxSlot)
                {
                    Debug.LogWarning($"[PLINKO] Target slot {targetSlot} out of range, clamping to valid range [0, {maxSlot}]");
                    targetSlot = targetSlot < 0 ? 0 : maxSlot;
                }

                float slotX = board.GetSlotXPosition(targetSlot);
                Debug.Log($"[PLINKO] Controlled drop targeting slot {targetSlot} at X: {slotX}");
                ball.Initialize(targetSlot, slotX, board.PegRows, slotY, pegSpace, rowSpace);
                currentTargetIndex++;
            }
            else
            {
                Debug.Log("[PLINKO] Random drop initiated");
                ball.InitializeRandom(board.PegRows, slotY, pegSpace, rowSpace, board.SlotCount, board.GetSlotXPosition);
            }
        }

        private void SpawnBall()
        {
            // Always drop from center (above middle top peg)
            Vector3 dropPos = board.GetDropPosition();
            dropPos.x = 0f;
            Debug.Log($"[PLINKO] Spawning ball at position: {dropPos}");

            currentBall = GetBallFromPool();
            currentBall.transform.SetParent(null);
            currentBall.transform.position = dropPos;
            currentBall.transform.rotation = Quaternion.identity;

            float slotY = board.GetSlotYPosition();
            float pegSpace = board.PegSpacing;
            float rowSpace = board.RowSpacing;

            if (useControlledOutcome && currentTargetIndex < targetSlots.Length)
            {
                int targetSlot = targetSlots[currentTargetIndex];
                int maxSlot = board.SlotCount - 1;

                if (targetSlot < 0 || targetSlot > maxSlot)
                {
                    Debug.LogWarning($"[PLINKO] Target slot {targetSlot} out of range, clamping to valid range [0, {maxSlot}]");
                    targetSlot = targetSlot < 0 ? 0 : maxSlot;
                }

                float slotX = board.GetSlotXPosition(targetSlot);
                Debug.Log($"[PLINKO] Controlled drop targeting slot {targetSlot} at X: {slotX}");
                currentBall.Initialize(targetSlot, slotX, board.PegRows, slotY, pegSpace, rowSpace);
            }
            else
            {
                Debug.Log("[PLINKO] Random drop initiated");
                currentBall.InitializeRandom(board.PegRows, slotY, pegSpace, rowSpace, board.SlotCount, board.GetSlotXPosition);
            }
        }

        private void HandleBallLanded(int slotId, float multiplier, PlinkoBall landedBall)
        {
            Debug.Log($"[PLINKO] Ball landed in slot {slotId} (x{multiplier})");

            var result = new BallResult
            {
                targetSlot = -1,
                actualSlot = slotId,
                multiplier = multiplier,
                wasControlled = useControlledOutcome,
                hitTarget = false
            };
            results.Add(result);
            Debug.Log($"[PLINKO] Total results: {results.Count}");

            OnBallResult?.Invoke(slotId, multiplier);

            // Handle multi-drop mode
            if (isMultiDropInProgress)
            {
                ballsInPlayCount--;
                Debug.Log($"[PLINKO] Balls still in play: {ballsInPlayCount}, landedBall: {(landedBall != null ? landedBall.name : "null")}, activeBalls count: {activeBalls.Count}");

                // Return the specific ball that landed
                if (landedBall != null && activeBalls.Contains(landedBall))
                {
                    Debug.Log($"[PLINKO] Returning landed ball to pool: {landedBall.name}");
                    activeBalls.Remove(landedBall);
                    ReturnBallToPool(landedBall);
                }
                else if (landedBall != null)
                {
                    // Ball landed but wasn't in activeBalls - still return it
                    Debug.LogWarning($"[PLINKO] Ball {landedBall.name} landed but was not in activeBalls list - returning anyway");
                    ReturnBallToPool(landedBall);
                }
                else
                {
                    // Fallback: remove first active ball if landedBall is null
                    Debug.LogWarning("[PLINKO] landedBall was null! Using fallback removal");
                    if (activeBalls.Count > 0)
                    {
                        PlinkoBall fallbackBall = activeBalls[0];
                        activeBalls.RemoveAt(0);
                        ReturnBallToPool(fallbackBall);
                    }
                }
            }
            else
            {
                // Single ball mode
                if (useControlledOutcome)
                {
                    currentTargetIndex++;
                    Debug.Log($"[PLINKO] Target index advanced to {currentTargetIndex}/{targetSlots.Length}");
                }

                ReturnBallToPool(landedBall != null ? landedBall : currentBall);
                currentBall = null;

                isBallInPlay = false;
                OnBallStateChanged?.Invoke(false);

                if (useControlledOutcome && currentTargetIndex >= targetSlots.Length)
                {
                    Debug.Log("[PLINKO] All targets completed!");
                    OnTargetsCompleted?.Invoke();
                }
            }
        }

        public void ResetGame()
        {
            Debug.Log("[PLINKO] Game reset");
            StopAllCoroutines();
            currentTargetIndex = 0;
            results.Clear();
            isBallInPlay = false;
            isMultiDropInProgress = false;
            ballsInPlayCount = 0;

            if (currentBall != null)
            {
                ReturnBallToPool(currentBall);
                currentBall = null;
            }

            // Return all active balls to pool
            foreach (var ball in activeBalls)
            {
                ReturnBallToPool(ball);
            }
            activeBalls.Clear();

            Debug.Log($"[PLINKO] Reset complete - {targetSlots.Length} targets remaining");
        }
    }
}
