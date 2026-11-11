using System.Runtime.CompilerServices;

// Editor tools (generators, scene builders) and tests configure runtime components
// through internal setup methods instead of reflection or public setters.
[assembly: InternalsVisibleTo("TidyUp.Editor")]
[assembly: InternalsVisibleTo("TidyUp.Tests.EditMode")]
