# =============================================================================
# Pagly - Multi-stage Dockerfile for CapRover
# Build: docker build -t planilla-test --build-arg VITE_API_URL= .
# Run:   docker run -p 8080:80 -e ConnectionStrings__DefaultConnection="..." planilla-test
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Build React SPA (Vite)
# -----------------------------------------------------------------------------
ARG NODE_VERSION=20-alpine
FROM node:${NODE_VERSION} AS frontend-build

WORKDIR /app/client

# Build-time: URL del API en producción (mismo origen si vacío)
ARG VITE_API_URL=
ENV VITE_API_URL=${VITE_API_URL}

COPY src/UI/Planilla.Web/ClientApp/package.json src/UI/Planilla.Web/ClientApp/package-lock.json ./
RUN npm ci

COPY src/UI/Planilla.Web/ClientApp/ ./
RUN npm run build

# Vite outDir ../wwwroot from /app/client → /app/wwwroot
RUN test -f /app/wwwroot/index.html || (echo "Frontend build failed: wwwroot not found" && exit 1)

# -----------------------------------------------------------------------------
# Stage 2: Build .NET API and copy wwwroot
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build

WORKDIR /src

COPY Planilla.sln ./
COPY src/Core/Planilla.Domain/*.csproj src/Core/Planilla.Domain/
COPY src/Core/Planilla.Application/*.csproj src/Core/Planilla.Application/
COPY src/Infrastructure/Planilla.Infrastructure/*.csproj src/Infrastructure/Planilla.Infrastructure/
COPY src/UI/Planilla.Web/*.csproj src/UI/Planilla.Web/

RUN dotnet restore src/UI/Planilla.Web/Vorluno.Planilla.Web.csproj

COPY src/ ./

# Copy SPA build from previous stage into wwwroot for publish
COPY --from=frontend-build /app/wwwroot src/UI/Planilla.Web/wwwroot

RUN dotnet publish src/UI/Planilla.Web/Vorluno.Planilla.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# -----------------------------------------------------------------------------
# Stage 3: Runtime
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=backend-build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80

# Migraciones se ejecutan al arranque en Program.cs antes de escuchar.
# CapRover puede usar la URL /health para health check (GET devuelve JSON).
# HEALTHCHECK omitido: imagen runtime no incluye wget/curl; configurar en CapRover.

ENTRYPOINT ["dotnet", "Vorluno.Planilla.Web.dll"]
