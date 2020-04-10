# run dotnet publish -c release -o published first
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS runtime
WORKDIR /app
COPY published/ ./
ENTRYPOINT ["dotnet", "BrokerServer.dll"]