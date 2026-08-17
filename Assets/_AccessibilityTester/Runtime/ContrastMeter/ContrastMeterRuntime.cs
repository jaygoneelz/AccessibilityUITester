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

            const float badgeWidth = 420f;
            const float badgeHeight = 90f;
            const int titleFontSize = 28;
            const int subtitleFontSize = 18;

            string status = _lastPass ? "PASS" : "FAIL";
            Color badgeColor = _lastPass ? new Color(0.15f, 0.55f, 0.15f) : new Color(0.75f, 0.15f, 0.15f);

            float guiY = Screen.height - _lastScreenPosition.y;
            Rect rect = new Rect(_lastScreenPosition.x + 16, guiY - 16, badgeWidth, badgeHeight);

            // Keep the badge fully on-screen if the click is near an edge.
            if (rect.xMax > Screen.width) rect.x = Screen.width - badgeWidth - 8;
            if (rect.yMax > Screen.height) rect.y = Screen.height - badgeHeight - 8;

            Color previousColor = GUI.color;
            GUI.color = badgeColor;
            GUI.Box(rect, string.Empty);
            GUI.color = previousColor;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };
            Rect titleRect = new Rect(rect.x + 14, rect.y + 8, rect.width - 28, 36);
            GUI.Label(titleRect, $"{status}  {_lastRatio:F2}:1", titleStyle);

            GUIStyle subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = subtitleFontSize,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
            };
            Rect subtitleRect = new Rect(rect.x + 14, rect.y + 48, rect.width - 28, 32);
            GUI.Label(subtitleRect, $"{_foregroundName} on {_backgroundName}", subtitleStyle);
        }
    }
}