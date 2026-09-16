using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TMPro;
using AccessibilityTester.Runtime.FontScaler;

namespace AccessibilityTester.Tests.EditMode
{
    /// <summary>
    /// EditMode regression tests for FontScalerUtility's overlap detection
    /// (now sort-and-sweep) — the true-positive and true-negative cases
    /// that were previously only verified manually during development.
    /// </summary>
    public class FontScalerUtilityTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            var canvasGO = new GameObject("Canvas");
            _spawned.Add(canvasGO);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown()
        {
            // FontScalerUtility's cache is a static Dictionary, so it must
            // be cleared between tests the same way a real user would via
            // the tool's own Revert button — otherwise state from one test
            // could leak into the next.
            FontScalerUtility.Revert();

            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private TextMeshProUGUI CreateText(string name, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            _spawned.Add(go);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = name;
            tmp.fontSize = 20;
            tmp.color = Color.white;

            RectTransform rect = tmp.rectTransform;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return tmp;
        }

        [Test]
        public void ApplyScale_OverlappingElements_AreHighlightedMagenta()
        {
            TextMeshProUGUI a = CreateText("A", new Vector2(0, 0), new Vector2(100, 20));
            TextMeshProUGUI b = CreateText("B", new Vector2(20, 0), new Vector2(100, 20)); // heavily overlaps A

            int overlapCount = FontScalerUtility.ApplyScale(1f);

            Assert.AreEqual(2, overlapCount, "Both overlapping elements should be flagged.");
            Assert.AreEqual(Color.magenta, a.color);
            Assert.AreEqual(Color.magenta, b.color);
        }

        [Test]
        public void ApplyScale_NonOverlappingElements_AreNotHighlighted()
        {
            TextMeshProUGUI a = CreateText("A", new Vector2(-500, 0), new Vector2(50, 20));
            TextMeshProUGUI b = CreateText("B", new Vector2(500, 0), new Vector2(50, 20)); // far apart, no overlap

            int overlapCount = FontScalerUtility.ApplyScale(1f);

            Assert.AreEqual(0, overlapCount, "Non-overlapping elements should not be flagged.");
            Assert.AreEqual(Color.white, a.color);
            Assert.AreEqual(Color.white, b.color);
        }
    }
}
