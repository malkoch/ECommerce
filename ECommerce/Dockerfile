ARG SERVICE
ARG SERVICE_PATH

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG SERVICE
ARG SERVICE_PATH
WORKDIR /src
COPY src/BuildingBlocks/Messaging/Messaging.csproj src/BuildingBlocks/Messaging/
COPY ${SERVICE_PATH}/${SERVICE}.csproj ${SERVICE_PATH}/
RUN dotnet restore ${SERVICE_PATH}/${SERVICE}.csproj
COPY src/ src/
RUN dotnet publish ${SERVICE_PATH}/${SERVICE}.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
ARG SERVICE
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    SERVICE_DLL=${SERVICE}.dll
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet $SERVICE_DLL"]
