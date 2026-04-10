$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot\..
Write-Host 'Запуск приложения с SQLite...'
dotnet run
