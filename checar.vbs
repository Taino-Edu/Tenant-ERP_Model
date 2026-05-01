Dim sh, fso, f, saida
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

saida = ""

' Checar dotnet
Dim tmp
tmp = sh.ExpandEnvironmentStrings("%TEMP%")
sh.Run "cmd /c dotnet --version > """ & tmp & "\dotnet_check.txt"" 2>&1", 0, True
If fso.FileExists(tmp & "\dotnet_check.txt") Then
    Set f = fso.OpenTextFile(tmp & "\dotnet_check.txt", 1)
    saida = saida & ".NET SDK: " & f.ReadAll() & Chr(10)
    f.Close
End If

' Checar psql
sh.Run "cmd /c psql --version > """ & tmp & "\psql_check.txt"" 2>&1", 0, True
If fso.FileExists(tmp & "\psql_check.txt") Then
    Set f = fso.OpenTextFile(tmp & "\psql_check.txt", 1)
    saida = saida & "PostgreSQL: " & f.ReadAll() & Chr(10)
    f.Close
End If

' Checar mongod
sh.Run "cmd /c mongod --version > """ & tmp & "\mongod_check.txt"" 2>&1", 0, True
If fso.FileExists(tmp & "\mongod_check.txt") Then
    Set f = fso.OpenTextFile(tmp & "\mongod_check.txt", 1)
    saida = saida & "MongoDB: " & f.ReadAll() & Chr(10)
    f.Close
End If

' Checar node
sh.Run "cmd /c node --version > """ & tmp & "\node_check.txt"" 2>&1", 0, True
If fso.FileExists(tmp & "\node_check.txt") Then
    Set f = fso.OpenTextFile(tmp & "\node_check.txt", 1)
    saida = saida & "Node.js: " & f.ReadAll() & Chr(10)
    f.Close
End If

' Salvar resultado
Dim resultPath
resultPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\resultado.txt"
Set f = fso.CreateTextFile(resultPath, True)
f.Write saida
f.Close

MsgBox saida, vbInformation, "Verificacao de Dependencias"
