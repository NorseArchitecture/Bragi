namespace Norse.DesignSystem.Stories.Scenarios;

/// <summary>
///     The ambient scenario a story pins its fake family into. Registered as a singleton per fake
///     family, constructed with that family's initial (happy-path) value — the constructor argument,
///     not the enum's CLR default, is why an unwrapped story renders success while <c>0</c> stays the
///     platform-law sentinel. <see cref="ScenarioScope{TScenario}" /> is the only writer.
/// </summary>
/// <param name="initialValue">The family's happy-path value, restored when the current pin is released.</param>
sealed class Scenario<TScenario>(TScenario initialValue)
	where TScenario : struct, Enum
{
	readonly Lock _gate = new();

	/// <summary>The constructor-supplied happy-path value a current-pin release restores.</summary>
	readonly TScenario _initialValue = initialValue;

	object? _owner;

	/// <summary>The currently pinned scenario.</summary>
	public TScenario Value
	{
		get
		{
			lock (_gate)
			{
				return field;
			}
		}
		private set;
	} = initialValue;

	/// <summary>Pins a scenario value and returns its opaque ownership token.</summary>
	public object Pin(TScenario value)
	{
		lock (_gate)
		{
			var token = new object();
			Value = value;
			_owner = token;
			return token;
		}
	}

	/// <summary>Restores the initial value when the released token still owns the current pin.</summary>
	public void Release(object? token)
	{
		lock (_gate)
		{
			if (token is null || !ReferenceEquals(_owner, token))
			{
				return;
			}

			Value = _initialValue;
			_owner = null;
		}
	}
}
