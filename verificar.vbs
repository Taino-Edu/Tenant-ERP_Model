Set sh = CreateObject("WScript.Shell")

' Verificar .NET SDK
Dim dotnet
On Error Resume Next
Dim oExec
Set oExec = sh.Exec("dotnet --version")
WScript.Sleep 3000
dotnet = oExec.StdOut.ReadAll()
If dotnet = "" Then dotnet = "(nao encontrado)"
On Error GoTo 0

' Verificar psql (PostgreSQL)
Dim psql
On Error Resume Next
Set oExec = sh.Exec("psql --version")
WScript.Sleep 2000
psql = oExec.StdOut.ReadAll()
If psql = "" Then psql = "(nao encontrado)"
On Error GoTo 0

' Verificar mongod (MongoDB)
Dim mongod
On Error Resume Next
Set oExec = sh.Exec("mongod --version")
WScript.Sleep 2000
mongod = oExec.StdOut.ReadAll()
If mongod = "" Then mongod = "(nao encontrado)"
On Error GoTo 0

MsgBox "=== STATUS DAS INSTALACOES ===" & Chr(10) & Chr(10) & _
       ".NET SDK: " & dotnet & Chr(10) & _
       "PostgreSQL: " & psql & Chr(10) & _
       "MongoDB: " & mongod, vbInformation, "CardGameStore - Verificacao"
