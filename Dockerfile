# =========================
# Build Stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files first (better layer caching)
COPY *.sln ./
COPY Directory.Build.props ./
COPY nuget.config ./
COPY src/Template.Api/*.csproj ./src/Template.Api/
COPY tests/Template.UnitTests/*.csproj ./tests/Template.UnitTests/

# Restore dependencies (authenticate GitHub Packages via build secret)
RUN --mount=type=secret,id=github_token \
    TOKEN=$(cat /run/secrets/github_token 2>/dev/null) && \
    if [ -n "$TOKEN" ]; then \
      dotnet nuget update source github -u sassy-solutions -p "$TOKEN" --store-password-in-clear-text --configfile nuget.config; \
    fi && \
    dotnet restore

# Copy all source code
COPY . .

# Build and publish
ARG VERSION=1.0.0
ARG INFORMATIONAL_VERSION=1.0.0+local

RUN dotnet publish src/Template.Api/Template.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:Version=${VERSION} \
    /p:InformationalVersion=${INFORMATIONAL_VERSION}

# =========================
# Test Stage (optional, for CI)
# =========================
FROM build AS test
WORKDIR /src
RUN dotnet test --no-restore --verbosity normal --collect:"XPlat Code Coverage"

# =========================
# Runtime Stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime

# Security: Create non-root user and install curl for healthcheck
RUN addgroup -g 1000 appgroup && \
    adduser -u 1000 -G appgroup -s /bin/sh -D appuser && \
    apk add --no-cache curl

WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Security: Set ownership and switch to non-root user
RUN chown -R appuser:appgroup /app
USER appuser

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Template.Api.dll"]
