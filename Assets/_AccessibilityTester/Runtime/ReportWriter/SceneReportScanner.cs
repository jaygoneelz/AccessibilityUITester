using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AccessibilityTester.Runtime.ContrastMeter;

namespace AccessibilityTester.Runtime.ReportWriter
{
    /// <summary>
    /// Static-analysis scan of every Canvas in the currently loaded scene.
    /// Finds Text and TextMeshProUGUI elements, computes contrast using
    /// the best available background estimate, checks font size, and
    /// produces a SceneReport. Runs in Edit Mode — no EventSystem/raycast
    /// dependency, unlike Contrast Meter.
    ///
    /// Background resolution order (each step reports its own confidence,
    /// see ElementReport.backgroundConfidence):
    /// 1. Direct ancestor Graphic.color, if it is not a near-white tint.
    /// 2. If the ancestor is an Image with a Sprite and a white/near-white
    ///    tint, sample the sprite's texture pixels for an estimated
    ///    effective colour (handles the common "white-tinted coloured
    ///    sprite" UI pattern). Requires the texture to be marked Read/
    ///    Write Enabled; falls through if not.
    /// 3. Camera.main.backgroundColor, if no ancestor Graphic exists at
    ///    all. Only representative if the camera uses a solid colour
    ///    clear flag; flagged lower-confidence otherwise.
    /// 4. Hardcoded white, only if no ancestor and no Main Camera exist.
    /// </summary>
    public static class SceneReportScanner
    {
        private const float WhiteTintThreshold = 0.95f;

        public static SceneReport Scan(AccessibilityThresholds thresholds)
        {
            var report = new SceneReport
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                scanTimestampUtc = DateTime.UtcNow.ToString("o"),
                minContrastRatioUsed = thresholds.minContrastRatio,
                minFontSizePointUsed = thresholds.minFontSizePoint
            };

            var elements = new List<ElementReport>();

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                ScanCanvas(canvas, thresholds, elements);
            }

            report.elements = elements.ToArray();
            report.totalElementsScanned = elements.Count;
            report.totalFailures = elements.FindAll(e => !e.contrastPass || !e.fontSizePass).Count;

            return report;
        }

        private static void ScanCanvas(Canvas canvas, AccessibilityThresholds thresholds, List<ElementReport> results)
        {
            Text[] legacyTexts = canvas.GetComponentsInChildren<Text>(includeInactive: true);
            foreach (var text in legacyTexts)
            {
                results.Add(BuildReport(text.gameObject, text.color, text.fontSize, thresholds));
            }

            TextMeshProUGUI[] tmpTexts = canvas.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
            foreach (var tmp in tmpTexts)
            {
                results.Add(BuildReport(tmp.gameObject, tmp.color, tmp.fontSize, thresholds));
            }
        }

        private static ElementReport BuildReport(GameObject go, Color fgColor, float fontSize, AccessibilityThresholds thresholds)
        {
            (Color bgColor, string bgLabel, string confidence) = GetEffectiveBackground(go.transform);

            float ratio = WcagContrastUtility.ContrastRatio(fgColor, bgColor);
            bool contrastPass = ratio >= thresholds.minContrastRatio;
            bool fontPass = fontSize >= thresholds.minFontSizePoint;

            return new ElementReport
            {
                elementName = go.name,
                hierarchyPath = GetHierarchyPath(go.transform),
                sceneName = go.scene.name,
                contrastRatio = ratio,
                contrastPass = contrastPass,
                fontSizePoint = fontSize,
                fontSizePass = fontPass,
                foregroundColorHex = ColorToHex(fgColor),
                backgroundColorHex = ColorToHex(bgColor),
                backgroundSourceLabel = bgLabel,
                backgroundConfidence = confidence
            };
        }

        private static (Color color, string label, string confidence) GetEffectiveBackground(Transform start)
        {
            Graphic ancestor = FindAncestorGraphic(start);

            if (ancestor != null)
            {
                bool isWhiteTint = ancestor.color.r > WhiteTintThreshold
                                 && ancestor.color.g > WhiteTintThreshold
                                 && ancestor.color.b > WhiteTintThreshold;

                if (isWhiteTint && ancestor is Image image && image.sprite != null)
                {
                    if (TryGetAverageSpriteColor(image.sprite, out Color sampled))
                    {
                        return (sampled, ancestor.gameObject.name,
                            "Estimated (sprite texture sampled, ancestor tint was white)");
                    }
                    return (ancestor.color, ancestor.gameObject.name,
                        "Low (white tint on sprite, texture not Read/Write Enabled)");
                }

                return (ancestor.color, ancestor.gameObject.name, "High (direct Graphic.color)");
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                string confidence = cam.clearFlags == CameraClearFlags.SolidColor
                    ? "Medium (camera background fallback, solid colour clear flag)"
                    : "Low (camera background fallback, camera clear flag is not solid colour - not representative)";
                return (cam.backgroundColor, "Camera Background", confidence);
            }

            return (Color.white, "None (no ancestor Graphic, no Main Camera found)", "Low (hardcoded white default)");
        }

        /// <summary>
        /// Attempts to compute the alpha-weighted average colour of a
        /// sprite's texture region. Returns false if the texture is not
        /// marked Read/Write Enabled (a per-asset import setting this
        /// scanner does not modify, since changing it would alter the
        /// evaluated third-party project's asset configuration).
        /// </summary>
        private static bool TryGetAverageSpriteColor(Sprite sprite, out Color avgColor)
        {
            avgColor = Color.white;
            if (sprite == null || sprite.texture == null) return false;

            Texture2D tex = sprite.texture;
            if (!tex.isReadable) return false;

            Rect rect = sprite.textureRect;
            int x = Mathf.FloorToInt(rect.x);
            int y = Mathf.FloorToInt(rect.y);
            int w = Mathf.Max(1, Mathf.FloorToInt(rect.width));
            int h = Mathf.Max(1, Mathf.FloorToInt(rect.height));

            try
            {
                Color[] pixels = tex.GetPixels(x, y, w, h);
                float r = 0, g = 0, b = 0, totalWeight = 0;
                foreach (var p in pixels)
                {
                    float weight = p.a; // ignore fully transparent pixels
                    r += p.r * weight;
                    g += p.g * weight;
                    b += p.b * weight;
                    totalWeight += weight;
                }
                if (totalWeight <= 0f) return false;

                avgColor = new Color(r / totalWeight, g / totalWeight, b / totalWeight, 1f);
                return true;
            }
            catch
            {
                return false;
            }
        }

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

        private static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            Transform current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static string ColorToHex(Color c)
        {
            return $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";
        }
    }
}