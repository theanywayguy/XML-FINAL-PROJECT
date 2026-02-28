# 1. Base Runtime Stage (Extremely small Alpine Linux image)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
# .NET 8+ defaults to port 8080 inside the container
EXPOSE 8080 

# 2. Build Stage (Heavy SDK to compile the code)
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy just the project file first to cache dependencies
COPY ["CarDealership.Api/CarDealership.Api.csproj", "CarDealership.Api/"]
RUN dotnet restore "CarDealership.Api/CarDealership.Api.csproj"

# Copy the rest of the code (respecting .dockerignore)
COPY . .
WORKDIR "/src/CarDealership.Api"
RUN dotnet build "CarDealership.Api.csproj" -c Release -o /app/build

# 3. Publish Stage (Optimizes the output)
FROM build AS publish
RUN dotnet publish "CarDealership.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 4. Final Stage (Take the tiny base, copy the published files, and run it)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Tell Docker how to start the app
ENTRYPOINT ["dotnet", "CarDealership.Api.dll"]