@echo off
rem Compila S136Battery.exe con el csc de .NET Framework (viene con Windows).
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
cd /d "%~dp0"
if not exist obj mkdir obj
"%CSC%" -nologo -out:obj\mkicon.exe -r:System.Drawing.dll tools\mkicon.cs || exit /b 1
obj\mkicon.exe obj\app.ico || exit /b 1
"%CSC%" -nologo -target:winexe -platform:anycpu -optimize+ -win32icon:obj\app.ico -win32manifest:src\app.manifest ^
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:S136Battery.exe src\*.cs || exit /b 1
echo OK: S136Battery.exe
