@echo off
rem Builds AdbAux.exe and copies it next to the "scrcpy" folder (one level up).
rem   cd /d "%~dp0"      : work in this file's folder, whatever folder it was started from
rem   dotnet build       : compiles AdbAux.csproj; -c Release = optimised build
rem                        (the first build downloads .NET Framework reference files from nuget.org)
rem   copy /y            : copies the finished exe, overwriting an older copy without asking
cd /d "%~dp0"
dotnet build AdbAux.csproj -c Release -nologo
if errorlevel 1 goto fail
copy /y "bin\Release\net48\AdbAux.exe" "..\AdbAux.exe" >nul
if errorlevel 1 goto fail
echo.
echo Built: %~dp0..\AdbAux.exe
pause
exit /b 0
:fail
echo.
echo Build failed. See the messages above (close AdbAux.exe first if it is running).
pause
exit /b 1
