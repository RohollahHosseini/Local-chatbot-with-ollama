
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["src/LocalChatbot.Api/LocalChatbot.Api.csproj", "src/LocalChatbot.Api/"]

RUN dotnet restore "./src/LocalChatbot.Api/LocalChatbot.Api.csproj"

COPY . .

WORKDIR "/src/src/LocalChatbot.Api"

RUN dotnet build "./LocalChatbot.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build

FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "./LocalChatbot.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "LocalChatbot.Api.dll"]
