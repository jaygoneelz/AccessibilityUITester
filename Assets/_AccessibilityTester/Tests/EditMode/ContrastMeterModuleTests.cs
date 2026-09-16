using NUnit.Framework;
using AccessibilityTester.Editor.Core;
using AccessibilityTester.Editor.ContrastMeter;

namespace AccessibilityTester.Tests.EditMode
{
    public class ContrastMeterModuleTests
    {
        [Test]
        public void SubscribesOnConstructionAndUnsubscribesOnDispose()
        {
            var coreManager = new CoreManager();

            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleContrastMeterRequested"));

            var module = new ContrastMeterModule(coreManager);
            Assert.AreEqual(1, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleContrastMeterRequested"),
                "Constructing the module should subscribe exactly one handler to OnToggleContrastMeterRequested.");

            module.Dispose();
            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleContrastMeterRequested"),
                "Disposing the module should remove its handler.");

            coreManager.Dispose();
        }
    }
}
