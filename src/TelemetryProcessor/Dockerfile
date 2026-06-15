# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy central package management files
COPY Directory.Build.props Directory.Packages.props ./

# Copy shared project files
COPY src/Shared/SignalBeam.Domain/SignalBeam.Domain.csproj src/Shared/SignalBeam.Domain/
COPY src/Shared/SignalBeam.Shared.Infrastructure/SignalBeam.Shared.Infrastructure.csproj src/Shared/SignalBeam.Shared.Infrastructure/
COPY src/SignalBeam.ServiceDefaults/SignalBeam.ServiceDefaults.csproj src/SignalBeam.ServiceDefaults/

# Copy service project files
COPY src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/SignalBeam.TelemetryProcessor.Application.csproj src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/
COPY src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/SignalBeam.TelemetryProcessor.Infrastructure.csproj src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/
COPY src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host/SignalBeam.TelemetryProcessor.Host.csproj src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host/

# Restore dependencies
RUN dotnet restore src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host/SignalBeam.TelemetryProcessor.Host.csproj

# Copy all source code
COPY src/Shared/ src/Shared/
COPY src/SignalBeam.ServiceDefaults/ src/SignalBeam.ServiceDefaults/
COPY src/TelemetryProcessor/ src/TelemetryProcessor/

# Build and publish
WORKDIR /src/src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

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

ENTRYPOINT ["dotnet", "SignalBeam.TelemetryProcessor.Host.dll"]
