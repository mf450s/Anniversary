FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DiaryApi.csproj", "./"]
RUN dotnet restore "DiaryApi.csproj"

COPY . .
RUN dotnet build "DiaryApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DiaryApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .

# Create uploads directory
RUN mkdir -p /app/uploads

EXPOSE 5000

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "DiaryApi.dll"]
