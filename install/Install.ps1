<#
.SYNOPSIS
  Instaluje LedgerCore dla bieżącego użytkownika (bez uprawnień administratora).
  Program:  %LOCALAPPDATA%\LedgerCore\app
  Dane:     %LOCALAPPDATA%\LedgerCore\data   (baza, klucz, pliki – tego folderu NIE usuwamy przy aktualizacji)
#>
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$base = Join-Path $env:LOCALAPPDATA 'LedgerCore'
$app = Join-Path $base 'app'
$data = Join-Path $base 'data'

if (-not (Test-Path (Join-Path $source 'LedgerCore.Api.exe'))) {
    throw 'Uruchom ten skrypt z rozpakowanego folderu paczki (obok LedgerCore.Api.exe).'
}

# Zatrzymaj działającą wersję (aktualizacja)
Get-Process -Name 'LedgerCore.Api' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$app*" } | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host "Instalowanie w $app ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force $app, $data | Out-Null
Get-ChildItem $app -Force | Remove-Item -Recurse -Force
Copy-Item (Join-Path $source '*') $app -Recurse -Force -Exclude 'Install.ps1', 'Install.cmd'

# Skróty: pulpit i menu Start
$shell = New-Object -ComObject WScript.Shell
$target = Join-Path $app 'Start-LedgerCore.cmd'
foreach ($dir in @([Environment]::GetFolderPath('Desktop'), (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'))) {
    $lnk = $shell.CreateShortcut((Join-Path $dir 'LedgerCore.lnk'))
    $lnk.TargetPath = $target
    $lnk.WorkingDirectory = $app
    $lnk.IconLocation = (Join-Path $app 'LedgerCore.Api.exe') + ',0'
    $lnk.Description = 'LedgerCore - ksiegowosc'
    $lnk.Save()
}

Write-Host ''
Write-Host 'LedgerCore zostal zainstalowany.' -ForegroundColor Green
Write-Host "Dane (baza) znajduja sie w: $data"
Write-Host 'Uruchom aplikacje skrotem "LedgerCore" z pulpitu. Przy pierwszym uruchomieniu utworzysz konto administratora.'
