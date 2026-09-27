#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER app
WORKDIR /app
EXPOSE 5295
EXPOSE 7210

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
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

# EF Core migrations bundle: one executable that applies pending migrations. Shipping it
# inside the backend image means the migrations that run before a deploy always come from the same
# build as the backend being deployed. The homelab's ArgoCD PreSync hook runs it with
# ConnectionStrings__GrifballWebApp set in the environment. Framework-dependent, like the app itself:
# it uses the .NET runtime already in the aspnet base image, and builds for whatever platform the
# image is built for, so nothing here names one.
FROM build AS migrations
RUN dotnet tool install --global dotnet-ef --version 10.0.0
ENV PATH="$PATH:/root/.dotnet/tools"
WORKDIR /src
RUN dotnet ef migrations bundle \
      --project GrifballWebApp.Database --startup-project GrifballWebApp.Database \
      --configuration Release -o /app/efbundle

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=migrations /app/efbundle /app/efbundle
ENTRYPOINT ["dotnet", "GrifballWebApp.Server.dll"]
