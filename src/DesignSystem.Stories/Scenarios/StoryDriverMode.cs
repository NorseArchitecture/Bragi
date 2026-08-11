namespace Norse.DesignSystem.Stories.Scenarios;

/// <summary>How <see cref="StoryDriver" /> drives the form it wraps after first render.</summary>
// Public, not the house default of internal: Razor-generated components are public, and Mode is a
// public [Parameter] property (Blazor's binding requirement) -- a public member cannot expose a
// less-accessible type (CS0053), so this enum must be at least as accessible as the property.
public enum StoryDriverMode
{
	/// <summary>Sentinel CLR default — never valid; the driver throws on it.</summary>
	Unspecified = 0,

	/// <summary>Submit the untouched form — client-side validation is the pinned state.</summary>
	SubmitOnly = 1,

	/// <summary>Fill valid-shaped values, then submit — the armed scenario's server state is the pinned state.</summary>
	FillAndSubmit = 2,

	/// <summary>Click the story's first button on load — for driving confirm-style pages (no form).</summary>
	ClickOnly = 3
}
