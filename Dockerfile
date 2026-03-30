# syntax=docker/dockerfile:1

# Build stage: compile the project with native AOT
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install native AOT prerequisites (clang for linking, zlib1g-dev for compression support)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        clang \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Copy solution and project files first to enable NuGet restore layer caching
COPY Source/Cyborg.slnx Source/Directory.Build.props Source/
COPY Source/Cyborg.Cli/Cyborg.Cli.csproj Source/Cyborg.Cli/
COPY Source/Cyborg.Core/Cyborg.Core.csproj Source/Cyborg.Core/
COPY Source/Cyborg.Core.Aot/Cyborg.Core.Aot.csproj Source/Cyborg.Core.Aot/
COPY Source/Cyborg.Modules/Cyborg.Modules.csproj Source/Cyborg.Modules/
COPY Source/Cyborg.Modules.Borg/Cyborg.Modules.Borg.csproj Source/Cyborg.Modules.Borg/

# Restore NuGet packages for the target runtime
RUN dotnet restore Source/Cyborg.Cli/Cyborg.Cli.csproj --runtime linux-x64

# Copy remaining source files
COPY Source/ Source/

# Publish the native AOT binary for linux-x64
RUN dotnet publish Source/Cyborg.Cli/Cyborg.Cli.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --no-restore \
    --output /artifacts

# Artifact stage: contains only the published native binary for host export
FROM scratch AS artifact
COPY --from=build /artifacts/Cyborg.Cli /Cyborg.Cli
