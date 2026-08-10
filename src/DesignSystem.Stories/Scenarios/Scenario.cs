namespace Norse.DesignSystem.Stories.Scenarios;

/// <summary>
///     The ambient scenario a story pins its fake family into. Registered as a singleton per fake
///     family, constructed with that family's initial (happy-path) value — the constructor argument,
///     not the enum's CLR default, is why an unwrapped story renders success while <c>0</c> stays the
///     platform-law sentinel. Pins are single-slot and non-composing: the newest pin supersedes the
///     prior owner, disposing a stale pin is a no-op, and disposing the current pin restores the
///     constructor initial value, never a superseded pin's value. Reference identity rejects stale
///     disposal; the lock only keeps value reads and owner/value transitions coherent. A superseded
///     scope that remains mounted after its successor is released does not regain ownership: the slot
///     stays at its initial value unless that stale scope re-renders, at which point repinning fails
///     loudly rather than silently stealing the slot.
///     <see cref="ScenarioScope{TScenario}" /> is the only writer.
/// </summary>
/// <param name="initialValue">The family's happy-path value, restored when the current pin is released.</param>
sealed class Scenario<TScenario>(TScenario initialValue)
	where TScenario : struct, Enum
{
	readonly Lock _gate = new();

	/// <summary>The constructor-supplied happy-path value a current-pin release restores.</summary>
	readonly TScenario _initialValue = initialValue;

	ScenarioPin? _owner;

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

	/// <summary>
	///     Pins the value in the scenario's single slot, superseding prior ownership, and returns the
	///     new owner. Initial pins intentionally supersede a live owner so Blazor can mount a successor
	///     before disposing its predecessor. Disposing a stale pin does nothing; disposing the current
	///     pin restores the constructor initial value rather than the superseded value.
	/// </summary>
	internal ScenarioPin Pin(TScenario value)
	{
		lock (_gate)
		{
			var pin = new ScenarioPin(this);
			Value = value;
			_owner = pin;
			return pin;
		}
	}

	/// <summary>
	///     Updates the value owned by an existing pin without changing its identity.
	/// </summary>
	/// <exception cref="InvalidOperationException">The pin has been superseded by another scope.</exception>
	void Repin(ScenarioPin pin, TScenario value)
	{
		lock (_gate)
		{
			if (!ReferenceEquals(_owner, pin))
			{
				throw new InvalidOperationException("Cannot repin a scenario pin after a successor has superseded it.");
			}

			Value = value;
		}
	}

	void Release(ScenarioPin pin)
	{
		lock (_gate)
		{
			if (!ReferenceEquals(_owner, pin))
			{
				return;
			}

			Value = _initialValue;
			_owner = null;
		}
	}

	/// <summary>
	///     An idempotently disposable owner of one single-slot scenario pin. A newer pin can supersede
	///     this one; this handle never composes or represents a stack frame, and disposing it never
	///     restores an earlier pin.
	/// </summary>
	internal sealed class ScenarioPin(Scenario<TScenario> scenario) : IDisposable
	{
		Scenario<TScenario>? _scenario = scenario;

		/// <summary>Updates the value while this pin remains the scenario's current owner.</summary>
		/// <exception cref="ObjectDisposedException">This pin has already been disposed.</exception>
		/// <exception cref="InvalidOperationException">A successor has superseded this pin.</exception>
		internal void Repin(TScenario value)
		{
			var owner = _scenario ?? throw new ObjectDisposedException(nameof(ScenarioPin));
			owner.Repin(this, value);
		}

		public void Dispose()
		{
			var owner = _scenario;
			if (owner is null)
			{
				return;
			}

			_scenario = null;
			owner.Release(this);
		}
	}
}
