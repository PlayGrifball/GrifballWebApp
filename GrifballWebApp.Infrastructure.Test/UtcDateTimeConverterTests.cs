using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Test;

[TestFixture]
public class UtcDateTimeConverterTests
{
    private static readonly UtcDateTimeConverter Converter = new();

    private static DateTime Write(DateTime value) => (DateTime)Converter.ConvertToProvider(value)!;
    private static DateTime Read(DateTime value) => (DateTime)Converter.ConvertFromProvider(value)!;

    [Test]
    public void Write_Utc_IsKept()
    {
        var value = new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Utc);

        var stored = Write(value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.Ticks, Is.EqualTo(value.Ticks));
            Assert.That(stored.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    [Test]
    public void Write_Unspecified_IsTakenAsUtc_WithoutShifting()
    {
        var value = new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Unspecified);

        var stored = Write(value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.Ticks, Is.EqualTo(value.Ticks));
            Assert.That(stored.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    [Test]
    public void Write_Local_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Write(new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Local)));

        Assert.That(ex!.Message, Does.Contain("Kind Local"));
    }

    [TestCase(DateTimeKind.Unspecified)]
    [TestCase(DateTimeKind.Utc)]
    public void Read_IsUtc(DateTimeKind kind)
    {
        var value = new DateTime(2026, 10, 11, 12, 30, 0, kind);

        var read = Read(value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(read.Ticks, Is.EqualTo(value.Ticks));
            Assert.That(read.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }
}
