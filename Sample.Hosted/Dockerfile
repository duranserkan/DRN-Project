#https://aka.ms/containerfastmode
#https://blog.jetbrains.com/dotnet/2023/06/07/how-docker-fast-mode-works-in-rider/
#https://www.jetbrains.com/help/rider/Docker_fast_mode.html
ARG RUNTIME=linux-arm64
ARG PROJECT=Sample.Hosted
ARG SSLPORT=5999
#https://www.mytechramblings.com/posts/testing-chiseled-ubuntu-containers-with-dotnet/
#https://hub.docker.com/r/ubuntu/dotnet-aspnet use this dotnet deps with self contained apps are not supported with dockerfastmode
FROM ubuntu/dotnet-aspnet:7.0_edge as base
#https://learn.microsoft.com/en-us/aspnet/core/security/docker-https
#Bind ports from rider build configs or use docker compose
#https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints
ARG SSLPORT
EXPOSE $SSLPORT/udp
EXPOSE $SSLPORT/tcp
ENV ASPNETCORE_HTTPS_PORT=$SSLPORT
ENV ASPNETCORE_URLS=https://+:${SSLPORT}
#Create certificates then map volume in host
#dotnet dev-certs https -ep ${HOME}/.aspnet/https/aspnetapp.pfx -p <CREDENTIAL_PLACEHOLDER> 
#change drn with actual CREDENTIAL_PLACEHOLDER in ENV ASPNETCORE_Kestrel__Certificates__Default__Password
#dotnet dev-certs https --trust
#~/.aspnet/https:/https
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=drn
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
WORKDIR /app

#https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build
ARG RUNTIME
ARG PROJECT
WORKDIR /app
# Copy everything
COPY . ./
RUN dotnet restore "./${PROJECT}/${PROJECT}.csproj" -s "https://api.nuget.org/v3/index.json" --runtime ${RUNTIME}
RUN dotnet build "./${PROJECT}/${PROJECT}.csproj" -c Release --runtime ${RUNTIME} --no-restore
RUN dotnet publish "./${PROJECT}/${PROJECT}.csproj" -c Release -o /app/publish --no-restore --no-build --runtime ${RUNTIME}

# Build runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Set Entrypoint - ARGs are not allowed here. we need retype project name as we did in ARG PROJECT
ENTRYPOINT ["dotnet", "Sample.Hosted.dll"]