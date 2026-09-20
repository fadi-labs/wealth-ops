# syntax=docker/dockerfile:1
#
# Multi-stage build for the WealthOps Host. Build it locally with Docker or
# Podman (`docker build -t wealth-ops:local .`) or let `docker-compose.yml` build
# it via `build: .`. The published GHCR image is produced from this file by
# .github/workflows/publish-image.yml.
#
# Layout invariant: the build stage WORKDIR is `/build`, NOT `/src`. The repo's
# own `src/` tree is copied in as `src/` beneath it, so `dotnet restore` reports
# clean repo-root-relative `src/WealthOps.*` paths. Using `/src` as the WORKDIR
# with `COPY src/ src/` produces a `/src/src/WealthOps.*` double path in the
# output — keep the working directory distinct from the copied `src/` folder.

# ── Build stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /build

# Central build/package management files first, then the source. Restoring the
# Host csproj transitively restores its referenced projects (Domain /
# Application / Infrastructure).
COPY WealthOps.slnx Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY src/ src/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/WealthOps.Host/WealthOps.Host.csproj
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/WealthOps.Host/WealthOps.Host.csproj \
      -c Release -o /app --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app ./

# Bind 5080 to match the launch-profile and docs convention. The aspnet image
# ships a non-root user; run as it.
ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080
USER $APP_UID

# No in-image HEALTHCHECK is defined; the alpine base ships wget, so a future
# change could add one in-image. For now probe http://localhost:5080/health
# from the host.
ENTRYPOINT ["./WealthOps.Host"]
