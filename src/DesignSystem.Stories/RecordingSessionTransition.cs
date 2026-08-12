using Norse.Abstractions.Contracts;
using Norse.AuthN.Components;

namespace Norse.DesignSystem.Stories;

/// <summary>
///     The catalog's <see cref="ISessionTransition" />: suppress and record. A transition that begins
///     here never completes — the canvas stays put — and the recording is the assertable trace that a
///     story reached a principal transition. What the tests read; deliberately not surfaced in the
///     canvas. Singleton, same as every catalog fake (WASM makes scoped effectively singleton anyway).
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
