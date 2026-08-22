using Norse.Abstractions.Contracts;
using Norse.AuthN.Components;

namespace Norse.DesignSystem.Stories;

/// <summary>
///     The catalog's <see cref="ISessionTransition" />: suppress and record. A transition that begins
///     here never completes — the canvas stays put — and the recording is the assertable trace that a
///     story reached a principal transition. What the tests read; deliberately not surfaced in the
///     canvas. Scoped, same as every catalog fake: the story host is a Blazor Server composition, and
///     DI scope is the framework's own per-circuit boundary — one visitor's circuit gets its own
///     recorder, with no state bleeding across circuits.
/// </summary>
sealed class RecordingSessionTransition : ISessionTransition
{
	readonly List<NavigationResult> _transitions = [];

	/// <summary>Every transition begun, in order.</summary>
	internal IReadOnlyList<NavigationResult> Transitions =>
		_transitions;

	public void Begin(NavigationResult result) =>
		_transitions.Add(result);
}
