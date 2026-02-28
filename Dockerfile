# 1. Base Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080 

# 2. Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["CarDealership.Api/CarDealership.Api.csproj", "CarDealership.Api/"]
RUN dotnet restore "CarDealership.Api/CarDealership.Api.csproj"
COPY . .
WORKDIR "/src/CarDealership.Api"
RUN dotnet build "CarDealership.Api.csproj" -c Release -o /app/build

# 3. Publish Stage
FROM build AS publish
RUN dotnet publish "CarDealership.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 4. Final Stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Bake the Data folder in as a default
COPY CarDealership.Api/Data ./Data

# Ensure permissions for the XML database (crucial for both internal & volume use)
USER root
RUN chmod -R 777 /app/Data

# --- PRO HEALTHCHECK ---
# Hits your internal /health endpoint. 
# 0 = healthy, 1 = unhealthy.
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CarDealership.Api.dll"]
