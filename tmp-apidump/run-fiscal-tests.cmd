@echo off
set "PROGRAMFILES=C:\Program Files"
set "PROGRAMFILES(X86)=C:\Program Files (x86)"
set "PROGRAMDATA=C:\ProgramData"
set "ALLUSERSPROFILE=C:\ProgramData"
set "SYSTEMROOT=C:\Windows"
set "TEST_POSTGRES_CONNECTION=Host=127.0.0.1;Port=5433;Database=tenant_erp_test;Username=tenant_test;Password=tenant_test_pw"
cd /d C:\Users\TI06\Documents\GitHub\Tenant-ERP_Model
"C:\Program Files\dotnet\dotnet.exe" test softNerd.sln --filter "FullyQualifiedName~NfceEmissionServiceTests|FullyQualifiedName~FiscalCertificadoServiceTests|FullyQualifiedName~FiscalXmlExportServiceTests"
