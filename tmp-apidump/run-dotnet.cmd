@echo off
rem Helper: ambiente Windows completo p/ NuGet (o harness do Git Bash nao herda PROGRAMFILES etc.)
set "PROGRAMFILES=C:\Program Files"
set "PROGRAMFILES(X86)=C:\Program Files (x86)"
set "PROGRAMDATA=C:\ProgramData"
set "ALLUSERSPROFILE=C:\ProgramData"
set "SYSTEMROOT=C:\Windows"
cd /d C:\Users\TI06\Documents\GitHub\Tenant-ERP_Model
"C:\Program Files\dotnet\dotnet.exe" %*
