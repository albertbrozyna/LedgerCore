@echo off
chcp 65001 >nul
title LedgerCore - serwer (nie zamykaj tego okna podczas pracy)
setlocal

rem Dane (baza, klucz, pliki) leza poza folderem programu - aktualizacja ich nie kasuje.
if not defined LEDGERCORE_DATA_DIR set "LEDGERCORE_DATA_DIR=%LOCALAPPDATA%\LedgerCore\data"
rem Domyslnie tylko ten komputer. Dostep z sieci biura: ustaw ASPNETCORE_URLS=http://0.0.0.0:5000 (patrz INSTRUKCJA.txt)
if not defined ASPNETCORE_URLS set "ASPNETCORE_URLS=http://localhost:5000"
set "ASPNETCORE_ENVIRONMENT=Production"

rem Jesli aplikacja juz dziala - tylko otworz przegladarke
powershell -NoProfile -Command "try { (Invoke-WebRequest -UseBasicParsing http://localhost:5000/api/v1/setup/status -TimeoutSec 2) | Out-Null; exit 0 } catch { exit 1 }"
if %errorlevel%==0 (
  start "" http://localhost:5000
  exit /b 0
)

start "" /b powershell -NoProfile -Command "Start-Sleep -Seconds 3; Start-Process http://localhost:5000"
"%~dp0LedgerCore.Api.exe"
pause
