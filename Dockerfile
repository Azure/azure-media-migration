#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0-jammy AS base
WORKDIR /app
RUN apt-get update && apt-get install -y ffmpeg libsecret-1-dev

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["AMSMigrate.csproj", "."]
RUN dotnet restore "./AMSMigrate.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "AMSMigrate.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AMSMigrate.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN chmod +x /app/packager-linux-x64
ENTRYPOINT ["dotnet", "AMSMigrate.dll"]