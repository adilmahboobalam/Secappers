@echo off
set "DOTNET_ROOT=C:\Users\Adi\AppData\Local\Microsoft\dotnet"
set "DOTNET_ROOT(x64)=C:\Users\Adi\AppData\Local\Microsoft\dotnet"
set "DOTNET_ROOT_X64=C:\Users\Adi\AppData\Local\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"

if exist "%~dp0src\SecApper.FolderLocker\bin\Debug\net8.0-windows\SecApper.FolderLocker.exe" (
    start "" "%DOTNET_ROOT%\dotnet.exe" "%~dp0src\SecApper.FolderLocker\bin\Debug\net8.0-windows\SecApper.FolderLocker.dll"
) else (
    start "" "%DOTNET_ROOT%\dotnet.exe" "%~dp0publish\SecApper.FolderLocker.dll"
)
