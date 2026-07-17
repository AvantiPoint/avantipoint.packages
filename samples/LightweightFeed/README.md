# LightweightFeed

LightweightFeed is a minimal NuGet-only host for local development and CI. It reads packages from
an existing NuGet global packages folder before contacting nuget.org. Packages served from the
mounted cache are streamed in place and are not copied into feed storage.

## Run locally

```shell
dotnet run --project samples/LightweightFeed/LightweightFeed.csproj
```

When `LocalCache:Path` is omitted, the application uses `NUGET_PACKAGES` and then
`~/.nuget/packages`. The NuGet service index is available at
`http://localhost:5000/v3/index.json` unless the ASP.NET Core URL configuration is overridden.

## Run with Docker Compose

Set `NUGET_PACKAGES_HOST` to the absolute path of the host global packages folder before starting
the service.

PowerShell:

```powershell
$env:NUGET_PACKAGES_HOST = Join-Path $HOME ".nuget\packages"
docker compose -f samples/LightweightFeed/docker-compose.yml up --build
```

Bash:

```shell
export NUGET_PACKAGES_HOST="$HOME/.nuget/packages"
docker compose -f samples/LightweightFeed/docker-compose.yml up --build
```

The container mounts the cache at `/nuget-cache` as read-only. Persistent feed data is stored in a
named volume at `/data`. The feed is available at `http://localhost:8080/v3/index.json`, and its
health endpoint is `http://localhost:8080/health`.

## Behavior

1. Existing feed storage is checked first.
2. The mounted NuGet global packages folder is checked next.
3. Cache misses are proxied to the sources in `NuGet.Config` without being persisted.

Set `LocalCache:CopyToFeedStorage` to `true` to copy cache hits into feed storage with cache-only
semantics. Copied packages do not create database rows and do not appear in search.
