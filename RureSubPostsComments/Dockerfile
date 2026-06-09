# Stage 1: Runtime Base
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

USER app
WORKDIR /app

# Stage 2: SDK Build Environment
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the project file first to cache NuGet dependencies
COPY ["RureSubPostsComments.csproj", "."]
RUN dotnet restore "RureSubPostsComments.csproj"

# Copy everything else and build the application
COPY . .
RUN dotnet build "RureSubPostsComments.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RureSubPostsComments.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final minimal production image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RureSubPostsComments.dll"]