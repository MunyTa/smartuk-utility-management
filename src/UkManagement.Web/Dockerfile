FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /workspace

COPY src/UkManagement.Web/UkManagement.Web.csproj src/UkManagement.Web/
RUN dotnet restore src/UkManagement.Web/UkManagement.Web.csproj

COPY src/UkManagement.Web/ src/UkManagement.Web/
RUN dotnet publish src/UkManagement.Web/UkManagement.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "UkManagement.Web.dll"]
