#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 5295
EXPOSE 7210

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GrifballWebApp.Server.dll"]