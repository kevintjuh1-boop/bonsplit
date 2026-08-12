namespace PrivateExpenses.Web.Services;

/// <summary>
/// The person using this browser, persisted client-side (localStorage, via PersonPicker) so it
/// survives reloads and future visits — not authentication, just an attribution label. Used to
/// default the "wie heeft betaald" field, to record who uploaded a receipt, and as the exclusion
/// when notifying the other two people about new activity.
/// </summary>
public class CurrentPersonState
{
    public Guid? CurrentPersonId { get; private set; }

    /// <summary>True once the initial localStorage read has completed (whether or not a person was
    /// found), so consumers can distinguish "still loading" from "genuinely no one picked yet".</summary>
    public bool Loaded { get; private set; }

    public event Action? Changed;

    /// <summary>Raised when something (e.g. the Instellingen page) wants the picker shown again so
    /// the person can switch identity.</summary>
    public event Action? ChangeRequested;

    public void RequestChange() => ChangeRequested?.Invoke();

    public void SetLoaded(Guid? personId)
    {
        CurrentPersonId = personId;
        Loaded = true;
        Changed?.Invoke();
    }

    public void SetCurrentPerson(Guid personId)
    {
        CurrentPersonId = personId;
        Loaded = true;
        Changed?.Invoke();
    }
}
