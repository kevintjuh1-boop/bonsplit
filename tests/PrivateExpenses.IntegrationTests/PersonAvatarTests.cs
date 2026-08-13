using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Application.Services;
using PrivateExpenses.Domain.Entities;
using PrivateExpenses.IntegrationTests.TestSupport;

namespace PrivateExpenses.IntegrationTests;

public class PersonAvatarTests : IAsyncLifetime
{
    // A minimal but genuinely valid 1x1 JPEG — real magic bytes, so it passes the same signature
    // check the app uses on real uploads.
    private const string TinyJpegBase64 =
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgICAgMCAgIDAwMDBAYEBAQEBAgGBgUGCQgKCgkICQkKDA8MCgsOCwkJDRENDg8QEBEQ" +
        "CgwSExIQEw8QEBD/2wBDAQMDAwQDBAgEBAgQCwkLEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQ" +
        "EBD/wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAv/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAA" +
        "AAAAAAAAAAAAAAX/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIRAxEAPwCdABmX/9k=";

    private SqliteTestDatabase _db = null!;
    private PersonService _service = null!;
    private Person _kevin = null!;

    public async Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        _service = new PersonService(_db.UnitOfWorkFactory, _db.PersonAvatarStorage);
        var people = await _db.GetPeopleAsync();
        _kevin = people.Single(p => p.Name == "Kevin");
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private static MemoryStream JpegStream() => new(Convert.FromBase64String(TinyJpegBase64));

    [Fact]
    public async Task OpenAvatarAsync_PersonWithoutAnAvatar_ReturnsNull()
    {
        var file = await _service.OpenAvatarAsync(_kevin.Id);

        Assert.Null(file);
    }

    [Fact]
    public async Task UpdateAvatarAsync_ValidJpeg_IsStoredAndRetrievable()
    {
        await using var jpeg = JpegStream();
        await _service.UpdateAvatarAsync(_kevin.Id, jpeg, "kevin.jpg", "image/jpeg", jpeg.Length);

        var updated = await _service.GetByIdAsync(_kevin.Id);
        Assert.NotNull(updated!.AvatarStoredFileName);
        Assert.Equal("image/jpeg", updated.AvatarMimeType);

        var file = await _service.OpenAvatarAsync(_kevin.Id);
        Assert.NotNull(file);
        Assert.Equal("image/jpeg", file.MimeType);

        using var reader = new MemoryStream();
        await file.Content.CopyToAsync(reader);
        Assert.Equal(Convert.FromBase64String(TinyJpegBase64), reader.ToArray());
    }

    [Fact]
    public async Task UpdateAvatarAsync_ReplacingAnExistingPhoto_DeletesTheOldStoredFile()
    {
        await using (var first = JpegStream())
        {
            await _service.UpdateAvatarAsync(_kevin.Id, first, "kevin.jpg", "image/jpeg", first.Length);
        }

        var afterFirst = await _service.GetByIdAsync(_kevin.Id);
        var firstStoredFileName = afterFirst!.AvatarStoredFileName!;

        await using (var second = JpegStream())
        {
            await _service.UpdateAvatarAsync(_kevin.Id, second, "kevin-2.jpg", "image/jpeg", second.Length);
        }

        var afterSecond = await _service.GetByIdAsync(_kevin.Id);
        Assert.NotEqual(firstStoredFileName, afterSecond!.AvatarStoredFileName);

        // The old file must actually be gone, not just unreferenced — this storage is a real local
        // filesystem, so it never accumulates orphaned photos every time someone changes their avatar.
        await Assert.ThrowsAsync<FileNotFoundException>(() => _db.PersonAvatarStorage.OpenAsync(firstStoredFileName));
    }

    [Fact]
    public async Task RemoveAvatarAsync_ClearsTheFieldsAndDeletesTheStoredFile()
    {
        await using (var jpeg = JpegStream())
        {
            await _service.UpdateAvatarAsync(_kevin.Id, jpeg, "kevin.jpg", "image/jpeg", jpeg.Length);
        }

        var withAvatar = await _service.GetByIdAsync(_kevin.Id);
        var storedFileName = withAvatar!.AvatarStoredFileName!;

        await _service.RemoveAvatarAsync(_kevin.Id);

        var afterRemoval = await _service.GetByIdAsync(_kevin.Id);
        Assert.Null(afterRemoval!.AvatarStoredFileName);
        Assert.Null(afterRemoval.AvatarMimeType);
        Assert.Null(await _service.OpenAvatarAsync(_kevin.Id));
        await Assert.ThrowsAsync<FileNotFoundException>(() => _db.PersonAvatarStorage.OpenAsync(storedFileName));
    }

    [Fact]
    public async Task UpdateAvatarAsync_FileContentDoesNotMatchItsClaimedType_ThrowsAndSavesNothing()
    {
        // A .jpg extension and image/jpeg content type, but the actual bytes are plain text — the
        // magic-byte check must catch this rather than trusting the browser-reported type.
        await using var fakeJpeg = new MemoryStream("not actually a jpeg"u8.ToArray());

        await Assert.ThrowsAsync<ExpenseValidationException>(
            () => _service.UpdateAvatarAsync(_kevin.Id, fakeJpeg, "kevin.jpg", "image/jpeg", fakeJpeg.Length));

        var person = await _service.GetByIdAsync(_kevin.Id);
        Assert.Null(person!.AvatarStoredFileName);
    }
}
