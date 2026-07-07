#https://aka.ms/containerfastmode
#https://blog.jetbrains.com/dotnet/2023/06/07/how-docker-fast-mode-works-in-rider/
#https://www.jetbrains.com/help/rider/Docker_fast_mode.html
#from solution root: docker build -f DRN.Nexus.Hosted/Dockerfile -t drn/nexus.hosted:localbuild .
ARG PROJECT=DRN.Nexus.Hosted
ARG DOTNET_RUNTIME_VERSION=10.0.9
ARG DOTNET_SDK_VERSION=10.0.301
ARG DOTNET_DISTRO=resolute
ARG APP_VERSION=0.0.1

#https://www.mytechramblings.com/posts/testing-chiseled-ubuntu-containers-with-dotnet/
#https://hub.docker.com/r/ubuntu/dotnet-aspnet use this dotnet deps with self contained apps are not supported with dockerfastmode
#https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/ enable buildkit to build multiplatform images
#https://mcr.microsoft.com/en-us/artifact/mar/dotnet/aspnet/tags alternative registry for ubuntu/dotnet-aspnet
#https://github.com/dotnet/dotnet-docker
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION}-${DOTNET_DISTRO}-chiseled-extra AS base

ARG APP_VERSION
ARG TARGETARCH
ARG TARGETPLATFORM
ARG BUILDPLATFORM
#https://docs.docker.com/build/building/multi-platform/

ENV APP_VERSION=${APP_VERSION}
# Default the image to Staging while allowing runtime Environment, DOTNET_ENVIRONMENT,
# mounted settings, or command-line values to override through DRN config precedence.
ENV ASPNETCORE_ENVIRONMENT=Staging
ENV DRN_TARGET_ARCH=${TARGETARCH}
ENV DRN_TARGET_PLATFORM=${TARGETPLATFORM}
ENV DRN_BUILD_PLATFORM=${BUILDPLATFORM}
ENV DrnAppDataSettings__DataPath=/appdata
ENV DrnAppDataSettings__TempPath=/apptemp
ENV NLog__variables__logPath=/applogs
ENV NLog__internalLogFile=/applogs/00-nlog-internal.log

LABEL org.opencontainers.image.version=${APP_VERSION} \
    org.opencontainers.image.vendor="Duran Serkan KILIC" \
    org.opencontainers.image.authors="Duran Serkan KILIC" \
    org.opencontainers.image.documentation="https://github.com/duranserkan/DRN-Project"

#https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
#https://mcr.microsoft.com/en-us/artifact/mar/dotnet/sdk/tags
#build image should use same distro with base image to prevent mismatches such as Alpine (Musl) vs. Ubuntu (Glibc)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}-${DOTNET_DISTRO} AS build
ARG PROJECT
ARG TARGETARCH
ARG DOTNET_RUNTIME_VERSION
ARG APP_VERSION

WORKDIR /app
COPY . ./

# The final-stage copy (COPY --chown=app:app) is the authoritative ownership fix.
RUN mkdir -p /appdata && touch /appdata/.keep && mkdir -p /apptemp && touch /apptemp/.keep && mkdir -p /applogs && touch /applogs/.keep

#https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
# Keep generated .deps.json framework metadata aligned with the runtime image patch level.
# https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props
RUN dotnet restore "./${PROJECT}/${PROJECT}.csproj" -a "$TARGETARCH" \
    -p:Configuration=Release \
    -p:RuntimeFrameworkVersion="$DOTNET_RUNTIME_VERSION" \
    -p:SelfContained=false \
    -p:UseAppHost=false
RUN dotnet build "./${PROJECT}/${PROJECT}.csproj" -c Release --no-restore -a "$TARGETARCH" \
    -p:RuntimeFrameworkVersion="$DOTNET_RUNTIME_VERSION" \
    -p:Version="$APP_VERSION" \
    -p:InformationalVersion="$APP_VERSION" \
    -p:SelfContained=false \
    -p:UseAppHost=false \
    -p:PreserveCompilationContext=false \
    -p:PreserveCompilationReferences=false \
    -p:CopyRefAssembliesToPublishDirectory=false
RUN dotnet publish "./${PROJECT}/${PROJECT}.csproj" -c Release -o /app/publish --no-restore --no-build -a "$TARGETARCH" \
    -p:RuntimeFrameworkVersion="$DOTNET_RUNTIME_VERSION" \
    -p:SelfContained=false \
    -p:UseAppHost=false \
    -p:PreserveCompilationContext=false \
    -p:PreserveCompilationReferences=false \
    -p:CopyRefAssembliesToPublishDirectory=false

# Build runtime image
FROM base AS final

#Kubernetes can volume mount configs and settings to these directories
WORKDIR /appconfig/key-per-file-settings
WORKDIR /appconfig/json-settings
WORKDIR /app

# 1654 is the default UID/GID for the non-root 'app' user in .NET container images.
COPY --chown=app:app --from=build /appdata /appdata
COPY --chown=app:app --from=build /apptemp /apptemp
COPY --chown=app:app --from=build /applogs /applogs
COPY --from=build /app/publish .

USER app

# Set Entrypoint - ARGs are not allowed here. we need retype project name as we did in ARG PROJECT
ENTRYPOINT ["dotnet", "DRN.Nexus.Hosted.dll"]
