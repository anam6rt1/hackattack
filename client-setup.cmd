@echo off
rem Pornește client-setup.ps1 pe Windows (fără să schimbe politica de scripturi PowerShell). Utilizare: client-setup.cmd [server] / client-setup.cmd close
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0client-setup.ps1" %*
