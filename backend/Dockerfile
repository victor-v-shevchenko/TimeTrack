FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TimeTrack.Api/TimeTrack.Api.csproj TimeTrack.Api/
RUN dotnet restore TimeTrack.Api/TimeTrack.Api.csproj
COPY TimeTrack.Api/ TimeTrack.Api/
RUN dotnet publish TimeTrack.Api/TimeTrack.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TimeTrack.Api.dll"]
