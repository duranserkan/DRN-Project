#https://aka.ms/containerfastmode
#https://blog.jetbrains.com/dotnet/2023/06/07/how-docker-fast-mode-works-in-rider/
#https://www.jetbrains.com/help/rider/Docker_fast_mode.html
ARG PROJECT=DRN.Nexus.Hosted
ARG SSLPORT=5989
ARG HTTPPORT=5988

#https://www.mytechramblings.com/posts/testing-chiseled-ubuntu-containers-with-dotnet/
#https://hub.docker.com/r/ubuntu/dotnet-aspnet use this dotnet deps with self contained apps are not supported with dockerfastmode
#https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/ enable buildkit to build multiplatform images
FROM --platform=$TARGETPLATFORM ubuntu/dotnet-aspnet:8.0_edge AS base
ARG SSLPORT
ARG HTTPPORT
ARG TARGETARCH
ARG TARGETPLATFORM
ARG BUILDPLATFORM
#https://docs.docker.com/build/building/multi-platform/
ENV DRN_TARGET_ARCH=${TARGETARCH}
ENV DRN_TARGET_PLATFORM=${TARGETPLATFORM}
ENV DRN_BUILD_PLATFORM=${BUILDPLATFORM}

#https://learn.microsoft.com/en-us/aspnet/core/security/docker-https
#Bind ports from rider build configs or use docker compose
#https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints

EXPOSE $SSLPORT/tcp
EXPOSE $HTTPPORT/tcp

#Set the HTTPS redirect port. Used for enforcing HTTPS.
ENV ASPNETCORE_HTTPS_PORTS=${SSLPORT};
ENV ASPNETCORE_HTTP_PORTS=${HTTPPORT};
#https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-8.0
ENV ASPNETCORE_URLS=http://+:${HTTPPORT};
#ENV ASPNETCORE_URLS=https://+:${SSLPORT};http://+:${HTTPPORT};

#Create certificates then map volume in host
#dotnet dev-certs https -ep ${HOME}/.aspnet/https/aspnetapp.pfx -p <CREDENTIAL_PLACEHOLDER>
#change drn with actual CREDENTIAL_PLACEHOLDER in ENV ASPNETCORE_Kestrel__Certificates__Default__Password
#dotnet dev-certs https --trust
#~/.aspnet/https:/https
#ENV ASPNETCORE_Kestrel__Certificates__Default__Password=drn
#ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx

USER app
#Kubernetes can volume mount configs and settings to these directories
WORKDIR /appconfig/key-per-file-settings
WORKDIR /appconfig/json-settings
WORKDIR /app

#https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
FROM --platform=$BUILDPLATFORM bitnami/dotnet-sdk:8 AS build
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