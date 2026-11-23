@echo off
rem Runs the AdbAux unit tests (tests\AdbAux.Tests).
rem   cd /d "%~dp0"   : work in this file's folder, whatever folder it was started from
rem   dotnet test     : builds AdbAux and the test project, then runs every test;
rem                     -c Release = same build type as build.cmd
rem                     (the first run downloads the xUnit test packages from nuget.org)
rem   -- RunConfiguration.TargetPlatform=x64 : run the tests as a 64-bit process, like AdbAux.exe
cd /d "%~dp0"
dotnet test tests\AdbAux.Tests\AdbAux.Tests.csproj -c Release -nologo -- RunConfiguration.TargetPlatform=x64
if errorlevel 1 (
  echo.
  echo Some tests failed, or the build failed. See the messages above.
  pause
  exit /b 1
)
echo.
echo All tests passed.
pause
exit /b 0
