#https://aka.ms/containerfastmode
#https://blog.jetbrains.com/dotnet/2023/06/07/how-docker-fast-mode-works-in-rider/
#https://www.jetbrains.com/help/rider/Docker_fast_mode.html
#from solution root: docker build -f Sample.Hosted/Dockerfile -t drn/sample.hosted:localbuild .
ARG PROJECT=Sample.Hosted
ARG DOTNET_RUNTIME_VERSION=10.0.8
ARG DOTNET_SDK_VERSION=10.0.300
ARG DOTNET_DISTRO=resolute

#https://www.mytechramblings.com/posts/testing-chiseled-ubuntu-containers-with-dotnet/
#https://hub.docker.com/r/ubuntu/dotnet-aspnet use this dotnet deps with self contained apps are not supported with dockerfastmode
#https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/ enable buildkit to build multiplatform images
#https://mcr.microsoft.com/en-us/artifact/mar/dotnet/aspnet/tags alternative registry for ubuntu/dotnet-aspnet
#https://github.com/dotnet/dotnet-docker
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION}-${DOTNET_DISTRO}-chiseled-extra AS base
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
#https://mcr.microsoft.com/en-us/artifact/mar/dotnet/sdk/tags
#build image should use same distro with base image to prevent mismatches such as Alpine (Musl) vs. Ubuntu (Glibc)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}-${DOTNET_DISTRO} AS build
ARG PROJECT
ARG TARGETARCH
ARG DOTNET_RUNTIME_VERSION

WORKDIR /app
COPY . ./

#https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
# Keep generated .deps.json framework metadata aligned with the runtime image patch level.
# https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props
RUN dotnet restore "./${PROJECT}/${PROJECT}.csproj" -a "$TARGETARCH" \
    -p:RuntimeFrameworkVersion="$DOTNET_RUNTIME_VERSION" \
    -p:SelfContained=false \
    -p:UseAppHost=false
RUN dotnet build "./${PROJECT}/${PROJECT}.csproj" -c Release --no-restore -a "$TARGETARCH" \
    -p:RuntimeFrameworkVersion="$DOTNET_RUNTIME_VERSION" \
    -p:SelfContained=false \
    -p:UseAppHost=false
RUN dotnet publish "./${PROJECT}/${PROJECT}.csproj" -c Release -o /app/publish --no-restore --no-build -a "$TARGETARCH" \
    -p:RuntimeFrameworkVersion="$DOTNET_RUNTIME_VERSION" \
    -p:SelfContained=false \
    -p:UseAppHost=false

# Build runtime image
FROM base AS final

COPY --from=build /app/publish .

# Set Entrypoint - ARGs are not allowed here. we need retype project name as we did in ARG PROJECT
ENTRYPOINT ["dotnet", "Sample.Hosted.dll"]
