$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot\..
if (Test-Path '.\plecho.db') {
    Remove-Item '.\plecho.db' -Force
    Write-Host 'Файл SQLite удалён: plecho.db'
} else {
    Write-Host 'Файл plecho.db не найден, удалять нечего.'
}
