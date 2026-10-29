using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace WfcStudio.Tests
{
    // Guards the assembly layering: Core -> nothing, Runtime -> Core, Editor -> Runtime + Core.
    // The solver core stays engine-free, and runtime code never depends on the editor.
    public class ArchitectureTests
    {
        [Test]
        public void Core_DoesNotReferenceUnityEngine()
        {
            Assert.That(ReferencedAssemblies("WfcStudio.Core"), Has.None.StartsWith("UnityEngine"));
        }

        [Test]
        public void Core_DoesNotReferenceUnityEditor()
        {
            Assert.That(ReferencedAssemblies("WfcStudio.Core"), Has.None.StartsWith("UnityEditor"));
        }

        [Test]
        public void Runtime_DoesNotReferenceUnityEditor()
        {
            Assert.That(ReferencedAssemblies("WfcStudio.Runtime"), Has.None.StartsWith("UnityEditor"));
        }

        static string[] ReferencedAssemblies(string assemblyName)
        {
            return Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name).ToArray();
        }
    }
}
