using Norse.Abstractions.Components.Primitives;

namespace Norse.DesignSystem.Stories.Shell;

/// <summary>
///     The catalog's <see cref="IAppShellLayout" />: declares <see cref="StoryShell" /> — a bare
///     <c>@Body</c> — so Heimdall's <c>ManageLayout</c> renders inside a story canvas without the host's
///     real chrome.
/// </summary>
sealed class StoryShellLayout : IAppShellLayout
{
	/// <inheritdoc />
	public Type LayoutType =>
		typeof(StoryShell);
}
