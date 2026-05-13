# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY Server/BasicUdpGameServer/BasicUdpGameServer.csproj Server/BasicUdpGameServer/
RUN dotnet restore Server/BasicUdpGameServer/BasicUdpGameServer.csproj

COPY Server/BasicUdpGameServer/ Server/BasicUdpGameServer/
RUN dotnet publish Server/BasicUdpGameServer/BasicUdpGameServer.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 7777/udp
EXPOSE 8080/tcp
ENTRYPOINT ["dotnet", "BasicUdpGameServer.dll", "7777"]
