# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.20 AS builder

WORKDIR /src

# Copy project file
COPY AgentApi.csproj .

# Restore dependencies
RUN dotnet restore "AgentApi.csproj"

# Copy source code
COPY . .

# Build the application
RUN dotnet build "AgentApi.csproj" -c Release -o /app/build

# Publish stage
FROM builder AS publish

RUN dotnet publish "AgentApi.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.20

WORKDIR /app

# Create non-root user
RUN addgroup -S dotnet && adduser -S dotnet -G dotnet

# Copy published application from publish stage
COPY --from=publish --chown=dotnet:dotnet /app/publish .

# Expose port (default ASP.NET Core port)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet-health || exit 1

# Switch to non-root user
USER dotnet

# Run the application
ENTRYPOINT ["dotnet", "AgentApi.dll"]
