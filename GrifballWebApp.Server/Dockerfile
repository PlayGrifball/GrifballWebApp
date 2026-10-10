#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Built for linux/amd64 and linux/arm64 (docker-publish.yml). The .NET stages run on the build
# machine's own platform (BUILDPLATFORM) and target the image's (TARGETARCH): the app is portable IL,
# the migrations bundle is built for the target's runtime, and nothing .NET runs under emulation.

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

# The PostgreSQL client (psql, pg_dump, pg_restore): the Helm chart's migration Job and backups on
# Postgres (charts/grifballwebapp), as sqlcmd is on SQL Server. Its major version is the newest
# server's the chart supports: pg_dump can't dump a newer server.
RUN apk add --no-cache icu-libs postgresql18-client
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER app
WORKDIR /app
EXPOSE 5295
EXPOSE 7210

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
#COPY ["grifballwebapp.client/nuget.config", "grifballwebapp.client/"]
COPY ["GrifballWebApp.Server/GrifballWebApp.Server.csproj", "GrifballWebApp.Server/"]
#COPY ["grifballwebapp.client/grifballwebapp.client.esproj", "grifballwebapp.client/"]
#COPY ["GrifballWebApp.Database/GrifballWebApp.Database.csproj", "GrifballWebApp.Database/"]
RUN dotnet restore "./GrifballWebApp.Server/GrifballWebApp.Server.csproj"
COPY . .
WORKDIR "/src/GrifballWebApp.Server"
RUN dotnet build "./GrifballWebApp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:DefineConstants=DOCKER_BUILD

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GrifballWebApp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:DefineConstants=DOCKER_BUILD

# EF Core migrations bundles: one executable per database that applies pending migrations - SQL
# Server's at /app/efbundle, Postgres's at /app/postgres/efbundle, each from its own migrations project.
# Shipping them inside the backend image means the migrations a deploy runs always come from the same
# build as the backend being deployed; the Helm chart's migration Job (charts/grifballwebapp) runs the
# database's with ConnectionStrings__GrifballWebApp set in the environment. Framework-dependent: they
# use the .NET runtime already in the aspnet base image, built for the image's platform.
# They connect only when run, but building one still creates the design-time context, which wants a
# connection string: a dummy.
FROM build AS migrations
ARG TARGETARCH
RUN dotnet tool install --global dotnet-ef --version 10.0.0
ENV PATH="$PATH:/root/.dotnet/tools"
WORKDIR /src
RUN case "$TARGETARCH" in \
      amd64) rid=linux-musl-x64 ;; \
      arm64) rid=linux-musl-arm64 ;; \
      *) echo "Unsupported TARGETARCH '$TARGETARCH'" >&2; exit 1 ;; \
    esac \
 && export ConnectionStrings__GrifballWebApp="Server=unused;Database=unused" \
 && dotnet ef migrations bundle \
      --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer \
      --configuration Release --target-runtime "$rid" -o /app/efbundle \
 && dotnet ef migrations bundle \
      --project GrifballWebApp.Migrations.Postgres --startup-project GrifballWebApp.Migrations.Postgres \
      --configuration Release --target-runtime "$rid" -o /app/postgres/efbundle

# Each bundle's migration IDs, one per line, beside it: /app/migrations.txt (SQL Server) and
# /app/postgres/migrations.txt. The migration Job and the backend's wait compare them with the
# database's __EFMigrationsHistory. --no-connect lists them without a database; the design-time
# factory still wants a connection string, so it gets a dummy. The build fails if a list is empty or
# not the YYYYMMDDHHMMSS_Name shape they rely on.
RUN list() { \
      ConnectionStrings__GrifballWebApp="Server=unused;Database=unused" \
        dotnet ef migrations list --no-connect --prefix-output \
          --project "$1" --startup-project "$1" --configuration Release \
      | sed -n 's/^data:[[:space:]]*//p' > "$2" \
      && test -s "$2" \
      && ! grep -v -E '^[0-9]{14}_[A-Za-z0-9_]+$' "$2" \
      && echo "$1 migrations in this build:" && cat "$2"; \
    } \
 && list GrifballWebApp.Migrations.SqlServer /app/migrations.txt \
 && mkdir -p /app/postgres \
 && list GrifballWebApp.Migrations.Postgres /app/postgres/migrations.txt

# sqlcmd (Microsoft's go-sqlcmd), for the migration Job's checks and backup and the backend's wait
# for it (charts/grifballwebapp). One static binary; Microsoft publishes no image with a shell. Pinned
# by checksum: a new version needs both checksums updated (sha256sum of each release asset).
FROM --platform=$BUILDPLATFORM alpine:3.22 AS sqlcmd
ARG TARGETARCH
ARG SQLCMD_VERSION=v1.10.0
RUN case "$TARGETARCH" in \
      amd64) sha=92516d98c63d99b0994de5b61350c91f6915f9b76f139a59039fbcb225c2e987 ;; \
      arm64) sha=9faaa981f9c374f319ac796dedb4678499b8596c87d5b6c512e9b0e7a3b74f8e ;; \
      *) echo "Unsupported TARGETARCH '$TARGETARCH'" >&2; exit 1 ;; \
    esac \
 && wget -q -O /tmp/sqlcmd.tar.bz2 \
      "https://github.com/microsoft/go-sqlcmd/releases/download/$SQLCMD_VERSION/sqlcmd-linux-$TARGETARCH.tar.bz2" \
 && echo "$sha  /tmp/sqlcmd.tar.bz2" | sha256sum -c - \
 && tar -xjf /tmp/sqlcmd.tar.bz2 -C /usr/local/bin sqlcmd

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=migrations /app/efbundle /app/efbundle
COPY --from=migrations /app/migrations.txt /app/migrations.txt
COPY --from=migrations /app/postgres /app/postgres
COPY --from=sqlcmd /usr/local/bin/sqlcmd /usr/local/bin/sqlcmd
ENTRYPOINT ["dotnet", "GrifballWebApp.Server.dll"]
