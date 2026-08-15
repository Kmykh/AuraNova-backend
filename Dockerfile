# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["AuraNova.sln", "./"]
COPY ["src/AuraNova.Domain/AuraNova.Domain.csproj", "src/AuraNova.Domain/"]
COPY ["src/AuraNova.Application/AuraNova.Application.csproj", "src/AuraNova.Application/"]
COPY ["src/AuraNova.Infrastructure/AuraNova.Infrastructure.csproj", "src/AuraNova.Infrastructure/"]
COPY ["src/AuraNova.API/AuraNova.API.csproj", "src/AuraNova.API/"]
COPY ["tests/AuraNova.UnitTests/AuraNova.UnitTests.csproj", "tests/AuraNova.UnitTests/"]
COPY ["tests/AuraNova.IntegrationTests/AuraNova.IntegrationTests.csproj", "tests/AuraNova.IntegrationTests/"]

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY . .

# Build and Publish the API
WORKDIR "/src/src/AuraNova.API"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Run as non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copy published artifacts
COPY --from=build /app/publish .

# Use the PORT environment variable provided by Railway (defaulting to 8080)
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

ENTRYPOINT ["dotnet", "AuraNova.API.dll"]
