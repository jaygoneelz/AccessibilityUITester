using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AccessibilityTester.Runtime.ContrastMeter
{
    /// <summary>
    /// On left-click, raycasts the UI at the click position via the active
    /// EventSystem, finds the clicked Graphic and its background colour
    /// (nearest ancestor Graphic, falling back to the camera's background
    /// colour if no ancestor Graphic exists), computes the WCAG contrast
    /// ratio, and displays a pass/fail badge at the click position.
    /// Requires an active EventSystem + GraphicRaycaster; Play Mode only.
    /// </summary>
    public class ContrastMeterRuntime : MonoBehaviour
    {
        private bool _hasResult;
        private float _lastRatio;
        private bool _lastPass;
        private Vector2 _lastScreenPosition;
        private string _foregroundName;
        private string _backgroundName;

        private void Update()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            EvaluateContrastAt(Mouse.current.position.ReadValue());
        }

        private void EvaluateContrastAt(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                Debug.LogWarning("[AccessibilityTester] No EventSystem in scene — Contrast Meter cannot raycast UI.");
                return;
            }

            var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            Graphic foreground = null;
            foreach (var result in results)
            {
                foreground = result.gameObject.GetComponent<Graphic>();
                if (foreground != null) break;
            }

            if (foreground == null)
            {
                _hasResult = false;
                return;
            }

            Graphic backgroundGraphic = FindAncestorGraphic(foreground.transform);
            Color backgroundColor;
            string backgroundLabel;

            if (backgroundGraphic != null)
            {
                backgroundColor = backgroundGraphic.color;
                backgroundLabel = backgroundGraphic.gameObject.name;
            }
            else
            {
                // No coloured ancestor in the UI hierarchy — fall back to
                // the main camera's background colour as the effective
                // backdrop behind this element.
                Camera cam = Camera.main;
                backgroundColor = cam != null ? cam.backgroundColor : Color.black;
                backgroundLabel = cam != null ? "Camera Background" : "Unknown (no Main Camera)";
            }

            float ratio = WcagContrastUtility.ContrastRatio(foreground.color, backgroundColor);

            _hasResult = true;
            _lastRatio = ratio;
            _lastPass = WcagContrastUtility.PassesNormalText(ratio);
            _lastScreenPosition = screenPosition;
            _foregroundName = foreground.gameObject.name;
            _backgroundName = backgroundLabel;
        }

        /// <summary>
        /// Walks up the hierarchy from the clicked element (excluding
        /// itself) for the nearest ancestor Graphic, treated as background.
        /// Returns null if none exists (e.g. element sits directly on
        /// Canvas with no coloured panel behind it).
        /// </summary>
        private static Graphic FindAncestorGraphic(Transform start)
        {
            Transform current = start.parent;
            while (current != null)
            {
                var graphic = current.GetComponent<Graphic>();
                if (graphic != null) return graphic;
                current = current.parent;
            }
            return null;
        }

        private void OnGUI()
        {
            if (!_hasResult) return;

            string status = _lastPass ? "PASS" : "FAIL";
            Color badgeColor = _lastPass ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            string label = $"{status}  {_lastRatio:F2}:1\n{_foregroundName} on {_backgroundName}";

            float guiY = Screen.height - _lastScreenPosition.y;
            Rect rect = new Rect(_lastScreenPosition.x + 12, guiY - 12, 220, 40);

            Color previousColor = GUI.color;
            GUI.color = badgeColor;
            GUI.Box(rect, string.Empty);
            GUI.color = previousColor;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, label, labelStyle);
        }
    }
}