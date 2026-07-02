# Production image for AvantiPoint.Packages.Host (Docker Hub / registry publish).
# Local validation: use docker-compose.yml (sets ASPNETCORE_ENVIRONMENT=Docker, volumes, DB profiles).
# Build: docker build -t avantipoint/packages-host:latest .
# Run:   docker run -p 8080:8080 -v feed-data:/data avantipoint/packages-host:latest

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/host/AvantiPoint.Packages.Host/AvantiPoint.Packages.Host.csproj
RUN dotnet publish src/host/AvantiPoint.Packages.Host/AvantiPoint.Packages.Host.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
# ASPNETCORE_ENVIRONMENT is Production, so appsettings.Docker.json is never loaded here -
# this environment variable is the only thing that makes Data Protection key persistence
# effective in the published image (see Host:DataProtection:KeyPath in
# HostAdminServiceExtensions). Keep it on the same durable volume as the database (/data).
ENV Host__DataProtection__KeyPath=/data/dataprotection-keys
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AvantiPoint.Packages.Host.dll"]
