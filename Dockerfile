# Build stage — TruvoID API v3.1.1 (cache-bust: 2026-08-29T06:00:00Z)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY src/TruvoID.Domain/TruvoID.Domain.csproj src/TruvoID.Domain/
COPY src/TruvoID.Core/TruvoID.Core.csproj src/TruvoID.Core/
COPY src/TruvoID.Infrastructure/TruvoID.Infrastructure.csproj src/TruvoID.Infrastructure/
COPY src/TruvoID.API/TruvoID.API.csproj src/TruvoID.API/
RUN dotnet restore src/TruvoID.API/TruvoID.API.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/TruvoID.API/TruvoID.API.csproj -c Release -o /app/out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "TruvoID.API.dll"]
