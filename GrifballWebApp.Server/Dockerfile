#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Built for linux/amd64 and linux/arm64 (docker-publish.yml). The .NET stages run on the build
# machine's own platform (BUILDPLATFORM) and target the image's (TARGETARCH): the app is portable IL,
# the migrations bundle is built for the target's runtime, and nothing .NET runs under emulation.

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

RUN apk add --no-cache icu-libs
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

# EF Core migrations bundle: one executable that applies pending migrations. Shipping it inside the
# backend image means the migrations a deploy runs always come from the same build as the backend
# being deployed; the Helm chart's migration Job (charts/grifballwebapp) runs it with
# ConnectionStrings__GrifballWebApp set in the environment. Framework-dependent: it uses the .NET
# runtime already in the aspnet base image, built for the image's platform.
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
 && dotnet ef migrations bundle \
      --project GrifballWebApp.Database --startup-project GrifballWebApp.Database \
      --configuration Release --target-runtime "$rid" -o /app/efbundle

# The build's migration IDs, one per line. The migration Job and the backend's wait compare them with
# the database's __EFMigrationsHistory. --no-connect lists them without a database; the design-time
# factory still wants a connection string, so it gets a dummy. The build fails if the list is empty
# or not the YYYYMMDDHHMMSS_Name shape they rely on.
RUN ConnectionStrings__GrifballWebApp="Server=unused;Database=unused" \
      dotnet ef migrations list --no-connect --prefix-output \
        --project GrifballWebApp.Database --startup-project GrifballWebApp.Database \
        --configuration Release \
    | sed -n 's/^data:[[:space:]]*//p' > /app/migrations.txt \
 && test -s /app/migrations.txt \
 && ! grep -v -E '^[0-9]{14}_[A-Za-z0-9_]+$' /app/migrations.txt \
 && echo "Migrations in this build:" && cat /app/migrations.txt

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
COPY --from=sqlcmd /usr/local/bin/sqlcmd /usr/local/bin/sqlcmd
ENTRYPOINT ["dotnet", "GrifballWebApp.Server.dll"]
