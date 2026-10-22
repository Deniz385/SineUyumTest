# syntax=docker/dockerfile:1

# --- BUILD STAGE ---
# Restore + publish run in the SDK image; the output is copied into the runtime
# image, so the final image contains neither the SDK nor intermediate files.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the csproj first and restore — Docker caches this step as long as
# the dependency layer is unchanged, even when the source changes.
COPY SineUyum.Api/SineUyum.Api.csproj SineUyum.Api/
RUN dotnet restore SineUyum.Api/SineUyum.Api.csproj

# Copy the remaining source and publish.
COPY SineUyum.Api/ SineUyum.Api/
RUN dotnet publish SineUyum.Api/SineUyum.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# --- RUNTIME STAGE ---
# ASP.NET Core runtime only; smaller, with less attack surface.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# A separate directory is created for the database and owned by the non-root
# user so it can write there. Since the app runs without root it cannot write
# to /app; the default connection string points to this writable /data
# directory. Compose mounts a persistent volume here; this goes away once we
# move to PostgreSQL (M2).
RUN mkdir -p /data/media && chown -R $APP_UID /data
ENV ConnectionStrings__DefaultConnection="Data Source=/data/sineuyum.db"
# Uploaded profile photos (B8). Compose mounts a persistent volume here.
ENV Storage__MediaRoot=/data/media

# Run as the non-root user (provided by the aspnet image).
USER $APP_UID

# The container listens on 8080; map it to the host with -p 5074:8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# No health check is baked into the image: the aspnet image has no curl/wget
# and we do not install them to keep the image small. The app exposes a
# /health endpoint; the orchestrator should probe it — a Kubernetes httpGet
# probe needs no tools inside the container; see the README for a
# docker-compose example.

ENTRYPOINT ["dotnet", "SineUyum.Api.dll"]
