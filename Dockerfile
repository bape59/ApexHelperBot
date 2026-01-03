# ---------- BUILD STAGE ----------
FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0 AS build
WORKDIR /app

# копируем файл проекта и восстанавливаем зависимости
COPY TelegramBot.csproj ./
RUN dotnet restore

# копируем остальной код и публикуем
COPY . .
RUN dotnet publish -c Release -o out

# ---------- RUNTIME STAGE ----------
FROM mcr.microsoft.com/dotnet/nightly/runtime:10.0
WORKDIR /app

COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "TelegramBot.dll"]
