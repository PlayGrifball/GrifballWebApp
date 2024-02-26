using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Services;

public class AccountService
{
    private readonly GrifballContext _context;
    private readonly CryptographyService _cryptographyService;

    public AccountService(GrifballContext grifballContext, CryptographyService cryptographyService)
    {
        _context = grifballContext;
        _cryptographyService = cryptographyService;
    }

    public async Task Login(string username, string password, CancellationToken cancellationToken = default)
    {
        var person = await _context.Persons.Where(p => p.Name == username).FirstOrDefaultAsync(cancellationToken);

        if (person is null)
            throw new Exception("Person does not exist");

        var passwordDB = await _context.Passwords.Where(p => p.PersonID == person.PersonID).FirstOrDefaultAsync(cancellationToken);

        if (person is null)
            throw new Exception("No password for that user");

        var hash = _cryptographyService.HashPasword(password, out byte[] salt);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash, nameof(hash));

        if (hash != passwordDB!.Hash)
            throw new Exception("Incorrect password");

        // Login success
    }

    public async Task Register(string username, string password, CancellationToken cancellationToken = default)
    {
        
    }
}
