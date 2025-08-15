# GitHub Copilot Instructions for GrifballWebApp

## Project Overview
GrifballWebApp is a full-stack web application for planning and organizing Grifball tournaments, pulling match stats, and providing data insights. The application serves players, event organizers, and viewers with features including tournament management, player availability tracking, draft systems, and comprehensive statistics.

## Technology Stack

### Backend (.NET 9)
- **Framework**: .NET 9 with ASP.NET Core Web API
- **Real-time**: SignalR for live updates
- **Database**: Entity Framework Core with SQL Server
- **Authentication**: JWT Bearer tokens with Discord OAuth
- **Logging**: Serilog with console, debug, and SQL Server sinks
- **API Documentation**: Swagger/OpenAPI (Swashbuckle)
- **External Integrations**: Discord bot integration, Google Sheets API, Halo Infinite stats via Grunt API

### Frontend (Angular 20)
- **Framework**: Angular 20 with TypeScript 5.8
- **UI Library**: Angular Material 20 with Angular CDK
- **Styling**: TailwindCSS 3.4 with PostCSS and Autoprefixer
- **Date Handling**: Luxon for timezone-aware date operations
- **Real-time**: SignalR client for live updates
- **Drag & Drop**: ngx-drag-drop for tournament brackets
- **Authentication**: @auth0/angular-jwt for JWT handling

### Database & Infrastructure
- **Database**: SQL Server 2022 CU12 (via Docker)
- **Containerization**: Docker with multi-stage builds
- **Orchestration**: Docker Compose for development environment
- **Development Server**: ASP.NET Core SPA proxy for integrated development

## Prerequisites & Setup

### Required Software
1. **.NET 9 SDK**: Download from https://dotnet.microsoft.com/en-us/download/dotnet/9.0
   - Use the Linux x64 installer for Linux systems
   - Verify installation: `dotnet --version` should show 9.0.x
2. **Node.js & npm**: Version compatible with Angular 20 (Node.js 18.19+ or 20.9+)
3. **Docker**: For SQL Server database and containerized development
4. **Git**: For version control

### Development Setup Commands
```bash
# Clone and navigate to repository
git clone <repository-url>
cd GrifballWebApp

# Install .NET tools (Entity Framework CLI)
dotnet tool restore

# Restore .NET dependencies (timeout: 120+ seconds for fresh install)
dotnet restore

# Navigate to Angular client and install dependencies (timeout: 180+ seconds for fresh install)
cd grifballwebapp.client
npm install
cd ..
```

## Build Commands & Timeouts

### .NET Backend
```bash
# Build entire solution (~11 seconds, expect ~59 warnings - these are normal)
dotnet build

# Build specific project
dotnet build GrifballWebApp.Server/GrifballWebApp.Server.csproj

# Run backend development server
dotnet run --project GrifballWebApp.Server/GrifballWebApp.Server.csproj

# Run with specific environment
dotnet run --project GrifballWebApp.Server/GrifballWebApp.Server.csproj --environment Development
```

### Angular Frontend
```bash
# Navigate to client directory
cd grifballwebapp.client

# Development build (~10.5 seconds)
ng build

# Production build
ng build --configuration production

# Start development server (~10.7 seconds startup, serves on https://localhost:4200)
ng serve
# or
npm start

# Run tests
ng test

# Build with bundle analysis
npm run build:stats
```

### Docker Development Environment
```bash
# Start all services (SQL Server + Application)
docker-compose up --build

# Start only SQL Server
docker-compose up sqlserver

# Clean rebuild
docker-compose up --build --force-recreate
```

## Database Operations

### Entity Framework Migrations
```bash
# Add new migration
dotnet ef migrations add <MigrationName> --project GrifballWebApp.Database

# Update database
dotnet ef database update --project GrifballWebApp.Database

# Remove last migration
dotnet ef migrations remove --project GrifballWebApp.Database

# Generate SQL script
dotnet ef migrations script --project GrifballWebApp.Database
```

### Database Connection
- **Development**: SQL Server via Docker Compose (localhost:5434)
- **Connection String**: `Server=localhost,5434;Database=GrifballWebApp;Encrypt=False;User Id=sa;Password=Pass@word;`

## Testing & Code Quality

### Unit Testing
```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run Angular tests
cd grifballwebapp.client
ng test
```

### Testing Requirements for New Pull Requests
**All new pull requests MUST include unit tests following these requirements:**

#### Required Testing Framework and Libraries
- **Test Framework**: NUnit 4.x (already configured)
- **Mocking Library**: **NSubstitute 5.x ONLY** - Never use Moq
- **Database Testing**: Testcontainers with SQL Server (Testcontainers.MsSql 4.6+)

#### Testing Patterns for Database Operations
When testing code that uses `GrifballContext`, always use testcontainers with the existing SetUpFixture pattern:

```csharp
[SetUp]
public async Task Setup()
{
    _context = await SetUpFixture.NewGrifballContext();
    
    // Setup mocks using NSubstitute
    _mockService = Substitute.For<IYourService>();
    
    // Configure mock behavior
    _mockService.Method(Arg.Any<Type>()).Returns(expectedResult);
}

[TearDown]
public void TearDown()
{
    _context.Dispose();
}
```

#### NSubstitute Examples
```csharp
// Creating mocks
var mockLogger = Substitute.For<ILogger<MyService>>();
var mockRepository = Substitute.For<IMyRepository>();

// Setting up method returns
mockRepository.GetAsync(Arg.Any<int>()).Returns(expectedEntity);

// Verifying method calls
await mockRepository.Received(1).SaveAsync(Arg.Any<MyEntity>());

// Argument matching
mockService.Process(Arg.Is<string>(x => x.Contains("test")));
```

#### Test Structure Requirements
- Use `[TestFixture]` and `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` for test classes
- Include descriptive test method names following the pattern: `MethodName_Should[Expected]_When[Condition]`
- Use `Assert.Multiple()` for multiple related assertions
- Always dispose of contexts in `[TearDown]` methods

### Known Test Issues
- Some unit tests may require database migrations to be current
- Angular tests may show peer dependency warnings (these are normal)

### Code Formatting & Linting
```bash
# Angular linting (if ESLint is configured)
cd grifballwebapp.client
ng lint

# Format Angular code (if Prettier is configured)
npx prettier --write src/
```

## Development Workflow & Expected Behavior

### Expected Warnings
- **.NET Build**: ~59 warnings are normal (mostly related to nullable references and API analysis)
- **npm install**: Peer dependency conflicts are expected and do not affect functionality
- **Angular build**: Some dependency warnings are normal for the current package versions

### Hot Reload & Development Servers
- **Backend**: Supports hot reload when using `dotnet run` or `dotnet watch`
- **Frontend**: Full hot reload with `ng serve`, changes reflect immediately
- **Integrated Development**: Use `dotnet run` from root to start both frontend and backend

### Application Startup Validation
1. **Backend API**: Should be accessible at the configured port (check Program.cs for port configuration)
2. **Frontend UI**: Should load at https://localhost:4200 with:
   - Navigation working properly
   - Data tables displaying (may be empty without database seeding)
   - Material Design components rendering correctly
   - No console errors related to core functionality

### Common Issues & Solutions

#### HTTPS Certificate Issues
```bash
# Trust development certificates
dotnet dev-certs https --trust

# Clean and reinstall certificates
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

#### SQL Server Connection Issues
- Ensure Docker is running and SQL Server container is healthy
- Check connection string in appsettings.Development.json
- Verify port mapping (5434:1433)

#### Node Module Issues
```bash
# Clear npm cache and reinstall
cd grifballwebapp.client
rm -rf node_modules package-lock.json
npm install
```

#### Build Performance
- First builds take longer due to package restoration
- Subsequent builds with warm caches are significantly faster
- Use Docker for consistent build environments

## Project Structure

### Backend Structure
```
GrifballWebApp.Server/           # Main API project
├── Controllers/                 # API controllers
├── Services/                   # Business logic services  
├── Dtos/                       # Data transfer objects
├── Extensions/                 # Extension methods
└── Program.cs                  # Application entry point

GrifballWebApp.Database/        # Entity Framework project
├── Entities/                   # Database entity models
└── Migrations/                 # EF Core migrations

DiscordInterfaces/              # Discord integration
GrifballWebApp.Test/            # Unit tests
GrifballWebApp.Seeder/          # Database seeding
```

### Frontend Structure
```
grifballwebapp.client/src/
├── app/                        # Angular application
├── assets/                     # Static assets
├── environments/               # Environment configurations
└── main.ts                     # Application bootstrap
```

## Key Features & Modules

### Core Functionality
- **User Management**: Discord OAuth authentication, player profiles
- **Tournament Organization**: Season/tournament creation, bracket management
- **Player Availability**: Scheduling system with availability tracking
- **Statistics**: Match data pulling from Halo Infinite via Grunt API
- **Real-time Updates**: SignalR for live tournament updates
- **Draft System**: Interactive player drafting interface

### External Integrations
- **Discord**: Bot integration for notifications and user management
- **Halo Infinite**: Stats pulling via Grunt API
- **Google Sheets**: Data export/import functionality

## Timeout Recommendations for Automation

- **Package Restore** (.NET): 120+ seconds for fresh installs, 30+ seconds with cache
- **npm install**: 180+ seconds for fresh installs, 30+ seconds with cache  
- **Backend Build**: 30+ seconds (includes warnings, ~11 seconds typical)
- **Frontend Build**: 30+ seconds (includes Angular compilation, ~10.5 seconds typical)
- **Development Server Startup**: 30+ seconds for full initialization
- **Database Operations**: 60+ seconds for migrations and seeding
- **Docker Compose**: 300+ seconds for initial setup with image pulls

## Environment Variables & Configuration

### Required Environment Variables
```bash
# Database connection
ConnectionStrings__GrifballWebApp="Server=localhost,5434;Database=GrifballWebApp;Encrypt=False;User Id=sa;Password=Pass@word;"

# Discord OAuth (obtain from Discord Developer Portal)
Authentication__Discord__ClientId="your-discord-client-id"
Authentication__Discord__ClientSecret="your-discord-client-secret"

# Apply migrations on startup (Docker)
ApplyMigrations=true
```

### Configuration Files
- `appsettings.json`: Base configuration
- `appsettings.Development.json`: Development overrides
- `launchSettings.json`: Development server configuration

## Documentation Organization

### Documentation Structure
All project documentation should be organized in the `docs/` folder to keep the repository root clean:

```
docs/
├── api/                    # API documentation
├── architecture/           # Technical architecture docs
├── deployment/             # Deployment guides
├── development/            # Development guides
├── user/                  # User documentation
└── README.md              # Documentation index
```

### Documentation Guidelines
- **Never place documentation files in the repository root** (except README.md)
- Use the `docs/` folder for all documentation changes and additions
- Reference external documentation files from the main README.md when needed
- Keep documentation up to date with code changes
- Use clear, descriptive filenames and organize by topic

This setup provides a fully functional development environment for the GrifballWebApp with all necessary tools, dependencies, and workflows properly configured.

# Frontend Best Practices
You are an expert in TypeScript, Angular, and scalable web application development. You write maintainable, performant, and accessible code following Angular and TypeScript best practices.
## TypeScript Best Practices
- Use strict type checking
- Prefer type inference when the type is obvious
- Avoid the `any` type; use `unknown` when type is uncertain
## Angular Best Practices
- Always use standalone components over NgModules
- Must NOT set `standalone: true` inside Angular decorators. It's the default.
- Use signals for state management
- Implement lazy loading for feature routes
- Do NOT use the `@HostBinding` and `@HostListener` decorators. Put host bindings inside the `host` object of the `@Component` or `@Directive` decorator instead
- Use `NgOptimizedImage` for all static images.
  - `NgOptimizedImage` does not work for inline base64 images.
## Components
- Keep components small and focused on a single responsibility
- Use `input()` and `output()` functions instead of decorators
- Use `computed()` for derived state
- Set `changeDetection: ChangeDetectionStrategy.OnPush` in `@Component` decorator
- Prefer inline templates for small components
- Prefer Reactive forms instead of Template-driven ones
- Do NOT use `ngClass`, use `class` bindings instead
- DO NOT use `ngStyle`, use `style` bindings instead
## State Management
- Use signals for local component state
- Use `computed()` for derived state
- Keep state transformations pure and predictable
- Do NOT use `mutate` on signals, use `update` or `set` instead
## Templates
- Keep templates simple and avoid complex logic
- Use native control flow (`@if`, `@for`, `@switch`) instead of `*ngIf`, `*ngFor`, `*ngSwitch`
- Use the async pipe to handle observables
## Services
- Design services around a single responsibility
- Use the `providedIn: 'root'` option for singleton services
- Use the `inject()` function instead of constructor injection