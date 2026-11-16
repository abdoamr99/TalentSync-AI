# Multi-stage build for ASP.NET Core (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project definition files for dependency restore caching
COPY TalentSyncAI.Domain/TalentSyncAI.Domain.csproj TalentSyncAI.Domain/
COPY TalentSyncAI.Application/TalentSyncAI.Application.csproj TalentSyncAI.Application/
COPY TalentSyncAI.Infrastructure/TalentSyncAI.Infrastructure.csproj TalentSyncAI.Infrastructure/
COPY TalentSyncAI.API/TalentSyncAI.API.csproj TalentSyncAI.API/

RUN dotnet restore TalentSyncAI.API/TalentSyncAI.API.csproj

# Copy all source code
COPY . .

# Build and publish release binaries
WORKDIR /src/TalentSyncAI.API
RUN dotnet publish TalentSyncAI.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Create uploads storage directory
RUN mkdir -p /app/Storage/Uploads

# Default container port for .NET 8/9/10
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TalentSyncAI.API.dll"]
