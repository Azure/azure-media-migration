FROM mcr.microsoft.com/dotnet/aspnet:7.0-jammy
WORKDIR /app
COPY bin/Release/net7.0/publish /app

env PlaybackService__AzureKeyVaultAccountName=
env PlaybackService__EnableDebugUI=

ENTRYPOINT ["dotnet", "PlaybackService.dll"]