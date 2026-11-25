using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Migrations.SqlServer.Test;

/// <summary>TableDiagrams.Generate on small models, one case for each way the real model can differ from the simple one.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TableDiagramsGeneratorTests
{
    private sealed class Alpha { public int Id { get; set; } }
    private sealed class Zeta { public int Id { get; set; } }
    private sealed class Child { public int Id { get; set; } public int AlphaId { get; set; } public int ZetaId { get; set; } }
    private sealed class Report { public string Name { get; set; } = ""; }
    private sealed class OneItem { public int Id { get; set; } }
    private sealed class TwoItem { public int Id { get; set; } }
    private enum Kind : byte { A }
    private sealed class KindRow { public Kind Id { get; set; } }
    private sealed class BlobRow { public byte[] Id { get; set; } = []; }
    private sealed class Address { public string Street { get; set; } = ""; }
    private sealed class Owner { public int Id { get; set; } public Address Home { get; set; } = new(); }
    private class Animal { public int Id { get; set; } }
    private sealed class Dog : Animal { public string Name { get; set; } = ""; }

    private sealed class ModelContext(DbContextOptions<ModelContext> options, Action<ModelBuilder> build) : DbContext(options)
    {
        public Action<ModelBuilder> Build { get; } = build;
        protected override void OnModelCreating(ModelBuilder modelBuilder) => Build(modelBuilder);
    }

    // EF caches a model per context type: key it by the test's own builder instead
    private sealed class KeyByBuilder : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => ((ModelContext)context).Build;
    }

    private static string Diagram(Action<ModelBuilder> build)
    {
        var options = new DbContextOptionsBuilder<ModelContext>()
            .UseSqlServer("Server=none;Database=none;")
            .ReplaceService<IModelCacheKeyFactory, KeyByBuilder>()
            .Options;
        using var context = new ModelContext(options, build);
        return TableDiagrams.Generate(context.GetService<IDesignTimeModel>().Model);
    }

    [Test]
    public void AKeylessEntity_IsDrawnWithNoKey()
    {
        var text = Diagram(b => b.Entity<Report>().HasNoKey().ToTable("Reports", "Other"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("    Reports {"));
            Assert.That(text, Does.Not.Contain(" PK"));
        }
    }

    [Test]
    public void ADerivedTypeInItsOwnTable_IsDrawnWithItsKey()
    {
        var text = Diagram(b =>
        {
            b.Entity<Animal>().UseTptMappingStrategy().ToTable("Animals", "Other");
            b.Entity<Dog>().ToTable("Dogs", "Other");
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("    Animals {"));
            Assert.That(text, Does.Contain("    Dogs {"));
            Assert.That(text, Does.Contain("        int Id PK, FK"));
        }
    }

    [Test]
    public void TablesOfOneNameInTwoSchemas_AreDrawnWithTheirSchemas()
    {
        var text = Diagram(b =>
        {
            b.Entity<OneItem>().ToTable("Items", "Auth");
            b.Entity<TwoItem>().ToTable("Items", "Event");
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("    Auth_Items {"));
            Assert.That(text, Does.Contain("    Event_Items {"));
            Assert.That(text, Does.Not.Contain("    Items {"));
        }
    }

    [Test]
    public void TheTablesOfOtherSchemas_AreListedInOrdinalOrder()
    {
        var text = Diagram(b =>
        {
            b.Entity<Alpha>().ToTable("alpha", "Auth");
            b.Entity<Zeta>().ToTable("Zeta", "Auth");
            b.Entity<Child>(c =>
            {
                c.ToTable("Children", "Event");
                c.HasOne<Alpha>().WithMany().HasForeignKey(x => x.AlphaId);
                c.HasOne<Zeta>().WithMany().HasForeignKey(x => x.ZetaId);
            });
        });

        var event_ = text[text.IndexOf("### Event", StringComparison.Ordinal)..];
        Assert.That(event_.IndexOf("    Zeta {", StringComparison.Ordinal), Is.LessThan(event_.IndexOf("    alpha {", StringComparison.Ordinal)),
            "Upper case sorts before lower case, whatever the machine's culture");
    }

    [Test]
    public void AnOwnedTypeWithATableOfItsOwn_IsDrawnAndTiedToItsOwner()
    {
        var text = Diagram(b => b.Entity<Owner>(o =>
        {
            o.ToTable("Owners", "Other");
            o.OwnsOne(x => x.Home, h => h.ToTable("Addresses", "Other"));
        }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("    Addresses {"));
            Assert.That(text, Does.Contain("Owners ||--o| Addresses"));
        }
    }

    [Test]
    public void AnOwnedTypeInItsOwnersTable_IsNotTiedToIt()
    {
        var text = Diagram(b => b.Entity<Owner>(o =>
        {
            o.ToTable("Owners", "Other");
            o.OwnsOne(x => x.Home);
        }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("    Owners {"));
            Assert.That(text, Does.Not.Contain("Owners ||--o| Owners"));
            Assert.That(text, Does.Contain("(1 tables)"));
        }
    }

    [Test]
    public void ASchemaWithNoDescription_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Diagram(b => b.Entity<Alpha>().ToTable("Alphas", "Nowhere")));

        Assert.That(ex.Message, Does.Contain("Nowhere"));
    }

    [Test]
    public void AKeysType_IsItsUnderlyingTypeForAnEnumAndByteArrayForBytes()
    {
        var text = Diagram(b =>
        {
            b.Entity<KindRow>(k => { k.ToTable("Kinds", "Other"); k.HasKey(x => x.Id); k.Property(x => x.Id).ValueGeneratedNever(); });
            b.Entity<BlobRow>(k => { k.ToTable("Blobs", "Other"); k.HasKey(x => x.Id); });
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("        byte Id PK"));
            Assert.That(text, Does.Contain("        byte[] Id PK"));
        }
    }
}
