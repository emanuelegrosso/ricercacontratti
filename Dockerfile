FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY RicercaContratti.csproj ./
RUN dotnet restore RicercaContratti.csproj

COPY . ./
RUN dotnet publish RicercaContratti.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir -p /data

ENV ASPNETCORE_URLS=http://+:8080
ENV CONTRACTS_DATA_DIR=/data
EXPOSE 8080

ENTRYPOINT ["dotnet", "RicercaContratti.dll"]
