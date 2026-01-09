using UnityEngine;
using UnityEngine.UI;
using System.Text;

namespace Plinko
{
    public class PlinkoUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button dropButton;
        [SerializeField] private Button drop10Button;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text targetText;
        [SerializeField] private Text resultsText;
        [SerializeField] private Text remainingText;
        [SerializeField] private InputField targetInputField;
        [SerializeField] private Button setTargetsButton;
        [SerializeField] private Toggle controlledToggle;

        [Header("Auto-Create UI")]
        [SerializeField] private bool autoCreateUI = true;

        private GameManager gameManager;
        private Canvas canvas;
        private readonly StringBuilder resultsBuilder = new StringBuilder(512);
        private readonly StringBuilder tempBuilder = new StringBuilder(32);

        private void Start()
        {
            Debug.Log("[PLINKO] PlinkoUI Start");
            gameManager = GameManager.Instance;

            if (autoCreateUI)
            {
                Debug.Log("[PLINKO] Auto-creating UI");
                CreateUI();
            }

            BindEvents();
            UpdateUI();
            Debug.Log("[PLINKO] PlinkoUI initialization complete");
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        private void CreateUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("PlinkoCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create panel background
            GameObject panelObj = CreatePanel(canvasObj.transform, "UIPanel",
                new Vector2(250, 450), new Vector2(135, -185));

            // Create Drop Button
            dropButton = CreateButton(panelObj.transform, "DropButton", "DROP BALL",
                new Vector2(0, 175), new Vector2(200, 50));

            // Create Drop 10 Balls Button
            drop10Button = CreateButton(panelObj.transform, "Drop10Button", "DROP 10 BALLS",
                new Vector2(0, 115), new Vector2(200, 45));

            // Create Reset Button
            resetButton = CreateButton(panelObj.transform, "ResetButton", "RESET",
                new Vector2(0, 60), new Vector2(200, 40));

            // Create Target Text
            targetText = CreateText(panelObj.transform, "TargetText", "Next Target: -",
                new Vector2(0, 20), new Vector2(200, 30));

            // Create Remaining Text
            remainingText = CreateText(panelObj.transform, "RemainingText", "Remaining: 0",
                new Vector2(0, -10), new Vector2(200, 30));

            // Create Results Label
            CreateText(panelObj.transform, "ResultsLabel", "Results:",
                new Vector2(0, -45), new Vector2(200, 25));

            // Create Results Text (scrollable area)
            resultsText = CreateText(panelObj.transform, "ResultsText", "",
                new Vector2(0, -120), new Vector2(200, 120));
            resultsText.alignment = TextAnchor.UpperLeft;

            // Create Input Field for custom targets
            targetInputField = CreateInputField(panelObj.transform, "TargetInput",
                "0,5,8,15,3", new Vector2(0, -210), new Vector2(200, 35));

            // Create Set Targets Button
            setTargetsButton = CreateButton(panelObj.transform, "SetTargetsButton", "SET TARGETS",
                new Vector2(0, -255), new Vector2(200, 35));

            // Create Controlled Outcome Toggle
            controlledToggle = CreateToggle(panelObj.transform, "ControlledToggle", "Controlled Outcome",
                new Vector2(0, -295), new Vector2(200, 30));
            controlledToggle.isOn = gameManager != null && gameManager.UseControlledOutcome;

            // Bind button events
            dropButton.onClick.AddListener(OnDropClicked);
            drop10Button.onClick.AddListener(OnDrop10Clicked);
            resetButton.onClick.AddListener(OnResetClicked);
            setTargetsButton.onClick.AddListener(OnSetTargetsClicked);
            controlledToggle.onValueChanged.AddListener(OnControlledToggleChanged);
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;

            var image = panel.AddComponent<Image>();
            image.color = new Color(0.1f, 0.15f, 0.25f, 0.9f);

            return panel;
        }

        private Button CreateButton(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.9f);

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var buttonText = textObj.AddComponent<Text>();
            buttonText.text = text;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 18;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;

            return button;
        }

        private Text CreateText(Transform parent, string name, string content, Vector2 position, Vector2 size)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            var rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var text = textObj.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return text;
        }

        private Toggle CreateToggle(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent, false);

            var rect = toggleObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var toggle = toggleObj.AddComponent<Toggle>();

            // Create background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(toggleObj.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(24, 24);
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(0, 0.5f);
            bgRect.pivot = new Vector2(0, 0.5f);
            bgRect.anchoredPosition = new Vector2(0, 0);

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.3f);

            // Create checkmark
            GameObject checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(bgObj.transform, false);

            var checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = new Vector2(-6, -6);

            var checkImage = checkObj.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.8f, 0.4f);

            // Create label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(toggleObj.transform, false);

            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = new Vector2(30, 0);
            labelRect.offsetMax = Vector2.zero;

            var labelText = labelObj.AddComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = Color.white;

            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;

            return toggle;
        }

        private InputField CreateInputField(Transform parent, string name, string placeholder,
            Vector2 position, Vector2 size)
        {
            GameObject inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent, false);

            var rect = inputObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = inputObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.3f);

            var inputField = inputObj.AddComponent<InputField>();

            // Create text component
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-10, 0);

            var inputText = textObj.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.fontSize = 14;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.color = Color.white;
            inputText.supportRichText = false;

            inputField.textComponent = inputText;
            inputField.text = placeholder;

            return inputField;
        }

        private void BindEvents()
        {
            if (gameManager != null)
            {
                gameManager.OnBallResult += HandleBallResult;
                gameManager.OnBallStateChanged += HandleBallStateChanged;
                gameManager.OnTargetsCompleted += HandleTargetsCompleted;
            }
        }

        private void UnbindEvents()
        {
            if (gameManager != null)
            {
                gameManager.OnBallResult -= HandleBallResult;
                gameManager.OnBallStateChanged -= HandleBallStateChanged;
                gameManager.OnTargetsCompleted -= HandleTargetsCompleted;
            }
        }

        private void OnDropClicked()
        {
            Debug.Log("[PLINKO] UI: Drop button clicked");
            if (gameManager != null)
            {
                gameManager.DropBall();
            }
        }

        private void OnDrop10Clicked()
        {
            Debug.Log("[PLINKO] UI: Drop 10 button clicked");
            if (gameManager != null)
            {
                gameManager.Drop10Balls();
            }
        }

        private void OnResetClicked()
        {
            Debug.Log("[PLINKO] UI: Reset button clicked");
            if (gameManager != null)
            {
                gameManager.ResetGame();
                resultsBuilder.Clear();
                UpdateUI();
            }
        }

        private void OnSetTargetsClicked()
        {
            Debug.Log("[PLINKO] UI: Set targets button clicked");
            if (targetInputField == null || gameManager == null) return;

            string input = targetInputField.text;
            Debug.Log($"[PLINKO] UI: Parsing targets from input: '{input}'");
            string[] parts = input.Split(',');

            int[] targets = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int slot))
                {
                    targets[i] = slot;
                }
                else
                {
                    targets[i] = 0;
                }
            }

            Debug.Log($"[PLINKO] UI: Parsed {targets.Length} targets: [{string.Join(", ", targets)}]");
            gameManager.SetTargetSlots(targets);
            resultsBuilder.Clear();
            UpdateUI();
        }

        private void OnControlledToggleChanged(bool isOn)
        {
            Debug.Log($"[PLINKO] UI: Controlled toggle changed to: {isOn}");
            if (gameManager != null)
            {
                gameManager.SetControlledOutcome(isOn);
                UpdateUI();
            }
        }

        private void HandleBallResult(int slotId, float multiplier)
        {
            Debug.Log($"[PLINKO] UI: Received ball result - Slot {slotId}, Multiplier x{multiplier}");
            // Build result line without allocation
            tempBuilder.Clear();
            tempBuilder.Append("Slot ").Append(slotId).Append(" (x").Append(multiplier).Append(")\n");

            // Insert at beginning
            resultsBuilder.Insert(0, tempBuilder.ToString());

            // Limit results display
            if (resultsBuilder.Length > 500)
            {
                resultsBuilder.Length = 500;
            }

            UpdateUI();
        }

        private void HandleBallStateChanged(bool inPlay)
        {
            Debug.Log($"[PLINKO] UI: Ball state changed - In play: {inPlay}");
            bool canDrop = !inPlay && (gameManager == null || !gameManager.IsMultiDropInProgress);
            if (dropButton != null)
            {
                dropButton.interactable = canDrop;
            }
            if (drop10Button != null)
            {
                drop10Button.interactable = canDrop;
            }
            UpdateUI();
        }

        private void HandleTargetsCompleted()
        {
            Debug.Log("[PLINKO] UI: All targets completed");
            if (targetText != null)
            {
                targetText.text = "All targets complete!";
            }
        }

        private void UpdateUI()
        {
            if (gameManager == null) return;

            // Update target text
            if (targetText != null)
            {
                int target = gameManager.CurrentTargetSlot;
                targetText.text = target >= 0 ? $"Next Target: Slot {target}" : "Mode: Random";
            }

            // Update remaining text
            if (remainingText != null)
            {
                remainingText.text = $"Remaining: {gameManager.RemainingTargets}";
            }

            // Update results text
            if (resultsText != null)
            {
                resultsText.text = resultsBuilder.ToString();
            }

            // Update drop button states
            bool canDrop = !gameManager.IsBallInPlay && !gameManager.IsMultiDropInProgress;
            if (dropButton != null)
            {
                dropButton.interactable = canDrop;
            }
            if (drop10Button != null)
            {
                drop10Button.interactable = canDrop;
            }
        }
    }
}
