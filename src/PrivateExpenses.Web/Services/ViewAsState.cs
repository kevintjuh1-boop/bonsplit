namespace PrivateExpenses.Web.Services;

/// <summary>
/// Holds the "Bekijk als" person selected on the dashboard (section 35). This is a display
/// preference only — never authentication — scoped to the current circuit/session and reset on
/// reload.
/// </summary>
public class ViewAsState
{
    public Guid? CurrentPersonId { get; private set; }

    public event Action? Changed;

    public void SetCurrentPerson(Guid personId)
    {
        if (CurrentPersonId == personId)
        {
            return;
        }

        CurrentPersonId = personId;
        Changed?.Invoke();
    }
}
