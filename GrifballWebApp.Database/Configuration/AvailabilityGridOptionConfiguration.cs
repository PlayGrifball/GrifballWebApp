using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class AvailabilityGridOptionConfiguration : IEntityTypeConfiguration<AvailabilityGridOption>
{
    public void Configure(EntityTypeBuilder<AvailabilityGridOption> entity)
    {
        entity.ToTable("AvailabilityGridOptions", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.DayOfWeek, e.Time });

        entity.HasData(new List<AvailabilityGridOption>()
        {
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Sunday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19)), // 7
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Sunday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19.5d)), // 7:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Sunday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), // 8
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Sunday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20.5d)), // 8:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Sunday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21)), // 9
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Sunday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21.5d)), // 9:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Sunday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22)), // 10
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Sunday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22.5d)), // 10:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Monday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19)), // 7
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Monday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19.5d)), // 7:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Monday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), // 8
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Monday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20.5d)), // 8:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Monday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21)), // 9
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Monday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21.5d)), // 9:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Monday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22)), // 10
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Monday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22.5d)), // 10:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Tuesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19)), // 7
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Tuesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19.5d)), // 7:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Tuesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), // 8
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Tuesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20.5d)), // 8:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Tuesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21)), // 9
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Tuesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21.5d)), // 9:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Tuesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22)), // 10
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Tuesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22.5d)), // 10:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Wednesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19)), // 7
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Wednesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19.5d)), // 7:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Wednesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), // 8
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Wednesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20.5d)), // 8:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Wednesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21)), // 9
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Wednesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21.5d)), // 9:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Wednesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22)), // 10
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Wednesday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22.5d)), // 10:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Thursday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19)), // 7
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Thursday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19.5d)), // 7:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Thursday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), // 8
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Thursday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20.5d)), // 8:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Thursday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21)), // 9
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Thursday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21.5d)), // 9:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Thursday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22)), // 10
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Thursday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22.5d)), // 10:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Friday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19)), // 7
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Friday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19.5d)), // 7:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Friday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), // 8
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Friday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20.5d)), // 8:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Friday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21)), // 9
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Friday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21.5d)), // 9:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Friday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22)), // 10
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Friday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22.5d)), // 10:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Saturday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19)), // 7
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Saturday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(19.5d)), // 7:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Saturday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), // 8
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Saturday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(20.5d)), // 8:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Saturday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21)), // 9
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Saturday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(21.5d)), // 9:30
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Saturday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22)), // 10
            },
            new AvailabilityGridOption()
            {
                DayOfWeek = DayOfWeek.Saturday,
                Time = TimeOnly.FromTimeSpan(TimeSpan.FromHours(22.5d)), // 10:30
            },
        });

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AvailabilityGridOption> entity);
}
