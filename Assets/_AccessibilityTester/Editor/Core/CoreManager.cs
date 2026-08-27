using System;

namespace AccessibilityTester.Editor.Core
{
    /// <summary>
    /// Central dispatcher. Decouples the EditorWindow (UI) from individual
    /// modules (Shader Controller, Contrast Meter, Report Writer, Font Scaler).
    /// Modules subscribe to the events they care about in their own constructor.
    /// </summary>
    public class CoreManager : IDisposable
    {
        public event Action OnToggleCvdSimulationRequested;
        public event Action OnToggleBlurRequested;
        public event Action OnToggleContrastMeterRequested;
        public event Action OnGenerateReportRequested;
        public event Action<float> OnFontScaleRequested;

        public void RequestToggleCvdSimulation() => OnToggleCvdSimulationRequested?.Invoke();
        public void RequestToggleBlur() => OnToggleBlurRequested?.Invoke();
        public void RequestToggleContrastMeter() => OnToggleContrastMeterRequested?.Invoke();
        public void RequestGenerateReport() => OnGenerateReportRequested?.Invoke();
        public void RequestFontScale(float factor) => OnFontScaleRequested?.Invoke(factor);

        public void Dispose()
        {
            OnToggleCvdSimulationRequested = null;
            OnToggleBlurRequested = null;
            OnToggleContrastMeterRequested = null;
            OnGenerateReportRequested = null;
            OnFontScaleRequested = null;
        }
    }
}