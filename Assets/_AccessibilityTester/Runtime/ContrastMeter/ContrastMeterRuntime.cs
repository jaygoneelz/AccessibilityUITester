using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AccessibilityTester.Runtime.Common;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AccessibilityTester.Runtime.ContrastMeter
{
    /// <summary>
    /// On left-click, raycasts the UI at the click position via the active
    /// EventSystem and computes an effective foreground/background colour
    /// by alpha-compositing every semi-transparent layer in the raycast
    /// stack (front-to-back) down to the camera's background colour as an
    /// opaque base. This handles overlapping/semi-transparent UI layers
    /// (see proposal Risk #2) rather than reading a single layer's colour
    /// in isolation. Displays a pass/fail badge at the click position.
    /// Requires an active EventSystem + GraphicRaycaster; Play Mode only.
    /// Mouse input is read via the new Input System when the host project
    /// has it enabled (ENABLE_INPUT_SYSTEM), falling back to the legacy
    /// Input class otherwise — allows this module to run unmodified in
    /// third-party projects regardless of their Active Input Handling
    /// setting (discovered when testing against Red Runner, which uses
    /// the legacy Input Manager only).
    /// </summary>
    public class ContrastMeterRuntime : MonoBehaviour
    {
        private bool _hasResult;
        private float _lastRatio;
        private bool _lastPass;
        private Vector2 _lastScreenPosition;
        private string _foregroundName;
        private string _backgroundLabel;

        // Created once on first OnGUI call rather than per-call — OnGUI can
        // fire multiple times per frame for as long as the badge is shown,
        // and allocating a new GUIStyle each time was needless GC pressure.
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;

        private void Update()
        {
            if (!WasLeftClickThisFrame(out Vector2 screenPosition)) return;

            EvaluateContrastAt(screenPosition);
        }

        private static bool WasLeftClickThisFrame(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
            screenPosition = default;
            return false;
#else
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
            screenPosition = default;
            return false;
#endif
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

            int foregroundIndex = -1;
            Graphic foreground = null;
            for (int i = 0; i < results.Count; i++)
            {
                var graphic = results[i].gameObject.GetComponent<Graphic>();
                if (graphic != null)
                {
                    foreground = graphic;
                    foregroundIndex = i;
                    break;
                }
            }

            if (foreground == null)
            {
                _hasResult = false;
                return;
            }

            var behindLayers = new List<Graphic>();
            for (int i = foregroundIndex + 1; i < results.Count; i++)
            {
                var graphic = results[i].gameObject.GetComponent<Graphic>();
                if (graphic != null) behindLayers.Add(graphic);
            }

            Graphic ancestor = GraphicHierarchyUtility.FindAncestorGraphic(foreground.transform);
            if (ancestor != null && !behindLayers.Contains(ancestor))
            {
                behindLayers.Add(ancestor);
            }

            Camera cam = Camera.main;
            Color opaqueBase = cam != null ? cam.backgroundColor : Color.black;

            Color effectiveBackground = opaqueBase;
            for (int i = behindLayers.Count - 1; i >= 0; i--)
            {
                Color layerColor = behindLayers[i].color;
                effectiveBackground = Color.Lerp(effectiveBackground, layerColor, layerColor.a);
            }

            Color fgColor = foreground.color;
            Color effectiveForeground = Color.Lerp(effectiveBackground, fgColor, fgColor.a);

            float ratio = WcagContrastUtility.ContrastRatio(effectiveForeground, effectiveBackground);

            _hasResult = true;
            _lastRatio = ratio;
            _lastPass = WcagContrastUtility.PassesNormalText(ratio);
            _lastScreenPosition = screenPosition;
            _foregroundName = foreground.gameObject.name;
            _backgroundLabel = behindLayers.Count > 0
                ? $"{behindLayers.Count} composited layer(s)"
                : (cam != null ? "Camera Background" : "Unknown (no Main Camera)");
        }

        private void OnGUI()
        {
            if (!_hasResult) return;

            const float badgeWidth = 420f;
            const float badgeHeight = 90f;

            EnsureStylesCreated();

            string status = _lastPass ? "PASS" : "FAIL";
            Color badgeColor = _lastPass ? new Color(0.15f, 0.55f, 0.15f) : new Color(0.75f, 0.15f, 0.15f);

            float guiY = Screen.height - _lastScreenPosition.y;
            Rect rect = new Rect(_lastScreenPosition.x + 16, guiY - 16, badgeWidth, badgeHeight);

            if (rect.xMax > Screen.width) rect.x = Screen.width - badgeWidth - 8;
            if (rect.yMax > Screen.height) rect.y = Screen.height - badgeHeight - 8;

            Color previousColor = GUI.color;
            GUI.color = badgeColor;
            GUI.Box(rect, string.Empty);
            GUI.color = previousColor;

            Rect titleRect = new Rect(rect.x + 14, rect.y + 8, rect.width - 28, 36);
            GUI.Label(titleRect, $"{status}  {_lastRatio:F2}:1", _titleStyle);

            Rect subtitleRect = new Rect(rect.x + 14, rect.y + 48, rect.width - 28, 32);
            GUI.Label(subtitleRect, $"{_foregroundName} on {_backgroundLabel}", _subtitleStyle);
        }

        private void EnsureStylesCreated()
        {
            if (_titleStyle != null) return;

            const int titleFontSize = 28;
            const int subtitleFontSize = 18;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = subtitleFontSize,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
            };
        }
    }
}