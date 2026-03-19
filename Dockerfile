# ── Build stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Restore (layer-cached by .csproj files)
COPY ElysStay.slnx .
COPY API/API.csproj API/
COPY Application/Application.csproj Application/
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
RUN dotnet restore

# Build
COPY . .
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Keycloak realm import is NOT needed in API container
# EF migrations run via Program.cs in Development only;
# in production, run `dotnet ef database update` separately or use a migration job.

ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081

USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8081/healthz || exit 1

ENTRYPOINT ["dotnet", "API.dll"]
