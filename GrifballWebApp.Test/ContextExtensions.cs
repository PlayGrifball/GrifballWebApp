using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;
public static class ContextExtensions
{
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
    public static async Task DisableContraints(this GrifballContext _context, string table)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new Exception("You should not being using this method outside a transaction");

        //await _context.Database.ExecuteSqlRawAsync("EXEC sp_msforeachtable \"ALTER TABLE ? NOCHECK CONSTRAINT all\"");
        await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} ON");
    }

    public static async Task EnableContraints(this GrifballContext _context, string table)
    {
        //await _context.Database.ExecuteSqlRawAsync("EXEC sp_msforeachtable \"ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all\"");
        await _context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} OFF");
    }

    public static async Task SaveChangesWithoutContraints(this GrifballContext _context)
    {
        // Get all entities being added
        var entries = _context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Metadata)
            .Distinct()
            .ToList();

        // Find tables with identity columns
        var tablesWithIdentity = new List<(string Schema, string Table)>();
        foreach (var entityType in entries)
        {
            var key = entityType.FindPrimaryKey();
            if (key == null) continue;

            // Find if any PK property is identity
            var identityProp = key.Properties.FirstOrDefault(p =>
                p.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd &&
                p.GetAnnotations().Any(a => a.Name == "SqlServer:ValueGenerationStrategy" && a.Value?.ToString().Contains("Identity") == true)
            );
            if (identityProp == null) continue;

            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? "dbo";
            tablesWithIdentity.Add((schema, tableName));
        }

        // Use a transaction
        using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            // Disable constraints for each table
            foreach (var (schema, table) in tablesWithIdentity)
                await _context.DisableContraints($"[{schema}].[{table}]");

            await _context.SaveChangesAsync();

            // Enable constraints for each table
            foreach (var (schema, table) in tablesWithIdentity)
                await _context.EnableContraints($"[{schema}].[{table}]");

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
}
