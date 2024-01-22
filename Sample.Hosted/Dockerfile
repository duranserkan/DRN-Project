#https://aka.ms/containerfastmode
#https://blog.jetbrains.com/dotnet/2023/06/07/how-docker-fast-mode-works-in-rider/
#https://www.jetbrains.com/help/rider/Docker_fast_mode.html
ARG BUILDPLATFORM=linux/arm64
ARG TARGETPLATFORM=linux/arm64
ARG PROJECT=Sample.Hosted
ARG SSLPORT=5999
ARG HTTPPORT=5998

#https://www.mytechramblings.com/posts/testing-chiseled-ubuntu-containers-with-dotnet/
#https://hub.docker.com/r/ubuntu/dotnet-aspnet use this dotnet deps with self contained apps are not supported with dockerfastmode
FROM --platform=$TARGETPLATFORM ubuntu/dotnet-aspnet:8.0_edge AS base
ARG TARGETPLATFORM
ARG BUILDPLATFORM
#https://learn.microsoft.com/en-us/aspnet/core/security/docker-https
#Bind ports from rider build configs or use docker compose
#https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints
ARG SSLPORT
ARG HTTPPORT
EXPOSE $SSLPORT/udp
EXPOSE $SSLPORT/tcp
EXPOSE $HTTPPORT/tcp

#Set the HTTPS redirect port. Used in enforcing HTTPS.
ENV ASPNETCORE_HTTPS_PORTS=${SSLPORT};
ENV ASPNETCORE_HTTP_PORTS=${HTTPPORT};
#https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-8.0
ENV ASPNETCORE_URLS=https://+:${SSLPORT};http://+:${HTTPPORT};

#Create certificates then map volume in host
#dotnet dev-certs https -ep ${HOME}/.aspnet/https/aspnetapp.pfx -p <CREDENTIAL_PLACEHOLDER> 
#change drn with actual CREDENTIAL_PLACEHOLDER in ENV ASPNETCORE_Kestrel__Certificates__Default__Password
#dotnet dev-certs https --trust
#~/.aspnet/https:/https
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=drn
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx

#https://docs.docker.com/build/building/multi-platform/
ENV DRN_BUILD_PLATFORM=${BUILDPLATFORM};
ENV DRN_TARGET_PLATFORM=${TARGETPLATFORM};

WORKDIR /app

#https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
FROM --platform=$BUILDPLATFORM bitnami/dotnet-sdk:8 AS build
ARG PROJECT
ARG TARGETPLATFORM
ARG BUILDPLATFORM

ENV DRN_TARGET_PLATFORM=${TARGETPLATFORM}
ENV DRN_BUILD_PLATFORM=${BUILDPLATFORM}

WORKDIR /app
COPY . ./

#https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
RUN if [ "$DRN_TARGET_PLATFORM" = "linux/arm64" ]; then echo "linux-arm64" > /drn_runtime; else echo "linux-x64" >/drn_runtime; fi
RUN DRN_RUNTIME=$(cat /drn_runtime); dotnet restore "./${PROJECT}/${PROJECT}.csproj" -s "https://api.nuget.org/v3/index.json" --arch $DRN_RUNTIME
RUN DRN_RUNTIME=$(cat /drn_runtime); dotnet build "./${PROJECT}/${PROJECT}.csproj" -c Release  --no-restore --arch $DRN_RUNTIME
RUN DRN_RUNTIME=$(cat /drn_runtime); dotnet publish "./${PROJECT}/${PROJECT}.csproj" -c Release -o /app/publish --no-restore --no-build --arch $DRN_RUNTIME
RUN DRN_RUNTIME=$(cat /drn_runtime); echo "Built by docker $DRN_BUILD_PLATFORM for docker $DRN_TARGET_PLATFORM with dotnet runtime $DRN_RUNTIME" > /buildlog.txt


# Build runtime image
FROM base AS final

WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /buildlog.txt .
# Set Entrypoint - ARGs are not allowed here. we need retype project name as we did in ARG PROJECT
ENTRYPOINT ["dotnet", "Sample.Hosted.dll"]