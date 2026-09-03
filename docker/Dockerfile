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

# Run as the non-root user the base image provides.
USER $APP_UID

COPY --from=backend /app/publish ./

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "Rundfrage.Api.dll"]
