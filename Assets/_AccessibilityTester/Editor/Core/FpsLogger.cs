using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AccessibilityTester.Editor.Core
{
    /// <summary>
    /// Editor-only frame-time logger. When enabled, records frame deltas
    /// during Play Mode, trims warm-up/cool-down frames, and writes a
    /// summary to the Console plus a CSV on exiting Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class FpsLogger
    {
        private const string EnabledPrefKey = "AccessibilityTester.FpsLoggerEnabled";

        private static readonly List<float> _frameTimesMs = new List<float>();
        private static double _lastTime;
        private static bool _wasPlaying;

        private const int TrimFrames = 30;
        private const float Target60FpsMs = 1000f / 60f;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, false);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        static FpsLogger()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            bool isPlaying = Application.isPlaying;

            if (!Enabled)
            {
                _wasPlaying = isPlaying;
                return;
            }

            if (isPlaying && !_wasPlaying)
            {
                _frameTimesMs.Clear();
                _lastTime = EditorApplication.timeSinceStartup;
            }
            else if (!isPlaying && _wasPlaying)
            {
                WriteResults();
            }
            else if (isPlaying)
            {
                double now = EditorApplication.timeSinceStartup;
                float deltaMs = (float)((now - _lastTime) * 1000.0);
                _lastTime = now;
                _frameTimesMs.Add(deltaMs);
            }

            _wasPlaying = isPlaying;
        }

        private static void WriteResults()
        {
            if (_frameTimesMs.Count < 10) return;

            if (_frameTimesMs.Count <= TrimFrames * 2) return;
            var trimmed = _frameTimesMs.GetRange(TrimFrames, _frameTimesMs.Count - TrimFrames * 2);

            float sum = 0f, min = float.MaxValue, max = 0f;
            int framesOver60fps = 0;
            var offenderIndices = new List<int>();

            for (int i = 0; i < trimmed.Count; i++)
            {
                float t = trimmed[i];
                sum += t;
                if (t < min) min = t;
                if (t > max) max = t;
                if (t > Target60FpsMs)
                {
                    framesOver60fps++;
                    if (offenderIndices.Count < 20) offenderIndices.Add(i);
                }
            }

            float avg = sum / trimmed.Count;
            float percentOver = 100f * framesOver60fps / trimmed.Count;

            string path = Path.Combine(Application.dataPath, "..", "fps_log.csv");
            using (var writer = new StreamWriter(path, false))
            {
                writer.WriteLine("frame_index,frame_time_ms,exceeds_60fps_threshold");
                for (int i = 0; i < trimmed.Count; i++)
                    writer.WriteLine($"{i},{trimmed[i]:F4},{(trimmed[i] > Target60FpsMs ? 1 : 0)}");
            }

            Debug.Log(
                $"[FpsLogger] {trimmed.Count} frames analysed (after trimming {TrimFrames} frames from each end). " +
                $"Avg: {avg:F3}ms ({1000f / avg:F1} fps) | Min: {min:F3}ms | Max: {max:F3}ms ({1000f / max:F1} fps worst-case) | " +
                $"Frames exceeding 16.67ms (60fps threshold): {framesOver60fps}/{trimmed.Count} ({percentOver:F4}%) | " +
                $"Exported to {path}"
            );

            if (offenderIndices.Count > 0)
            {
                Debug.Log($"[FpsLogger] First offending frame indices (up to 20): {string.Join(", ", offenderIndices)}");
            }
        }
    }
}