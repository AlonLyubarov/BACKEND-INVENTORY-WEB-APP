# ── Build stage: restore + publish the .NET 10 API ──────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer-cached restore
COPY AlonProject.csproj ./
COPY AlonProject.Domain/*.csproj AlonProject.Domain/
COPY AlonProject.Application/*.csproj AlonProject.Application/
COPY AlonProject.Infrastructure/*.csproj AlonProject.Infrastructure/
RUN dotnet restore AlonProject.csproj

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish AlonProject.csproj -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage: ASP.NET Core runtime only (small image) ───────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The app listens on plain HTTP inside the container; nginx terminates TLS.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AlonProject.dll"]
