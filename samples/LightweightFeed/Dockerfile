FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet restore samples/LightweightFeed/LightweightFeed.csproj
RUN case "$TARGETARCH" in \
        amd64) runtime=linux-musl-x64 ;; \
        arm64) runtime=linux-musl-arm64 ;; \
        *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && dotnet publish samples/LightweightFeed/LightweightFeed.csproj \
        --configuration Release \
        --runtime "$runtime" \
        --self-contained false \
        --output /app/publish \
        /p:GeneratePackageOnBuild=false \
        /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Container
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
VOLUME ["/data"]
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --quiet --output-document=- http://127.0.0.1:8080/health > /dev/null || exit 1
ENTRYPOINT ["dotnet", "LightweightFeed.dll"]
