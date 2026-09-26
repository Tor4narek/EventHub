FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
COPY ["certs/russian_trusted_root_ca.crt", "/usr/local/share/ca-certificates/"]
RUN update-ca-certificates
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EventHubAPI/EventHubAPI.csproj", "EventHubAPI/"]
COPY ["Services/Services.csproj", "Services/"]
COPY ["EventImport/EventImport.csproj", "EventImport/"]
COPY ["Storage/Storage.csproj", "Storage/"]
COPY ["ImageStorage/ImageStorage.csproj", "ImageStorage/"]
COPY ["MaxBotCore/MaxBotCore.csproj", "MaxBotCore/"]
COPY ["Scheduler/Scheduler.csproj", "Scheduler/"]
RUN dotnet restore "EventHubAPI/EventHubAPI.csproj"
COPY . .
WORKDIR "/src/EventHubAPI"
RUN dotnet build "./EventHubAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EventHubAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EventHubAPI.dll"]
