namespace Norse.DesignSystem.Stories.Tests;

public class AssemblyMarkerTests
{
	[Fact]
	void Assembly_resolves_to_DesignSystem_Stories() =>
		typeof(AssemblyMarker).Assembly.GetName().Name.ShouldBe("Norse.DesignSystem.Stories");
}
