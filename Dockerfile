FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Pw.Modules.Api/Pw.Modules.Api.csproj", "Pw.Modules.Api/"]
RUN dotnet restore "Pw.Modules.Api/Pw.Modules.Api.csproj"
COPY . .
WORKDIR "/src/Pw.Modules.Api"
RUN dotnet build "./Pw.Modules.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Pw.Modules.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pw.Modules.Api.dll"]
