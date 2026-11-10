using System.Runtime.CompilerServices;

// Serialized fields are declared `internal` so the editor-side content builder and the tests can
// wire and inspect them with compile-time checking, without widening the public API.
[assembly: InternalsVisibleTo("BikiniWipeout.Editor")]
[assembly: InternalsVisibleTo("BikiniWipeout.Tests.EditMode")]
[assembly: InternalsVisibleTo("BikiniWipeout.Tests.PlayMode")]
