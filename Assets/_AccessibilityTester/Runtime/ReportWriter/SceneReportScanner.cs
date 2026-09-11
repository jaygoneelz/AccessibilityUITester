using System;
using System.Collections.Generic;
using System.Linq;
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
    /// produces a SceneReport.
    ///
    /// Background resolution order (each step reports its own confidence
    /// via ElementReport.backgroundConfidence):
    /// 1. Direct ancestor Graphic.color, if not a near-white tint.
    /// 2. If the ancestor is a white-tinted Image with a Sprite, attempt
    ///    texture-pixel sampling. Requires Read/Write Enabled; falls
    ///    through if not (a genuine, disclosed limitation: UI content is
    ///    not part of what a Camera renders, so this cannot be solved by
    ///    camera compositing).
    /// 3. If no ancestor Graphic exists, composite every enabled Camera
    ///    in the scene (sorted by m_Depth, respecting each camera's own
    ///    clear flags) to an offscreen texture and sample the actual
    ///    rendered pixel at the element's screen position. This correctly
    ///    handles both single-camera scenes and legacy multi-camera
    ///    layered rigs (e.g. a parallax-background camera + gameplay
    ///    camera + UI-backdrop camera + UI camera, as found in Red
    ///    Runner's Play scene). A Canvas's own worldCamera is deliberately
    ///    NOT used to narrow this list — worldCamera tells Unity which
    ///    camera to raycast/project the Canvas from, not which camera(s)
    ///    contributed to what is visually behind it. A worldCamera with
    ///    DepthOnly clear flags paints nothing of its own; compositing it
    ///    alone would reproduce the exact single-camera bug this method
    ///    exists to fix.
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
                    return (ancestor.color, ancestor.gameObject.name,
                        "Low (white tint on sprite, texture not Read/Write Enabled - UI content cannot be camera-rendered)");
                }

                return (ancestor.color, ancestor.gameObject.name, "High (direct Graphic.color)");
            }

            List<Camera> compositeCameras = ResolveCompositeCameras();
            if (compositeCameras.Count > 0 && go.GetComponent<RectTransform>() != null)
            {
                if (TryRenderAndSampleComposite(compositeCameras, go.GetComponent<RectTransform>(), canvas, out Color rendered))
                {
                    string label = compositeCameras.Count > 1
                        ? $"Camera Render ({compositeCameras.Count}-camera composite)"
                        : "Camera Render";
                    return (rendered, label, "High (camera(s) rendered to offscreen texture, actual composited pixel sampled)");
                }
            }

            if (compositeCameras.Count > 0)
            {
                Camera fallbackCam = compositeCameras[0];
                string confidence = fallbackCam.clearFlags == CameraClearFlags.SolidColor
                    ? "Medium (camera background fallback, solid colour clear flag)"
                    : "Low (camera background fallback, camera clear flag is not solid colour - not representative)";
                return (fallbackCam.backgroundColor, "Camera Background (fallback)", confidence);
            }

            return (Color.white, "None (no ancestor Graphic, no Camera of any kind found in scene)", "Low (hardcoded white default)");
        }

        /// <summary>
        /// Finds every enabled, active Camera in the scene, sorted by
        /// depth ascending (lowest depth renders first). Always returns
        /// the full scene camera stack — deliberately ignores any
        /// Canvas's worldCamera assignment, since some rigs (e.g. legacy
        /// Built-in RP layered setups) compose the final visible image
        /// from multiple independent cameras, and a Canvas's worldCamera
        /// may be only the topmost of those (often DepthOnly-cleared,
        /// painting nothing on its own).
        /// </summary>
        private static List<Camera> ResolveCompositeCameras()
        {
            Camera[] allCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            return allCameras
                .Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy)
                .OrderBy(c => c.depth)
                .ToList();
        }

        /// <summary>
        /// Renders each camera in the list, in order, to the same
        /// offscreen texture (respecting each camera's own clear flags,
        /// so lower-depth cameras establish the base image and
        /// higher-depth cameras composite on top without wiping it),
        /// then samples the average pixel colour within the element's
        /// on-screen rect from the final composited result.
        /// </summary>
        private static bool TryRenderAndSampleComposite(List<Camera> cameras, RectTransform rect, Canvas canvas, out Color sampledColor)
        {
            sampledColor = Color.white;
            if (rect == null || cameras.Count == 0) return false;

            int width = Mathf.Max(64, Screen.width);
            int height = Mathf.Max(64, Screen.height);

            var originalTargets = new RenderTexture[cameras.Count];
            RenderTexture sharedRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            try
            {
                for (int i = 0; i < cameras.Count; i++)
                {
                    originalTargets[i] = cameras[i].targetTexture;
                    cameras[i].targetTexture = sharedRT;
                }

                foreach (var cam in cameras)
                {
                    cam.Render();
                }

                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);

                Camera referenceCam = cameras[cameras.Count - 1];
                bool isOverlay = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay;

                Vector2 minScreen, maxScreen;
                if (isOverlay)
                {
                    minScreen = new Vector2(corners[0].x, corners[0].y);
                    maxScreen = new Vector2(corners[2].x, corners[2].y);
                }
                else
                {
                    Vector3 p0 = RectTransformUtility.WorldToScreenPoint(referenceCam, corners[0]);
                    Vector3 p2 = RectTransformUtility.WorldToScreenPoint(referenceCam, corners[2]);
                    minScreen = new Vector2(Mathf.Min(p0.x, p2.x), Mathf.Min(p0.y, p2.y));
                    maxScreen = new Vector2(Mathf.Max(p0.x, p2.x), Mathf.Max(p0.y, p2.y));
                }

                int x = Mathf.Clamp(Mathf.RoundToInt(minScreen.x), 0, width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(minScreen.y), 0, height - 1);
                int w = Mathf.Clamp(Mathf.RoundToInt(maxScreen.x - minScreen.x), 1, width - x);
                int h = Mathf.Clamp(Mathf.RoundToInt(maxScreen.y - minScreen.y), 1, height - y);

                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = sharedRT;

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
                for (int i = 0; i < cameras.Count; i++)
                {
                    if (cameras[i] != null) cameras[i].targetTexture = originalTargets[i];
                }
                RenderTexture.ReleaseTemporary(sharedRT);
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