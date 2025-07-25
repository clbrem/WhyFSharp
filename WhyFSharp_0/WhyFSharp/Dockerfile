FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WhyFSharp_0/WhyFSharp/WhyFSharp.fsproj", "WhyFSharp_0/WhyFSharp/"]
RUN dotnet restore "WhyFSharp_0/WhyFSharp/WhyFSharp.fsproj"
COPY . .
WORKDIR "/src/WhyFSharp_0/WhyFSharp"
RUN dotnet build "./WhyFSharp.fsproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WhyFSharp.fsproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WhyFSharp.dll"]
