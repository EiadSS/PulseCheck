FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/PulseCheck.Api/PulseCheck.Api.csproj backend/PulseCheck.Api/
RUN dotnet restore backend/PulseCheck.Api/PulseCheck.Api.csproj

COPY backend/PulseCheck.Api backend/PulseCheck.Api
RUN dotnet publish backend/PulseCheck.Api/PulseCheck.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PulseCheck.Api.dll"]
