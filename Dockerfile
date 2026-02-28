# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Hpoll.sln ./
COPY src/Hpoll.Core/Hpoll.Core.csproj src/Hpoll.Core/
COPY src/Hpoll.Data/Hpoll.Data.csproj src/Hpoll.Data/
COPY src/Hpoll.Email/Hpoll.Email.csproj src/Hpoll.Email/
COPY src/Hpoll.Worker/Hpoll.Worker.csproj src/Hpoll.Worker/
COPY src/Hpoll.Admin/Hpoll.Admin.csproj src/Hpoll.Admin/
COPY tests/Hpoll.Core.Tests/Hpoll.Core.Tests.csproj tests/Hpoll.Core.Tests/
COPY tests/Hpoll.Worker.Tests/Hpoll.Worker.Tests.csproj tests/Hpoll.Worker.Tests/

RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish src/Hpoll.Worker/Hpoll.Worker.csproj -c Release -o /app/publish --no-restore

# Runtime stage (worker service, no HTTP -- use smaller runtime image)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Create non-root user and data directory
RUN adduser --disabled-password --gecos "" appuser \
    && mkdir -p /app/data \
    && chown -R appuser:appuser /app

COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser

ENV DataPath=/app/data
ENV DOTNET_ENVIRONMENT=Production

VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Hpoll.Worker.dll"]
