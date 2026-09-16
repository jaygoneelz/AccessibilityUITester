#if ACCESSIBILITY_TESTER_URP
using NUnit.Framework;
using AccessibilityTester.Editor.Core;
using AccessibilityTester.Editor.ShaderController;

namespace AccessibilityTester.Tests.EditMode
{
    public class ShaderControllerModuleTests
    {
        [Test]
        public void SubscribesOnConstructionAndUnsubscribesOnDispose()
        {
            var coreManager = new CoreManager();

            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleCvdSimulationRequested"));
            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleBlurRequested"));

            // A null UniversalRendererData is fine here — the constructor's
            // FindFeature() call early-returns on a null renderer data, and
            // subscription doesn't depend on it.
            var module = new ShaderControllerModule(coreManager, null);
            Assert.AreEqual(1, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleCvdSimulationRequested"),
                "Constructing the module should subscribe exactly one handler to OnToggleCvdSimulationRequested.");
            Assert.AreEqual(1, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleBlurRequested"),
                "Constructing the module should subscribe exactly one handler to OnToggleBlurRequested.");

            module.Dispose();
            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleCvdSimulationRequested"),
                "Disposing the module should remove its CVD toggle handler.");
            Assert.AreEqual(0, CoreManagerTestUtility.GetSubscriberCount(coreManager, "OnToggleBlurRequested"),
                "Disposing the module should remove its blur toggle handler.");

            coreManager.Dispose();
        }
    }
}
#endif
