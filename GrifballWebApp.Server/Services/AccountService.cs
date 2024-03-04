using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Surprenant.Grunt.Core;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GrifballWebApp.Server.Services;

public class AccountService
{
    private readonly GrifballContext _context;
    private readonly CryptographyService _cryptographyService;
    private readonly HaloInfiniteClientFactory _haloInfiniteClientFactory;
    private readonly IConfiguration _configuration;

    public AccountService(GrifballContext grifballContext, CryptographyService cryptographyService,
        HaloInfiniteClientFactory haloInfiniteClientFactory, IConfiguration configuration)
    {
        _context = grifballContext;
        _cryptographyService = cryptographyService;
        _haloInfiniteClientFactory = haloInfiniteClientFactory;
        _configuration = configuration;
    }

    private string CreateToken(string username)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Convert.FromBase64String(_configuration.GetValue<string>("JwtSecret") ?? throw new Exception("Missing JwtSecret"));
        var tokenDescripor = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Name, username),
            }),
            Expires = DateTime.UtcNow.AddMinutes(60),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };
        var token = tokenHandler.CreateEncodedJwt(tokenDescripor);
        return token;
    }

    public async Task<string> Login(string username, string password, CancellationToken cancellationToken = default)
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
        return CreateToken(person.Name);
    }

    public async Task Register(string username, string password, string gamertag, CancellationToken ct = default)
    {
        var exists = await _context.Persons.Where(p => p.Name == username).AnyAsync(ct);

        // 400
        if (exists)
            throw new Exception("Username is taken");

        var xboxUserID = await _context.XboxUsers.Where(x => x.Gamertag == gamertag)
            .Select(x => x.XboxUserID)
            .FirstOrDefaultAsync(ct);

        if (xboxUserID == default)
        {
            var client = await _haloInfiniteClientFactory.CreateAsync();
            var user = await client.UserByGamertag(gamertag: gamertag);
            if (user is null || user.Result is null)
                throw new Exception("User not found. Verify gamertag is correct.");

            var xboxUser = new XboxUser()
            {
                Gamertag = user.Result.gamertag,
                XboxUserID = Convert.ToInt64(user.Result.xuid),
            };
            _context.Add(xboxUser);
            xboxUserID = xboxUser.XboxUserID;
        }

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
            XboxUserID = xboxUserID
        };

        await _context.AddAsync(person, ct);
        await _context.SaveChangesAsync(ct);
    }
}
