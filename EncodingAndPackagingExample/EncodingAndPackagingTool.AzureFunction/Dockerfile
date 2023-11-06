FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4.0-dotnet-isolated6.0 As base
RUN apt update && \
    apt install -y ffmpeg

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet build -c Release EncodingAndPackagingTool.AzureFunction/EncodingAndPackagingTool.AzureFunction.csproj -o /app/build

FROM build AS publish
RUN dotnet publish -c Release EncodingAndPackagingTool.AzureFunction/EncodingAndPackagingTool.AzureFunction.csproj -o /app/publish

FROM base AS final
COPY --from=publish /app/publish /app
ENV AzureWebJobsScriptRoot=/app \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
