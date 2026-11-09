#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Reproducible: the same commit builds the same image, byte for byte (docs/reproducible-builds.md).
# Every input is pinned: the base images by digest, the Alpine packages by sha256 from the repo's own
# archive (apk.lock), NuGet and the downloads by version and checksum. CI passes SOURCE_DATE_EPOCH
# (the commit's time) and rewrites the layers' timestamps to it.

# Built for linux/amd64 and linux/arm64 (docker-publish.yml). The .NET stages run on the build
# machine's own platform (BUILDPLATFORM) and target the image's (TARGETARCH): the app is portable IL,
# the migrations bundle is built for the target's runtime, and nothing .NET runs under emulation.

# The Alpine packages the image adds (apk.lock), for the image's platform: downloaded by sha256 from
# the lock's own release, apk-<first 12 of the lock's sha256> (Alpine's mirrors keep only each
# package's newest version; .github/scripts/apk-archive.sh publishes one immutable release per lock),
# and checked against it. apk-lock.yml refreshes the lock. Fetched with curl, HTTPS only (redirects
# included): Alpine's own wget can't refuse a redirect to http. The SDK image (the build's own) has curl.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:5562fd626eec39f6f53218f9b7d7856ab7195d51d846568304fbe85071a3b6e6 AS apk
ARG TARGETARCH
ARG APK_RELEASES=https://github.com/PlayGrifball/GrifballWebApp/releases/download
COPY GrifballWebApp.Server/apk.lock /apk.lock
RUN case "$TARGETARCH" in \
      amd64) arch=x86_64 ;; \
      arm64) arch=aarch64 ;; \
      *) echo "Unsupported TARGETARCH '$TARGETARCH'" >&2; exit 1 ;; \
    esac \
 && release="$APK_RELEASES/apk-$(sha256sum /apk.lock | cut -c1-12)" \
 && mkdir /pkgs \
 && grep -v '^#' /apk.lock | awk -v arch="$arch" '$1 == arch { print $2, $3, $4 }' > /want \
 && test -s /want \
 && while read -r name version sha; do \
      file="$name-$version.apk"; \
      curl --proto =https --retry 3 -fsSL -o "/pkgs/$file" "$release/$sha.apk" \
      && echo "$sha  /pkgs/$file" | sha256sum -c - > /dev/null \
      || { echo "$file ($sha.apk): download failed or sha256 mismatch" >&2; exit 1; }; \
    done < /want

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:486297ae006e95ee2ee3112a2c03e3d26e41fbbc64b7a29e28e7a18cc36b12bb AS base

# The PostgreSQL client (psql, pg_dump, pg_restore): the Helm chart's migration Job and backups on
# Postgres (charts/grifballwebapp), as sqlcmd is on SQL Server. Its major version is the newest
# server's the chart supports: pg_dump can't dump a newer server. tzdata: the time zone database, which Alpine leaves
# out and TimeZoneInfo needs (the scheduler works in US Eastern time).
# From the archive above (apk.lock, which also lists every dependency they pull in), offline:
# apk still checks each package's Alpine signature. Its log (the time of each install) is dropped.
RUN --mount=type=bind,from=apk,source=/pkgs,target=/pkgs \
    apk add --no-network --no-cache /pkgs/*.apk \
 && rm -f /var/log/apk.log
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER app
WORKDIR /app
EXPOSE 5295
EXPOSE 7210

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:5562fd626eec39f6f53218f9b7d7856ab7195d51d846568304fbe85071a3b6e6 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
#COPY ["grifballwebapp.client/nuget.config", "grifballwebapp.client/"]
COPY ["GrifballWebApp.Server/GrifballWebApp.Server.csproj", "GrifballWebApp.Server/"]
#COPY ["grifballwebapp.client/grifballwebapp.client.esproj", "grifballwebapp.client/"]
#COPY ["GrifballWebApp.Database/GrifballWebApp.Database.csproj", "GrifballWebApp.Database/"]
RUN dotnet restore "./GrifballWebApp.Server/GrifballWebApp.Server.csproj"
COPY . .
# The app's GitInfo (the footer's BUILD: the short hash and the commit's date) comes from git
# (GrifballWebApp.Server.csproj, WriteGitInfo). From the commit alone, not the clone: the build's .git
# is swapped for one holding only this commit. Git lengthens a short hash when another object shares
# its prefix, so the hash's length would otherwise depend on how much history (and what later
# commits) the clone has.
RUN git clone -q --depth 1 --no-tags "file:///src" /tmp/head \
 && rm -rf .git && mv /tmp/head/.git .git && rm -rf /tmp/head
WORKDIR "/src/GrifballWebApp.Server"
RUN dotnet build "./GrifballWebApp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:DefineConstants=DOCKER_BUILD

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GrifballWebApp.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:DefineConstants=DOCKER_BUILD

# The converter between SQL Server and PostgreSQL (GrifballWebApp.Convert): copies a database into a
# new one on either server, row history included. The Helm chart's migration Job runs it on a first
# deploy (migrations.convert) as dotnet /app/convert/GrifballWebApp.Convert.dll. Its own folder: its
# dependencies are its own publish's.
FROM build AS convert
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "/src/GrifballWebApp.Convert/GrifballWebApp.Convert.csproj" -c $BUILD_CONFIGURATION -o /app/convert /p:UseAppHost=false

# EF Core migrations bundles: one executable per database that applies pending migrations - SQL
# Server's at /app/efbundle, Postgres's at /app/postgres/efbundle, SQLite's at /app/sqlite/efbundle, each
# from its own migrations project.
# Shipping them inside the backend image means the migrations a deploy runs always come from the same
# build as the backend being deployed; the Helm chart's migration Job (charts/grifballwebapp) runs the
# database's with ConnectionStrings__GrifballWebApp set in the environment. Framework-dependent: they
# use the .NET runtime already in the aspnet base image, built for the image's platform.
# They connect only when run, but building one still creates the design-time context, which wants a
# connection string: a dummy.
FROM build AS migrations
ARG TARGETARCH
# dotnet ef builds each bundle in a new random folder (/tmp/<random>/), whose path a bundle's debug
# info would carry: no debug info, so the same commit builds the same bundles. (An environment
# variable is an MSBuild property.)
ENV DebugType=none
RUN dotnet tool install --global dotnet-ef --version 10.0.14
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
      --configuration Release --target-runtime "$rid" -o /app/postgres/efbundle \
 && ConnectionStrings__GrifballWebApp="Data Source=unused.db" dotnet ef migrations bundle \
      --project GrifballWebApp.Migrations.Sqlite --startup-project GrifballWebApp.Migrations.Sqlite \
      --configuration Release --target-runtime "$rid" -o /app/sqlite/efbundle

# Each bundle's migration IDs, one per line, beside it: /app/migrations.txt (SQL Server) and
# /app/postgres/migrations.txt (and /app/sqlite/migrations.txt). The migration Job and the backend's
# wait compare them with the database's __EFMigrationsHistory. --no-connect lists them without a database; the design-time
# factory still wants a connection string, so it gets a dummy. The build fails if a list is empty or
# not the YYYYMMDDHHMMSS_Name shape they rely on.
RUN list() { \
      ConnectionStrings__GrifballWebApp="${3:-Server=unused;Database=unused}" \
        dotnet ef migrations list --no-connect --prefix-output \
          --project "$1" --startup-project "$1" --configuration Release \
      | sed -n 's/^data:[[:space:]]*//p' > "$2" \
      && test -s "$2" \
      && ! grep -v -E '^[0-9]{14}_[A-Za-z0-9_]+$' "$2" \
      && echo "$1 migrations in this build:" && cat "$2"; \
    } \
 && list GrifballWebApp.Migrations.SqlServer /app/migrations.txt \
 && mkdir -p /app/postgres \
 && list GrifballWebApp.Migrations.Postgres /app/postgres/migrations.txt \
 && mkdir -p /app/sqlite \
 && list GrifballWebApp.Migrations.Sqlite /app/sqlite/migrations.txt "Data Source=unused.db"

# Each migration's content, "<ID> <sha256 of its .cs and .Designer.cs>", beside the lists:
# /app/migration-hashes.txt and /app/postgres/migration-hashes.txt. A preview records them after each
# migrate, so a migration of the PR's own edited in place (the same ID, other content) resets its
# database like a renamed one (charts/grifballwebapp, files/preview-reset.sh).
RUN hashes() { \
      while read -r id; do \
        test -f "$1/Migrations/$id.cs" || { echo "$1/Migrations/$id.cs is missing" >&2; return 1; }; \
        printf '%s %s\n' "$id" "$(cat "$1/Migrations/$id.cs" "$1/Migrations/$id.Designer.cs" 2>/dev/null | sha256sum | cut -d' ' -f1)"; \
      done < "$2" > "$3" \
      && test -s "$3"; \
    } \
 && hashes GrifballWebApp.Migrations.SqlServer /app/migrations.txt /app/migration-hashes.txt \
 && hashes GrifballWebApp.Migrations.Postgres /app/postgres/migrations.txt /app/postgres/migration-hashes.txt

# master's migration IDs when this image was built, per provider (now, and every one it has ever had),
# into /app/master-migrations: a preview's migration Job resets its database only when the migrations
# it has that this image lacks are none of master's (charts/grifballwebapp, files/preview-reset.sh). CI
# writes them to .master-migrations in the build context (.github/scripts/master-migrations.sh); a
# build without them (a local one) leaves the folder empty, and a preview then never resets on its own.
RUN mkdir -p /app/master-migrations \
 && if [ -d .master-migrations ]; then \
      for f in sqlserver sqlserver-ever postgres postgres-ever; do \
        test -s ".master-migrations/$f.txt" \
        && ! grep -v -E '^[0-9]{14}_[A-Za-z0-9_]+$' ".master-migrations/$f.txt" \
        && cp ".master-migrations/$f.txt" /app/master-migrations/ \
        || { echo ".master-migrations/$f.txt is missing, empty or not migration IDs" >&2; exit 1; }; \
      done; \
      echo "master's migrations:"; cat /app/master-migrations/sqlserver.txt /app/master-migrations/postgres.txt; \
    else \
      echo "No .master-migrations in the build context: a preview of this image won't reset its database."; \
    fi

# sqlcmd (Microsoft's go-sqlcmd), for the migration Job's checks and backup and the backend's wait
# for it (charts/grifballwebapp). One static binary; Microsoft publishes no image with a shell. Pinned
# by checksum: a new version needs both checksums updated (sha256sum of each release asset). Fetched
# like the apk stage's packages: curl, HTTPS only.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:5562fd626eec39f6f53218f9b7d7856ab7195d51d846568304fbe85071a3b6e6 AS sqlcmd
ARG TARGETARCH
ARG SQLCMD_VERSION=v1.10.0
RUN case "$TARGETARCH" in \
      amd64) sha=92516d98c63d99b0994de5b61350c91f6915f9b76f139a59039fbcb225c2e987 ;; \
      arm64) sha=9faaa981f9c374f319ac796dedb4678499b8596c87d5b6c512e9b0e7a3b74f8e ;; \
      *) echo "Unsupported TARGETARCH '$TARGETARCH'" >&2; exit 1 ;; \
    esac \
 && curl --proto =https --retry 3 -fsSL -o /tmp/sqlcmd.tar.bz2 \
      "https://github.com/microsoft/go-sqlcmd/releases/download/$SQLCMD_VERSION/sqlcmd-linux-$TARGETARCH.tar.bz2" \
 && echo "$sha  /tmp/sqlcmd.tar.bz2" | sha256sum -c - \
 && tar -xjf /tmp/sqlcmd.tar.bz2 -C /usr/local/bin sqlcmd

FROM base AS final
# The commit, from CI (docker-publish.yml). Labels are part of the image, so they come from the
# commit alone: nothing here may depend on when or where it was built.
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.source="https://github.com/PlayGrifball/GrifballWebApp" \
      org.opencontainers.image.revision="${SOURCE_REVISION}" \
      org.opencontainers.image.title="GrifballWebApp server"
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=migrations /app/efbundle /app/efbundle
COPY --from=migrations /app/migrations.txt /app/migrations.txt
COPY --from=migrations /app/migration-hashes.txt /app/migration-hashes.txt
COPY --from=migrations /app/postgres /app/postgres
COPY --from=migrations /app/sqlite /app/sqlite
COPY --from=migrations /app/master-migrations /app/master-migrations
COPY --from=convert /app/convert /app/convert
# The bundles are single-file apps that unpack themselves before they run, into $HOME by default,
# which a read-only root filesystem doesn't allow. The chart's migrate container has /tmp writable.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
COPY --from=sqlcmd /usr/local/bin/sqlcmd /usr/local/bin/sqlcmd
ENTRYPOINT ["dotnet", "GrifballWebApp.Server.dll"]
