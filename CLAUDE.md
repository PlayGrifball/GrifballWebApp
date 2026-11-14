# Claude Code

@AGENTS.md

The backend follows **Clean Architecture** (Domain ← Application ← Infrastructure ← Server, dependencies inwards only)
with **domain-driven design** in the Domain (bounded contexts, aggregates that own their rules). AGENTS.md, imported
above, summarises both; [docs/architecture.md](docs/architecture.md) has the detail. `GrifballWebApp.Architecture.Test`
enforces them.
