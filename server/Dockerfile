# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ["WorkTimeTracker.Server.csproj", "./"]
RUN dotnet restore "WorkTimeTracker.Server.csproj"

COPY . .
RUN dotnet publish "WorkTimeTracker.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Create persistent data directory
RUN mkdir -p /app/data && chmod 777 /app/data

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DATA_DIR=/app/data
ENV API_KEY=changeme_worktimetracker_key

EXPOSE 8080

ENTRYPOINT ["dotnet", "WorkTimeTracker.Server.dll"]
