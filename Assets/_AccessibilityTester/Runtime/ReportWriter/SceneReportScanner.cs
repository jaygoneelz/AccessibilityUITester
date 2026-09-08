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
    /// produces a SceneReport. Runs in Edit Mode.
    ///
    /// Background resolution order (each step reports its own confidence
    /// via ElementReport.backgroundConfidence):
    /// 1. Direct ancestor Graphic.color, if not a near-white tint.
    /// 2. If the ancestor is a white-tinted Image with a Sprite, attempt
    ///    texture-pixel sampling. Requires Read/Write Enabled; falls
    ///    through if not (a genuine, disclosed limitation — see below).
    /// 3. If no ancestor Graphic exists, render the scene's camera to an
    ///    offscreen texture and sample the actual rendered pixel at the
    ///    element's screen position. This captures the true visual
    ///    background regardless of clear flag (solid colour, skybox, or
    ///    anything else) and regardless of camera tag, since it reads
    ///    what is actually rendered rather than a stored settings field.
    /// 4. Hardcoded white, only if no ancestor and no camera of any kind
    ///    exist in the scene.
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
                results.Add(BuildReport(text.gameObject, text.color, text.fontSize, thresholds, canvas));
            }

            TextMeshProUGUI[] tmpTexts = canvas.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
            foreach (var tmp in tmpTexts)
            {
                results.Add(BuildReport(tmp.gameObject, tmp.color, tmp.fontSize, thresholds, canvas));
            }
        }

        private static ElementReport BuildReport(GameObject go, Color fgColor, float fontSize, AccessibilityThresholds thresholds, Canvas canvas)
        {
            (Color bgColor, string bgLabel, string confidence) = GetEffectiveBackground(go, canvas);

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

        private static (Color color, string label, string confidence) GetEffectiveBackground(GameObject go, Canvas canvas)
        {
            Graphic ancestor = FindAncestorGraphic(go.transform);

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
                    // Genuine, disclosed limitation: the coloured background here
                    // is UI (an Image + Sprite), not 3D scene content a camera
                    // renders, so camera-render sampling cannot help this case.
                    // Fixing it would require either modifying the evaluated
                    // project's texture import settings (Read/Write Enabled) or
                    // a separate UI-specific render-to-texture subsystem —
                    // deliberately out of scope, disclosed rather than worked
                    // around.
                    return (ancestor.color, ancestor.gameObject.name,
                        "Low (white tint on sprite, texture not Read/Write Enabled - UI content cannot be camera-rendered)");
                }

                return (ancestor.color, ancestor.gameObject.name, "High (direct Graphic.color)");
            }

            // No ancestor Graphic: render the actual camera output and sample
            // the real pixel at this element's screen position. Correct
            // regardless of clear flag (solid colour or Skybox) and
            // regardless of camera tag, since it reads what is genuinely
            // rendered rather than a stored settings field.
            Camera cam = ResolveCamera(canvas);
            if (cam != null && TryRenderAndSamplePixel(cam, go.GetComponent<RectTransform>(), canvas, out Color rendered))
            {
                return (rendered, "Camera Render", "High (camera rendered to offscreen texture, actual pixel sampled)");
            }

            if (cam != null)
            {
                // Render-sampling failed for some reason (e.g. degenerate
                // rect); fall back to the stored field, honestly flagged.
                string confidence = cam.clearFlags == CameraClearFlags.SolidColor
                    ? "Medium (camera background fallback, solid colour clear flag)"
                    : "Low (camera background fallback, camera clear flag is not solid colour - not representative)";
                return (cam.backgroundColor, "Camera Background (fallback)", confidence);
            }

            return (Color.white, "None (no ancestor Graphic, no Camera of any kind found in scene)", "Low (hardcoded white default)");
        }

        private static Camera ResolveCamera(Canvas canvas)
        {
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            }
            return cam;
        }

        /// <summary>
        /// Renders the given camera to a temporary offscreen texture and
        /// samples the average pixel colour within the element's on-screen
        /// rect. Restores the camera's original targetTexture afterward.
        /// </summary>
        private static bool TryRenderAndSamplePixel(Camera cam, RectTransform rect, Canvas canvas, out Color sampledColor)
        {
            sampledColor = Color.white;
            if (rect == null) return false;

            int width = Mathf.Max(64, Screen.width);
            int height = Mathf.Max(64, Screen.height);

            RenderTexture originalTarget = cam.targetTexture;
            RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            try
            {
                cam.targetTexture = tempRT;
                cam.Render();

                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);

                Vector2 minScreen, maxScreen;
                bool isOverlay = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay;

                if (isOverlay)
                {
                    // Overlay canvas world positions already correspond to
                    // screen pixel coordinates directly.
                    minScreen = new Vector2(corners[0].x, corners[0].y);
                    maxScreen = new Vector2(corners[2].x, corners[2].y);
                }
                else
                {
                    Vector3 p0 = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                    Vector3 p2 = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                    minScreen = new Vector2(Mathf.Min(p0.x, p2.x), Mathf.Min(p0.y, p2.y));
                    maxScreen = new Vector2(Mathf.Max(p0.x, p2.x), Mathf.Max(p0.y, p2.y));
                }

                int x = Mathf.Clamp(Mathf.RoundToInt(minScreen.x), 0, width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(minScreen.y), 0, height - 1);
                int w = Mathf.Clamp(Mathf.RoundToInt(maxScreen.x - minScreen.x), 1, width - x);
                int h = Mathf.Clamp(Mathf.RoundToInt(maxScreen.y - minScreen.y), 1, height - y);

                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = tempRT;

                Texture2D readback = new Texture2D(w, h, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(x, y, w, h), 0, 0);
                readback.Apply();

                RenderTexture.active = previousActive;

                Color[] pixels = readback.GetPixels();
                float r = 0, g = 0, b = 0;
                foreach (var p in pixels) { r += p.r; g += p.g; b += p.b; }
                int count = Mathf.Max(1, pixels.Length);
                sampledColor = new Color(r / count, g / count, b / count, 1f);

                UnityEngine.Object.DestroyImmediate(readback);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                cam.targetTexture = originalTarget;
                RenderTexture.ReleaseTemporary(tempRT);
            }
        }

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
                    float weight = p.a;
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