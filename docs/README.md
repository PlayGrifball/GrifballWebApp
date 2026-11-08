# GrifballWebApp Documentation

This directory contains all project documentation organized by topic. 

## Documentation Structure

- **api/**: API documentation and endpoint specifications
- **architecture/**: Technical architecture documentation and design decisions  
- **deployment/**: Deployment guides and infrastructure setup
- **development/**: Development setup, workflow guides, and contribution guidelines
- **user/**: End-user documentation and user guides

## Quick Links

- [GitHub Copilot Instructions](../.github/copilot-instructions.md) - Complete development setup for AI agents
- [Project README](../readme.md) - Main project overview
- [Code Coverage](code-coverage.md) - Running tests with code coverage and testing guidelines
- [Historical Import](historical-import.md) - How the app downloads, snapshots and imports the historical archive
- [Background jobs](jobs.md) - The Quartz jobs, where they run, and the Jobs page
- [Telemetry page](telemetry-page.md) - The sysadmins' view of the app's own logs, metrics and traces, and its settings
- [SQLite](sqlite.md) - Running on a SQLite file instead of SQL Server or PostgreSQL, and its limits
- [Reproducible builds](reproducible-builds.md) - Rebuilding the images byte for byte, their attestations, and checking the live site against the build

## Contributing to Documentation

When adding new documentation:
1. Choose the appropriate subdirectory based on the content type
2. Use clear, descriptive filenames 
3. Update this README with links to new documentation
4. Keep documentation synchronized with code changes