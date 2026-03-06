# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy central package management files
COPY Directory.Build.props Directory.Packages.props ./

# Copy shared project files
COPY src/Shared/SignalBeam.Domain/SignalBeam.Domain.csproj src/Shared/SignalBeam.Domain/
COPY src/Shared/SignalBeam.Shared.Infrastructure/SignalBeam.Shared.Infrastructure.csproj src/Shared/SignalBeam.Shared.Infrastructure/
COPY src/SignalBeam.ServiceDefaults/SignalBeam.ServiceDefaults.csproj src/SignalBeam.ServiceDefaults/

# Copy service project files
COPY src/DeviceManager/SignalBeam.DeviceManager.Application/SignalBeam.DeviceManager.Application.csproj src/DeviceManager/SignalBeam.DeviceManager.Application/
COPY src/DeviceManager/SignalBeam.DeviceManager.Infrastructure/SignalBeam.DeviceManager.Infrastructure.csproj src/DeviceManager/SignalBeam.DeviceManager.Infrastructure/
COPY src/DeviceManager/SignalBeam.DeviceManager.Host/SignalBeam.DeviceManager.Host.csproj src/DeviceManager/SignalBeam.DeviceManager.Host/

# Restore dependencies
RUN dotnet restore src/DeviceManager/SignalBeam.DeviceManager.Host/SignalBeam.DeviceManager.Host.csproj

# Copy all source code
COPY src/Shared/ src/Shared/
COPY src/SignalBeam.ServiceDefaults/ src/SignalBeam.ServiceDefaults/
COPY src/DeviceManager/ src/DeviceManager/

# Build and publish
WORKDIR /src/src/DeviceManager/SignalBeam.DeviceManager.Host
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine

# Create non-root user
RUN addgroup -S signalbeam && adduser -S signalbeam -G signalbeam

WORKDIR /app
COPY --from=build /app/publish .
RUN chown -R signalbeam:signalbeam /app

USER signalbeam

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_ENVIRONMENT=Production
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

EXPOSE 8080

ENTRYPOINT ["dotnet", "SignalBeam.DeviceManager.Host.dll"]
