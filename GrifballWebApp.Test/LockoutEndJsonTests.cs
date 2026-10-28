using System.Text.Json;
using GrifballWebApp.Server;
using GrifballWebApp.Server.UserManagement;

namespace GrifballWebApp.Test;

/// <summary>
/// EditUser compares the posted LockoutEnd with the stored one, so the client must be able to send an
/// untouched value back exactly as the API wrote it, and an edited one in a form the API reads.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class LockoutEndJsonTests
{
    private static readonly JsonSerializerOptions Api = ApiJson.Options();

    private static string Written(DateTimeOffset? value)
    {
        var dto = new UserResponseDto { UserID = 1, UserName = "u", LockoutEnd = value, Region = null, DisplayName = null, Gamertag = null, Discord = null, ExternalAuthCount = 0, HasPassword = false };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, Api));
        return doc.RootElement.GetProperty("lockoutEnd").GetString()!;
    }

    [Test]
    public void PermanentLockout_AsWritten_ReadsBackEqual()
    {
        var written = Written(DateTimeOffset.MaxValue);
        var read = JsonSerializer.Deserialize<DateTimeOffset?>($"\"{written}\"", Api);

        Assert.That(read, Is.EqualTo(DateTimeOffset.MaxValue), "an untouched permanent lockout is not a change");
    }

    [Test]
    public void OrdinaryLockout_AsWritten_ReadsBackEqual()
    {
        var stored = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero).AddTicks(1234567);
        var read = JsonSerializer.Deserialize<DateTimeOffset?>($"\"{Written(stored)}\"", Api);

        Assert.That(read, Is.EqualTo(stored), "sub-millisecond ticks survive when the string is sent back as written");
    }

    [Test]
    public void ClientUtcForm_OfThePermanentLockout_IsReadable()
    {
        // What the client's toApiDate sends for an edited value at the end of time.
        var read = JsonSerializer.Deserialize<DateTimeOffset?>("\"9999-12-31T23:59:59.999Z\"", Api);
        Assert.That(read!.Value.Year, Is.EqualTo(9999));
    }

    [Test]
    public void LocalOffsetForm_PastYear9999_IsRejected()
    {
        // What DateTime.toJSON() produced for DateTimeOffset.MaxValue in a zone ahead of UTC (the bug).
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTimeOffset?>("\"+010000-01-01T00:59:59.999+01:00\"", Api));
    }
}
