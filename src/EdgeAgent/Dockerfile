# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy Directory.Build.props and Directory.Packages.props for central package management
COPY Directory.Build.props Directory.Packages.props ./

# Copy shared project files
COPY src/Shared/SignalBeam.Domain/SignalBeam.Domain.csproj src/Shared/SignalBeam.Domain/
COPY src/Shared/SignalBeam.Shared.Infrastructure/SignalBeam.Shared.Infrastructure.csproj src/Shared/SignalBeam.Shared.Infrastructure/

# Copy EdgeAgent project files
COPY src/EdgeAgent/SignalBeam.EdgeAgent.Application/SignalBeam.EdgeAgent.Application.csproj src/EdgeAgent/SignalBeam.EdgeAgent.Application/
COPY src/EdgeAgent/SignalBeam.EdgeAgent.Infrastructure/SignalBeam.EdgeAgent.Infrastructure.csproj src/EdgeAgent/SignalBeam.EdgeAgent.Infrastructure/
COPY src/EdgeAgent/SignalBeam.EdgeAgent.Host/SignalBeam.EdgeAgent.Host.csproj src/EdgeAgent/SignalBeam.EdgeAgent.Host/

# Restore dependencies
RUN dotnet restore src/EdgeAgent/SignalBeam.EdgeAgent.Host/SignalBeam.EdgeAgent.Host.csproj

# Copy all source code
COPY src/Shared/ src/Shared/
COPY src/EdgeAgent/ src/EdgeAgent/

# Build and publish
WORKDIR /src/src/EdgeAgent/SignalBeam.EdgeAgent.Host
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine

# Install Docker CLI
RUN apk add --no-cache docker-cli

# Create non-root user
RUN addgroup -S signalbeam && adduser -S signalbeam -G signalbeam

# Create directories
RUN mkdir -p /var/lib/signalbeam /var/log/signalbeam && \
    chown -R signalbeam:signalbeam /var/lib/signalbeam /var/log/signalbeam

WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R signalbeam:signalbeam /app

# Switch to non-root user
USER signalbeam

# Environment variables
ENV DOTNET_ENVIRONMENT=Production
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Volume for persistent data
VOLUME ["/var/lib/signalbeam", "/var/log/signalbeam"]

# Entry point
ENTRYPOINT ["dotnet", "SignalBeam.EdgeAgent.Host.dll"]
CMD ["run"]
