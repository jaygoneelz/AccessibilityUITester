#if ACCESSIBILITY_TESTER_URP
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using AccessibilityTester.Runtime.ShaderController;
using AccessibilityTester.Editor.Core;

namespace AccessibilityTester.Editor.ShaderController
{
    /// <summary>
    /// Bridges the CoreManager event bus (Editor) to the CVDRendererFeature (Runtime),
    /// which lives as a sub-asset inside a UniversalRendererData asset.
    /// </summary>
    public class ShaderControllerModule
    {
        private readonly CoreManager _coreManager;
        private UniversalRendererData _rendererData;
        private CVDRendererFeature _cvdFeature;

        public ShaderControllerModule(CoreManager coreManager, UniversalRendererData rendererData)
        {
            _coreManager = coreManager;
            _rendererData = rendererData;
            _coreManager.OnToggleCvdSimulationRequested += HandleToggleCvd;
            _coreManager.OnToggleBlurRequested += HandleToggleBlur;
            FindFeature();
        }

        public void SetRendererData(UniversalRendererData rendererData)
        {
            _rendererData = rendererData;
            FindFeature();
        }

        private void FindFeature()
        {
            _cvdFeature = null;
            if (_rendererData == null) return;

            foreach (var feature in _rendererData.rendererFeatures)
            {
                if (feature is CVDRendererFeature cvd)
                {
                    _cvdFeature = cvd;
                    return;
                }
            }
        }

        private void HandleToggleCvd()
        {
            if (_cvdFeature == null)
            {
                Debug.LogWarning("[AccessibilityTester] No CVDRendererFeature found on the assigned Renderer Data asset. Drag the correct Renderer asset into the tool window first.");
                return;
            }

            _cvdFeature.simulationType = _cvdFeature.simulationType switch
            {
                CVDRendererFeature.CVDType.None => CVDRendererFeature.CVDType.Protanopia,
                CVDRendererFeature.CVDType.Protanopia => CVDRendererFeature.CVDType.Deuteranopia,
                CVDRendererFeature.CVDType.Deuteranopia => CVDRendererFeature.CVDType.Tritanopia,
                _ => CVDRendererFeature.CVDType.None
            };

            MarkDirtyAndRepaint();
        }

        private void HandleToggleBlur()
        {
            if (_cvdFeature == null)
            {
                Debug.LogWarning("[AccessibilityTester] No CVDRendererFeature found on the assigned Renderer Data asset. Drag the correct Renderer asset into the tool window first.");
                return;
            }

            _cvdFeature.blurEnabled = !_cvdFeature.blurEnabled;
            MarkDirtyAndRepaint();
        }

        private void MarkDirtyAndRepaint()
        {
            EditorUtility.SetDirty(_cvdFeature);
            EditorUtility.SetDirty(_rendererData);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }

        public string CurrentStateLabel =>
            _cvdFeature != null ? _cvdFeature.simulationType.ToString() : "No feature assigned";

        public bool BlurEnabled => _cvdFeature != null && _cvdFeature.blurEnabled;

        public void Dispose()
        {
            _coreManager.OnToggleCvdSimulationRequested -= HandleToggleCvd;
            _coreManager.OnToggleBlurRequested -= HandleToggleBlur;
        }
    }
}
#endif