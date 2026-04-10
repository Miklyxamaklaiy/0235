# Локальный запуск

## Требования

- .NET 8 SDK
- PowerShell

## Быстрый старт

```powershell
cd PlechPomoshchi
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts
eset-db.ps1
.\scripts
un.ps1
```

## Учётные записи

- Администратор — `admin@plecho.local` / `admin12345`
- Координатор — `coordinator@plecho.local` / `volunteer123`
- Пользователь — `user@plecho.local` / `requester123`


## Синхронизация каталога

Админ-панель запускает синхронизацию организаций через OpenStreetMap Overpass API. Лимит по умолчанию — 50 организаций за один запуск.
