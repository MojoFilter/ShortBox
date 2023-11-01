#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["ShortBox.Api/ShortBox.Api.csproj", "ShortBox.Api/"]
RUN dotnet restore "ShortBox.Api/ShortBox.Api.csproj"
COPY . .
WORKDIR "/src/ShortBox.Api"
RUN dotnet build "ShortBox.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ShortBox.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ShortBox.Api.dll"]