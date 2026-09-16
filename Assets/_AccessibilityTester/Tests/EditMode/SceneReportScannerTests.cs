using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using AccessibilityTester.Runtime.ReportWriter;

namespace AccessibilityTester.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for SceneReportScanner's background-resolution
    /// paths and scan behaviour. Each test builds a minimal scene graph by
    /// hand and tears it down afterward, so tests don't leak Canvases into
    /// each other (Scan() finds every Canvas in the currently loaded scene).
    /// </summary>
    public class SceneReportScannerTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private AccessibilityThresholds _thresholds;

        [SetUp]
        public void SetUp()
        {
            _thresholds = ScriptableObject.CreateInstance<AccessibilityThresholds>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            Object.DestroyImmediate(_thresholds);
        }

        private GameObject Spawn(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            _spawned.Add(go);
            return go;
        }

        private Canvas CreateCanvas()
        {
            Canvas canvas = Spawn("Canvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }

        private static ElementReport FindElement(SceneReport report, string name)
        {
            foreach (var element in report.elements)
            {
                if (element.elementName == name) return element;
            }
            return null;
        }

        [Test]
        public void Scan_ElementWithAncestorGraphic_UsesAncestorColorDirectly()
        {
            Canvas canvas = CreateCanvas();

            GameObject panelGO = Spawn("Panel", canvas.transform);
            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = Color.blue; // not a white tint, so used directly rather than sampled

            GameObject textGO = Spawn("Label", panelGO.transform);
            var text = textGO.AddComponent<Text>();
            text.color = Color.white;
            text.fontSize = 20;
            textGO.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 30);

            SceneReport report = SceneReportScanner.Scan(_thresholds);

            ElementReport element = FindElement(report, "Label");
            Assert.IsNotNull(element, "Expected a report entry for the Label element.");
            Assert.AreEqual("High (direct Graphic.color)", element.backgroundConfidence);
            Assert.AreEqual("#0000FF", element.backgroundColorHex);
            Assert.AreEqual("Panel", element.backgroundSourceLabel);
        }

        [Test]
        public void Scan_ElementWithNoAncestorGraphic_FallsBackToCameraBackground()
        {
            // A Canvas GameObject is not itself a Graphic, so a text element
            // parented directly under it (no Image/other Graphic ancestor
            // in between) has no ancestor Graphic — the scanner must fall
            // back to camera-based background resolution instead of
            // defaulting straight to the hardcoded-white case.
            Canvas canvas = CreateCanvas();

            var cam = Spawn("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.red;

            GameObject textGO = Spawn("Label", canvas.transform);
            var text = textGO.AddComponent<Text>();
            text.color = Color.white;
            text.fontSize = 20;
            textGO.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 30);

            SceneReport report = SceneReportScanner.Scan(_thresholds);

            ElementReport element = FindElement(report, "Label");
            Assert.IsNotNull(element, "Expected a report entry for the Label element.");
            StringAssert.Contains("Camera", element.backgroundSourceLabel,
                "With no ancestor Graphic but a Camera present, the background source should come from the camera path, not the hardcoded white default.");
        }

        [Test]
        public void Scan_IncludesInactiveElements()
        {
            Canvas canvas = CreateCanvas();

            GameObject textGO = Spawn("InactiveLabel", canvas.transform);
            var text = textGO.AddComponent<Text>();
            text.color = Color.white;
            text.fontSize = 20;
            textGO.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 30);
            textGO.SetActive(false);

            SceneReport report = SceneReportScanner.Scan(_thresholds);

            ElementReport element = FindElement(report, "InactiveLabel");
            Assert.IsNotNull(element,
                "Inactive elements should still be included in the scan (GetComponentsInChildren uses includeInactive: true).");
        }
    }
}
