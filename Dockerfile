FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore UrlShortener.slnx
RUN dotnet publish src/UrlShortener.Api/UrlShortener.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "UrlShortener.Api.dll"]
