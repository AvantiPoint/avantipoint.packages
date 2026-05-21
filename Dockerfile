FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/host/AvantiPoint.Packages.Host/AvantiPoint.Packages.Host.csproj
RUN dotnet publish src/host/AvantiPoint.Packages.Host/AvantiPoint.Packages.Host.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AvantiPoint.Packages.Host.dll"]
