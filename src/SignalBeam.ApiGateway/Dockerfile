# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy central package management files
COPY Directory.Build.props Directory.Packages.props ./

# Copy shared project files
COPY src/SignalBeam.ServiceDefaults/SignalBeam.ServiceDefaults.csproj src/SignalBeam.ServiceDefaults/

# Copy service project file
COPY src/SignalBeam.ApiGateway/SignalBeam.ApiGateway.csproj src/SignalBeam.ApiGateway/

# Restore dependencies
RUN dotnet restore src/SignalBeam.ApiGateway/SignalBeam.ApiGateway.csproj

# Copy all source code
COPY src/SignalBeam.ServiceDefaults/ src/SignalBeam.ServiceDefaults/
COPY src/SignalBeam.ApiGateway/ src/SignalBeam.ApiGateway/

# Build and publish
WORKDIR /src/src/SignalBeam.ApiGateway
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

ENTRYPOINT ["dotnet", "SignalBeam.ApiGateway.dll"]
