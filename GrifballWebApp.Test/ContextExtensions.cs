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
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
}
