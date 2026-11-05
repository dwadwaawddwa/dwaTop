@echo off
setlocal
cd /d "%~dp0\.."
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "CSC=%FW%\csc.exe"
set "OUT=%TEMP%\dwaTop-tests"
if not exist "%OUT%" mkdir "%OUT%"
set REFS=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:"%FW%\WPF\UIAutomationClient.dll" /r:"%FW%\WPF\UIAutomationTypes.dll" /r:"%FW%\WPF\WindowsBase.dll"
"%CSC%" /nologo /target:winexe /platform:x64 /codepage:65001 /out:"%OUT%\Target.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll tests\Target.cs || exit /b 1
"%CSC%" /nologo /target:exe /platform:x64 /codepage:65001 /utf8output /main:DwaTop.Tests /win32manifest:src\app.manifest /out:"%OUT%\Tests.exe" %REFS% src\*.cs tests\Tests.cs || exit /b 1
rem donnees isolees : ne touche pas a la vraie config
set "DWATOP_DATA=%OUT%\data"
"%OUT%\Tests.exe" "%OUT%\Target.exe"
exit /b %ERRORLEVEL%
