namespace Norse.DesignSystem.Stories.Scenarios;

/// <summary>
///     The ambient scenario a story pins its fake family into. Registered as a singleton per fake
///     family, constructed with that family's initial (happy-path) value — the constructor argument,
///     not the enum's CLR default, is why an unwrapped story renders success while <c>0</c> stays the
///     platform-law sentinel. <see cref="ScenarioScope{TScenario}" /> is the only writer.
/// </summary>
/// <param name="initialValue">The family's happy-path value, restored by <see cref="Reset" />.</param>
sealed class Scenario<TScenario>(TScenario initialValue)
	where TScenario : struct, Enum
{
	/// <summary>The constructor-supplied happy-path value <see cref="Reset" /> restores.</summary>
	readonly TScenario _initialValue = initialValue;

	/// <summary>The currently pinned scenario.</summary>
	public TScenario Value { get; set; } = initialValue;

	/// <summary>Restores the initial value — called when a <see cref="ScenarioScope{TScenario}" /> leaves the canvas.</summary>
	public void Reset() =>
		Value = _initialValue;
}
