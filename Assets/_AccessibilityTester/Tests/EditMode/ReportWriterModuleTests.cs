using NUnit.Framework;
using UnityEngine;
using AccessibilityTester.Editor.Core;
using AccessibilityTester.Editor.ReportWriter;
using AccessibilityTester.Runtime.ReportWriter;

namespace AccessibilityTester.Tests.EditMode
{
    public class ReportWriterModuleTests
    {
        [Test]
        public void SubscribesOnConstructionAndUnsubscribesOnDispose()
        {
            var coreManager = new CoreManager();
            var thresholds = ScriptableObject.CreateInstance<AccessibilityThresholds>();

            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnGenerateReportRequested"));

            var module = new ReportWriterModule(coreManager, thresholds);
            Assert.AreEqual(1, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnGenerateReportRequested"),
                "Constructing the module should subscribe exactly one handler to OnGenerateReportRequested.");

            module.Dispose();
            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnGenerateReportRequested"),
                "Disposing the module should remove its handler.");

            Object.DestroyImmediate(thresholds);
            coreManager.Dispose();
        }
    }
}
