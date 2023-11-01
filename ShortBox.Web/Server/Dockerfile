#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["ShortBox.Web/Server/ShortBox.Web.Server.csproj", "ShortBox.Web/Server/"]
COPY ["ShortBox.Web/Client/ShortBox.Web.Client.csproj", "ShortBox.Web/Client/"]
COPY ["ShortBox.Web/Shared/ShortBox.Web.Shared.csproj", "ShortBox.Web/Shared/"]
RUN dotnet restore "ShortBox.Web/Server/ShortBox.Web.Server.csproj"
COPY . .
WORKDIR "/src/ShortBox.Web/Server"
RUN dotnet build "ShortBox.Web.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ShortBox.Web.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /publish
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ShortBox.Web.Server.dll"]