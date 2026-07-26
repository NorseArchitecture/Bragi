using System.Reflection;

namespace Norse.DesignSystem.Stories;

/// <summary>Anchor type for locating this assembly by reflection — BlazingStory's <c>Assemblies</c> parameter takes a list of <see cref="Assembly"/> instances, and this avoids depending on the compiler-generated type name for a <c>.stories.razor</c> file.</summary>
public static class AssemblyMarker;
