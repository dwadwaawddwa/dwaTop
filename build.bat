@echo off
setlocal
cd /d "%~dp0"
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "CSC=%FW%\csc.exe"
if not exist "%CSC%" (
  echo csc.exe introuvable : .NET Framework 4.8 requis.
  exit /b 1
)
if not exist dist mkdir dist

rem dwaTop deja lance ? on le ferme proprement (il restaure les fenetres)
if exist dist\dwaTop.exe (
  dist\dwaTop.exe quit
  ping -n 2 127.0.0.1 >nul
)

set REFS=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:"%FW%\WPF\UIAutomationClient.dll" /r:"%FW%\WPF\UIAutomationTypes.dll" /r:"%FW%\WPF\WindowsBase.dll"
set OPTS=/nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /utf8output /win32manifest:src\app.manifest

if not exist src\dwaTop.ico (
  "%CSC%" %OPTS% /out:dist\dwaTop.exe %REFS% src\*.cs || goto :err
  dist\dwaTop.exe --make-icon src\dwaTop.ico
)
"%CSC%" %OPTS% /win32icon:src\dwaTop.ico /out:dist\dwaTop.exe %REFS% src\*.cs || goto :err
echo.
echo OK : dist\dwaTop.exe
exit /b 0

:err
echo.
echo ECHEC de la compilation
exit /b 1
