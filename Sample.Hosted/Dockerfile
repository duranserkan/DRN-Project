#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
#https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
#https://www.mytechramblings.com/posts/testing-chiseled-ubuntu-containers-with-dotnet/

ARG RUNTIME=linux-arm64
FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build-env
ARG RUNTIME

WORKDIR /app

# Copy everything
COPY . ./

# Restore packages
RUN dotnet restore "./Sample.Hosted/Sample.Hosted.csproj" \
	-s "https://api.nuget.org/v3/index.json" --runtime ${RUNTIME}	

# Build project
RUN dotnet build "./Sample.Hosted/Sample.Hosted.csproj" \ 
    -c Release \
	--runtime ${RUNTIME} \ 
	--self-contained true \	
	--no-restore

# Publish app
RUN dotnet publish "./Sample.Hosted/Sample.Hosted.csproj" \
	-c Release \
	-o /app/publish \
	--no-restore \ 
	--no-build \
	--self-contained true \
	--runtime ${RUNTIME}

# Build runtime image
FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:7.0-jammy-chiseled
EXPOSE 8080

# Copy artifact
WORKDIR /app
COPY --from=build-env /app/publish .

# Set Entrypoint
ENTRYPOINT ["./Sample.Hosted"]