# 1. Base Runtime Stage (Alpine Linux for small size)
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

# 4. Final Stage (The production image)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# --- NEW: Bake the XML and XSD files into the image ---
# This copies your local 'CarDealership.Api/Data' folder to '/app/Data' inside the image
COPY CarDealership.Api/Data ./Data

# Crucial for Alpine: Ensure the app has permissions to write to the XML files
USER root
RUN chmod -R 777 /app/Data

ENTRYPOINT ["dotnet", "CarDealership.Api.dll"]
