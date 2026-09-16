using NUnit.Framework;
using AccessibilityTester.Editor.Core;
using AccessibilityTester.Editor.FontScaler;

namespace AccessibilityTester.Tests.EditMode
{
    public class FontScalerModuleTests
    {
        [Test]
        public void SubscribesOnConstructionAndUnsubscribesOnDispose()
        {
            var coreManager = new CoreManager();

            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnFontScaleRequested"));

            var module = new FontScalerModule(coreManager);
            Assert.AreEqual(1, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnFontScaleRequested"),
                "Constructing the module should subscribe exactly one handler to OnFontScaleRequested.");

            module.Dispose();
            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnFontScaleRequested"),
                "Disposing the module should remove its handler.");

            coreManager.Dispose();
        }
    }
}
