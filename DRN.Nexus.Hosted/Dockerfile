#https://aka.ms/containerfastmode
#https://blog.jetbrains.com/dotnet/2023/06/07/how-docker-fast-mode-works-in-rider/
#https://www.jetbrains.com/help/rider/Docker_fast_mode.html
ARG PROJECT=DRN.Nexus.Hosted

#https://www.mytechramblings.com/posts/testing-chiseled-ubuntu-containers-with-dotnet/
#https://hub.docker.com/r/ubuntu/dotnet-aspnet use this dotnet deps with self contained apps are not supported with dockerfastmode
#https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/ enable buildkit to build multiplatform images
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0.7-noble-chiseled AS base
ARG TARGETARCH
ARG TARGETPLATFORM
ARG BUILDPLATFORM
#https://docs.docker.com/build/building/multi-platform/
ENV DRN_TARGET_ARCH=${TARGETARCH}
ENV DRN_TARGET_PLATFORM=${TARGETPLATFORM}
ENV DRN_BUILD_PLATFORM=${BUILDPLATFORM}

USER app
#Kubernetes can volume mount configs and settings to these directories
WORKDIR /appconfig/key-per-file-settings
WORKDIR /appconfig/json-settings
WORKDIR /app

#https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
#https://mcr.microsoft.com/en-us/artifact/mar/dotnet/sdk/tags alternative registry for bitnami/dotnet-sdk
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0.303-alpine3.22   AS build
ARG PROJECT
ARG TARGETARCH

WORKDIR /app
COPY . ./

#https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
RUN dotnet restore "./${PROJECT}/${PROJECT}.csproj"  -a "$TARGETARCH"
RUN dotnet build "./${PROJECT}/${PROJECT}.csproj" -c Release  --no-restore -a "$TARGETARCH"
RUN dotnet publish "./${PROJECT}/${PROJECT}.csproj" -c Release -o /app/publish --no-restore --no-build -a "$TARGETARCH"

# Build runtime image
FROM base AS final

COPY --from=build /app/publish .

# Set Entrypoint - ARGs are not allowed here. we need retype project name as we did in ARG PROJECT
ENTRYPOINT ["dotnet", "DRN.Nexus.Hosted.dll"]