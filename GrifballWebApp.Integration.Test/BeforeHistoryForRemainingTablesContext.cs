using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The model as the database had it before HistoryForRemainingTables: the eight tables it gave history have none (no
/// period columns, no history tables). A migration test that seeds a database migrated to before then writes those
/// tables through this, as the model of the day did; today's model would write and read back period columns the
/// tables don't have yet. Its own context type, so its own cached model.
/// </summary>
internal sealed class BeforeHistoryForRemainingTablesContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
{
    private static readonly Type[] WithoutHistory =
    [
        typeof(DuplicateDismissal), typeof(UserMerge), typeof(XuidLookup), typeof(HistoricalImportSettings),
        typeof(HistoricalSeasonReview), typeof(HistoricalSource), typeof(ScheduleRecommendation), typeof(ScheduleRecommendationMatch),
    ];

    /// <summary>On the same database as <paramref name="context"/>.</summary>
    public static BeforeHistoryForRemainingTablesContext On(GrifballContext context)
        => new(TestDatabase.Options(context.Database.GetConnectionString()!).Options);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(_ => new NoHistoryConvention());
    }

    /// <summary>Takes IsTemporal off the eight as their configurations set it: the providers' own conventions then
    /// drop the period columns, and OnModelCreating finds them not temporal.</summary>
    private sealed class NoHistoryConvention : IEntityTypeAnnotationChangedConvention
    {
        private const string IsTemporal = "SqlServer:IsTemporal";

        public void ProcessEntityTypeAnnotationChanged(IConventionEntityTypeBuilder entityTypeBuilder, string name,
            IConventionAnnotation? annotation, IConventionAnnotation? oldAnnotation, IConventionContext<IConventionAnnotation> context)
        {
            if (name == IsTemporal && annotation?.Value is true && WithoutHistory.Contains(entityTypeBuilder.Metadata.ClrType))
                entityTypeBuilder.Metadata.RemoveAnnotation(IsTemporal);
        }
    }
}
