# One image recipe for every .NET service. Build with --build-arg PROJECT=<path to .csproj>;
# compose.yaml sets PROJECT and the command (the service's dll).
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
ARG PROJECT
WORKDIR /src

# Restore from project files only, so code edits reuse the cached package layer.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Contracts/OrderFlow.Contracts/OrderFlow.Contracts.csproj src/Contracts/OrderFlow.Contracts/
COPY src/BuildingBlocks/OrderFlow.BuildingBlocks/OrderFlow.BuildingBlocks.csproj src/BuildingBlocks/OrderFlow.BuildingBlocks/
COPY src/Services/Catalog/Catalog.Api/Catalog.Api.csproj src/Services/Catalog/Catalog.Api/
COPY src/Services/Ordering/Ordering.Api/Ordering.Api.csproj src/Services/Ordering/Ordering.Api/
COPY src/Gateway/Gateway.Api/Gateway.Api.csproj src/Gateway/Gateway.Api/
RUN dotnet restore "$PROJECT"

COPY src/ src/
RUN dotnet publish "$PROJECT" -c Release --no-restore -o /app /p:UseAppHost=false

# "-extra" adds ICU: Microsoft.Data.SqlClient refuses to run in invariant globalization mode.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet"]
