FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["JustFSharpThings.Client/JustFSharpThings.Client.fsproj", "JustFSharpThings.Client/"]
RUN dotnet restore "JustFSharpThings.Client/JustFSharpThings.Client.fsproj"
COPY . .
WORKDIR "/src/JustFSharpThings.Client"
RUN dotnet build "./JustFSharpThings.Client.fsproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./JustFSharpThings.Client.fsproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JustFSharpThings.Client.dll"]
