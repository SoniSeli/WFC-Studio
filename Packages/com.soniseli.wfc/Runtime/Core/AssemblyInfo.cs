using System.Runtime.CompilerServices;

// WfcStudio.Core is the engine-free solver (noEngineReferences in its asmdef).
// ArchitectureTests keeps it free of UnityEngine and UnityEditor.
[assembly: InternalsVisibleTo("WfcStudio.Tests")]
