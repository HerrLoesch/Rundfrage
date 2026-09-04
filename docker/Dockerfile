# syntax=docker/dockerfile:1
#
# Multi-stage build producing the single application image. The Node stage builds the Vue
# frontend; the .NET publish stage receives its output as wwwroot/, so one container serves
# both the API and the web assets from one origin (FR-003, FR-003a, plan.md Structure Decision).

# --- Stage 1: build the frontend -----------------------------------------------------------
FROM node:24-alpine AS frontend
WORKDIR /src/frontend

# Copy manifests first so dependency installation is cached independently of source changes.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# --- Stage 2: build and publish the backend ------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend
WORKDIR /src

# Only the project file is needed to restore; the solution is a developer convenience.
COPY backend/src/Rundfrage.Api/Rundfrage.Api.csproj ./backend/src/Rundfrage.Api/
RUN dotnet restore backend/src/Rundfrage.Api/Rundfrage.Api.csproj

COPY backend/src/ ./backend/src/
RUN dotnet publish backend/src/Rundfrage.Api/Rundfrage.Api.csproj \
      -c Release -o /app/publish --no-restore

# The built SPA becomes the application's static content.
COPY --from=frontend /src/frontend/dist/ /app/publish/wwwroot/

# --- Stage 3: runtime ----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# The Alpine runtime image ships no zone data, so TimeZoneInfo.FindSystemTimeZoneById
# throws for "Europe/Berlin" - which FR-011a resolves every day boundary against, and
# which every poll creation needs to compute its retention deadline. The SDK image does
# carry zone data, so nothing outside this image can catch the omission (research.md R-6).
RUN apk add --no-cache tzdata

# The data directory, owned by the account the application runs as (003 FR-007a). Created here
# rather than left to the first start so that the ownership is right even when a named volume is
# mounted over it - Docker copies the image's ownership onto a fresh volume, and a directory that
# first appears at runtime would belong to root.
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data && chmod 700 /data

# Run as the non-root user the base image provides.
USER $APP_UID

COPY --from=backend /app/publish ./

ENV DATA_DIR=/data

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Liveness, so that an orchestrator can tell a working release from a broken one. Without it a
# container that restart-loops - on a missing ADMIN_USER, say, which the application refuses to
# start without - still counts as "running", and the deployment is reported as successful.
#
# It asks /api/v1/health, which answers without touching the storage. That is deliberate: FR-024
# keeps the application serving when its storage cannot be reached, so a check that failed on
# that would have the orchestrator kill or roll back a container behaving exactly as specified.
#
# wget is busybox's, which the Alpine base image already provides; there is no curl here.
# start-period covers the first start, where the schema is applied before the port opens.
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD wget --quiet --tries=1 --output-document=/dev/null \
        http://127.0.0.1:8080/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "Rundfrage.Api.dll"]
