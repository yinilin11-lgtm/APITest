FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ToyotaVehicleAdvisor/ToyotaVehicleAdvisor.csproj ToyotaVehicleAdvisor/
RUN dotnet restore ToyotaVehicleAdvisor/ToyotaVehicleAdvisor.csproj

COPY . .
RUN dotnet publish ToyotaVehicleAdvisor/ToyotaVehicleAdvisor.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ToyotaVehicleAdvisor.dll"]
