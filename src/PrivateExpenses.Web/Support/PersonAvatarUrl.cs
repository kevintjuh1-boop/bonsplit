using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Web.Support;

/// <summary>Builds the URL for a person's uploaded profile photo (served via the
/// /api/personen/{id}/avatar endpoint), or null when they have none — the single place this URL
/// shape is written, so every avatar call site stays a one-liner.</summary>
public static class PersonAvatarUrl
{
    public static string? For(Person person) =>
        person.AvatarStoredFileName is null ? null : $"/api/personen/{person.Id}/avatar";
}
