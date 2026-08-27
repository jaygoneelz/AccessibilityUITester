using UnityEditor;
using UnityEngine;
using AccessibilityTester.Runtime.ContrastMeter;
using AccessibilityTester.Editor.Core;

namespace AccessibilityTester.Editor.ContrastMeter
{
    /// <summary>
    /// Bridges the CoreManager event bus (Editor) to ContrastMeterRuntime
    /// (a Runtime MonoBehaviour). Contrast Meter only functions in Play
    /// Mode, since it depends on the active EventSystem/GraphicRaycaster.
    /// Toggling creates/enables or disables a dedicated GameObject holding
    /// the runtime component. Starts disabled on creation.
    /// </summary>
    public class ContrastMeterModule
    {
        private const string RuntimeObjectName = "_ContrastMeterRuntime";

        private readonly CoreManager _coreManager;
        private ContrastMeterRuntime _runtimeInstance;

        public ContrastMeterModule(CoreManager coreManager)
        {
            _coreManager = coreManager;
            _coreManager.OnToggleContrastMeterRequested += HandleToggle;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public bool IsActive => _runtimeInstance != null && _runtimeInstance.enabled;

        private void HandleToggle()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AccessibilityTester] Contrast Meter only works in Play Mode " +
                                  "(it relies on the active EventSystem/GraphicRaycaster). Enter Play Mode first.");
                return;
            }

            EnsureInstanceExists();
            _runtimeInstance.enabled = !_runtimeInstance.enabled;
        }

        private void EnsureInstanceExists()
        {
            if (_runtimeInstance != null) return;

            GameObject host = GameObject.Find(RuntimeObjectName) ?? new GameObject(RuntimeObjectName);
            _runtimeInstance = host.GetComponent<ContrastMeterRuntime>()
                                ?? host.AddComponent<ContrastMeterRuntime>();
            // Explicitly start disabled — the toggle is the only thing
            // that should turn this on.
            _runtimeInstance.enabled = false;
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _runtimeInstance = null;
            }
        }

        public void Dispose()
        {
            _coreManager.OnToggleContrastMeterRequested -= HandleToggle;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        }
    }
}