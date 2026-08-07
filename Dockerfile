# Multi-stage build: compile with the full SDK, ship only the smaller ASP.NET runtime image.
#
# A single unified `dotnet publish` (restore+build+publish combined) rather than a separate cached
# `dotnet restore` step run against bare .csproj files: splitting them caused the Blazor Interactive
# Server runtime asset (wwwroot/_framework/blazor.web.js) to go missing from the published output on
# this image's Linux SDK — the app rendered fine but every InputFile/component interaction silently
# died on since the browser had no client-side runtime to hydrate with. This costs a bit of Docker
# layer-cache efficiency (every source change re-restores), which is an acceptable trade for a
# three-person app deployed a handful of times, not something rebuilt continuously.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ src/
RUN dotnet publish src/PrivateExpenses.Web/PrivateExpenses.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "PrivateExpenses.Web.dll"]
