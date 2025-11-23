# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files
COPY pingpong.sln .
COPY src/PingPong.Api/PingPong.Api.csproj src/PingPong.Api/
COPY src/PingPong.Application/PingPong.Application.csproj src/PingPong.Application/
COPY src/PingPong.Domain/PingPong.Domain.csproj src/PingPong.Domain/
COPY src/PingPong.Infrastructure/PingPong.Infrastructure.csproj src/PingPong.Infrastructure/
COPY tests/PingPong.Tests/PingPong.Tests.csproj tests/PingPong.Tests/

# Restore dependencies
RUN dotnet restore

# Copy everything else
COPY . .

# Build and publish the application
WORKDIR /source/src/PingPong.Api
RUN dotnet publish -c Release -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app .

# Create a non-root user
RUN useradd -m -u 1001 appuser && chown -R appuser:appuser /app
USER appuser

# Expose port (fly.io uses 8080 by default)
EXPOSE 8080

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Run the application
ENTRYPOINT ["dotnet", "PingPong.Api.dll"]

