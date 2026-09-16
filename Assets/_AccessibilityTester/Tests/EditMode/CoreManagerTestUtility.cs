using System;
using System.Reflection;
using NUnit.Framework;
using AccessibilityTester.Editor.Core;

namespace AccessibilityTester.Tests.EditMode
{
    /// <summary>
    /// Shared reflection helper for the Editor bridge module tests: reads
    /// the private backing-field delegate for one of CoreManager's public
    /// events to count how many handlers are currently subscribed. This
    /// lets each module's subscribe-on-construct / unsubscribe-on-dispose
    /// behaviour be verified directly, rather than through an indirect
    /// side effect (a module's real file I/O or Play Mode behaviour).
    /// </summary>
    internal static class CoreManagerTestUtility
    {
        public static int GetSubscriberCount(CoreManager coreManager, string eventName)
        {
            FieldInfo field = typeof(CoreManager).GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"CoreManager has no event backing field named '{eventName}'.");

            var handler = field.GetValue(coreManager) as Delegate;
            return handler?.GetInvocationList().Length ?? 0;
        }
    }
}
