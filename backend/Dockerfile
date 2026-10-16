# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless project files change).
COPY Directory.Build.props Directory.Packages.props JobTrack.slnx ./
COPY src/JobTrack.Domain/JobTrack.Domain.csproj src/JobTrack.Domain/
COPY src/JobTrack.Application/JobTrack.Application.csproj src/JobTrack.Application/
COPY src/JobTrack.Infrastructure/JobTrack.Infrastructure.csproj src/JobTrack.Infrastructure/
COPY src/JobTrack.Api/JobTrack.Api.csproj src/JobTrack.Api/
RUN dotnet restore src/JobTrack.Api/JobTrack.Api.csproj

COPY src/ src/
RUN dotnet publish src/JobTrack.Api/JobTrack.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .
# Private storage for uploaded CVs (mounted as a volume); owned by the non-root user.
RUN mkdir -p /app/App_Data/uploads && chown -R app:app /app/App_Data

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "JobTrack.Api.dll"]
