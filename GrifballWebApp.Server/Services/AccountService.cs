using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
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

        if (passwordDB is null)
            throw new Exception("No password for that user");

        var correct = _cryptographyService.IsCorrectPassword(password: password, passwordHash: passwordDB.Hash, salt: passwordDB.Salt);

        if (!correct)
            throw new Exception("Incorrect password");

        // Login success
    }

    public async Task Register(string username, string password, CancellationToken ct = default)
    {
        var exists = await _context.Persons.Where(p => p.Name == username).AnyAsync(ct);

        // 400
        if (exists)
            throw new Exception("Username is taken");

        // Need gamertag
        var x = new XboxUser()
        {
            Gamertag = username,
            XboxUserID = 233232,
        };

        var hash = _cryptographyService.HashPasword(password, out string salt);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash, nameof(hash));

        var person = new Person()
        {
            Name = username,
            Password = new Password()
            {
                Hash = hash,
                Salt = salt,
            },
            XboxUser = x
        };

        await _context.AddAsync(person, ct);
        await _context.SaveChangesAsync(ct);
    }
}
