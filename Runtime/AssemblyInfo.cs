using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Soup.Editor")]

// Tests build graphs directly — node ids and outport lists — rather than going
// through the editor's SerializedProperty path, so they need the same internals.
[assembly: InternalsVisibleTo("Soup.Tests")]
