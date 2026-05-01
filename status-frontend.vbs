' Verifica se next.js e a API estao rodando, e testa a porta 3000
Dim sh, fso
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

' Verifica processos em execucao
Dim tmp
tmp = sh.ExpandEnvironmentStrings("%TEMP%")

Dim procFile
procFile = tmp & "\processos.txt"
sh.Run "cmd /c tasklist | findstr /i ""node dotnet"" > """ & procFile & """ 2>&1", 0, True

Dim processos
processos = ""
If fso.FileExists(procFile) Then
    Dim f
    Set f = fso.OpenTextFile(procFile, 1)
    processos = f.ReadAll()
    f.Close
End If

' Verifica porta 3000
Dim portFile
portFile = tmp & "\porta3000.txt"
sh.Run "cmd /c netstat -an | findstr "":3000"" > """ & portFile & """ 2>&1", 0, True

Dim porta3000
porta3000 = ""
If fso.FileExists(portFile) Then
    Dim f2
    Set f2 = fso.OpenTextFile(portFile, 1)
    porta3000 = f2.ReadAll()
    f2.Close
End If

' Verifica porta 5000 (API)
Dim portFile2
portFile2 = tmp & "\porta5000.txt"
sh.Run "cmd /c netstat -an | findstr "":5000"" > """ & portFile2 & """ 2>&1", 0, True

Dim porta5000
porta5000 = ""
If fso.FileExists(portFile2) Then
    Dim f3
    Set f3 = fso.OpenTextFile(portFile2, 1)
    porta5000 = f3.ReadAll()
    f3.Close
End If

' Monta resultado
Dim resultado
resultado = "=== PROCESSOS (node / dotnet) ===" & Chr(10)
If Trim(processos) = "" Then
    resultado = resultado & "(nenhum encontrado)" & Chr(10)
Else
    resultado = resultado & processos & Chr(10)
End If

resultado = resultado & Chr(10) & "=== PORTA 3000 (Frontend) ===" & Chr(10)
If Trim(porta3000) = "" Then
    resultado = resultado & "(porta 3000 NAO esta em uso - frontend NAO rodando)" & Chr(10)
Else
    resultado = resultado & porta3000 & Chr(10)
End If

resultado = resultado & Chr(10) & "=== PORTA 5000 (API) ===" & Chr(10)
If Trim(porta5000) = "" Then
    resultado = resultado & "(porta 5000 NAO esta em uso - API NAO rodando)" & Chr(10)
Else
    resultado = resultado & porta5000 & Chr(10)
End If

' Salva e exibe
Dim resultFile
resultFile = fso.GetParentFolderName(WScript.ScriptFullName) & "\status-servicos.txt"
Dim f4
Set f4 = fso.CreateTextFile(resultFile, True)
f4.Write resultado
f4.Close

MsgBox resultado, vbInformation, "Status dos Servicos CardGameStore"
