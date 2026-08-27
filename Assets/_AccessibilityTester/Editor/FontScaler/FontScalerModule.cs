using UnityEngine;
using AccessibilityTester.Runtime.FontScaler;
using AccessibilityTester.Editor.Core;

namespace AccessibilityTester.Editor.FontScaler
{
    /// <summary>
    /// Bridges the CoreManager event bus (Editor) to FontScalerUtility
    /// (Runtime, pure logic). Edit Mode safe — no Play Mode required.
    /// </summary>
    public class FontScalerModule
    {
        private readonly CoreManager _coreManager;

        public FontScalerModule(CoreManager coreManager)
        {
            _coreManager = coreManager;
            _coreManager.OnFontScaleRequested += HandleFontScale;
        }

        public bool IsScaled => FontScalerUtility.IsScaled;

        private void HandleFontScale(float factor)
        {
            if (factor <= 0f)
            {
                Debug.LogWarning("[AccessibilityTester] Font scale factor must be greater than 0.");
                return;
            }

            int overlaps = FontScalerUtility.ApplyScale(factor);
            Debug.Log($"[AccessibilityTester] Font scale {factor:F2}x applied. {overlaps} overlapping element(s) found and highlighted magenta.");
        }

        public void Revert()
        {
            FontScalerUtility.Revert();
            Debug.Log("[AccessibilityTester] Font scale reverted to original sizes.");
        }

        public void Dispose()
        {
            _coreManager.OnFontScaleRequested -= HandleFontScale;
        }
    }
}