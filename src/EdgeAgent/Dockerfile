# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY SignalBeam.EdgeAgent.Application/SignalBeam.EdgeAgent.Application.csproj SignalBeam.EdgeAgent.Application/
COPY SignalBeam.EdgeAgent.Infrastructure/SignalBeam.EdgeAgent.Infrastructure.csproj SignalBeam.EdgeAgent.Infrastructure/
COPY SignalBeam.EdgeAgent.Host/SignalBeam.EdgeAgent.Host.csproj SignalBeam.EdgeAgent.Host/

# Restore dependencies
RUN dotnet restore SignalBeam.EdgeAgent.Host/SignalBeam.EdgeAgent.Host.csproj

# Copy all source code
COPY . .

# Build and publish
WORKDIR /src/SignalBeam.EdgeAgent.Host
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine

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
