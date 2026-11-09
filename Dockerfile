# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/TodoList.Domain/TodoList.Domain.csproj src/TodoList.Domain/
COPY src/TodoList.Application/TodoList.Application.csproj src/TodoList.Application/
COPY src/TodoList.Infrastructure/TodoList.Infrastructure.csproj src/TodoList.Infrastructure/
COPY src/TodoList.Web/TodoList.Web.csproj src/TodoList.Web/
RUN dotnet restore src/TodoList.Web/TodoList.Web.csproj

COPY src/ src/
RUN dotnet publish src/TodoList.Web/TodoList.Web.csproj \
    --configuration Release \
    --output /app \
    --no-restore \
    /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql probes for GSSAPI; installing it avoids a loader warning on connect.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "TodoList.Web.dll"]
