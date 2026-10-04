@echo off
cd /d "%~dp0"
if exist "%~dp0publish\SecApper.FolderLocker.exe" (
    cd /d "%~dp0publish"
    start "" "SecApper.FolderLocker.exe"
    exit /b 0
)
if exist "%~dp0src\SecApper.FolderLocker\bin\Release\net8.0-windows\SecApper.FolderLocker.exe" (
    cd /d "%~dp0src\SecApper.FolderLocker\bin\Release\net8.0-windows"
    start "" "SecApper.FolderLocker.exe"
    exit /b 0
)
if exist "%~dp0src\SecApper.FolderLocker\bin\Debug\net8.0-windows\SecApper.FolderLocker.exe" (
    cd /d "%~dp0src\SecApper.FolderLocker\bin\Debug\net8.0-windows"
    start "" "SecApper.FolderLocker.exe"
    exit /b 0
)
echo SecApper executable not found. Please run dotnet build first.
pause
