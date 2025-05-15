# Dockerfile

### 1) Build stage ###
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution & csproj(s), restore first to leverage layer caching
COPY *.sln .
COPY api.garagecom/*.csproj api.garagecom/
RUN dotnet restore

# Copy everything else and publish
COPY . .
WORKDIR /src/api.garagecom
RUN dotnet publish -c Release -o /app/publish

### 2) Runtime stage ###
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "api.garagecom.dll"]
