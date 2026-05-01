' Verifica portas em uso (3000 e 5000) e processos node/dotnet
Dim sh, fso
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

Dim tmp
tmp = sh.ExpandEnvironmentStrings("%TEMP%")

' Roda netstat e tasklist para arquivo unico
Dim outFile
outFile = tmp & "\status_check.txt"

sh.Run "cmd /c (netstat -ano | findstr "":3000 :5000"") > """ & outFile & """ 2>&1", 0, True

Dim netstatInfo
netstatInfo = ""
If fso.FileExists(outFile) Then
    Dim fSize
    fSize = fso.GetFile(outFile).Size
    If fSize > 0 Then
        Dim f1
        Set f1 = fso.OpenTextFile(outFile, 1)
        If Not f1.AtEndOfStream Then
            netstatInfo = f1.ReadAll()
        End If
        f1.Close
    End If
End If

' Processos
Dim outFile2
outFile2 = tmp & "\proc_check.txt"
sh.Run "cmd /c tasklist /fi ""imagename eq node.exe"" /fi ""imagename eq dotnet.exe"" > """ & outFile2 & """ 2>&1", 0, True

Dim procInfo
procInfo = ""
If fso.FileExists(outFile2) Then
    If fso.GetFile(outFile2).Size > 0 Then
        Dim f2
        Set f2 = fso.OpenTextFile(outFile2, 1)
        If Not f2.AtEndOfStream Then
            procInfo = f2.ReadAll()
        End If
        f2.Close
    End If
End If

' Monta resultado
Dim msg
msg = "=== PORTAS 3000 / 5000 ===" & Chr(10)
If Trim(netstatInfo) = "" Then
    msg = msg & "Nenhuma das portas esta em uso!" & Chr(10)
Else
    msg = msg & netstatInfo & Chr(10)
End If

msg = msg & Chr(10) & "=== PROCESSOS node.exe / dotnet.exe ===" & Chr(10)
If Trim(procInfo) = "" Then
    msg = msg & "Nenhum processo encontrado" & Chr(10)
Else
    msg = msg & procInfo
End If

' Salva
Dim resultFile
resultFile = fso.GetParentFolderName(WScript.ScriptFullName) & "\status-servicos.txt"
Dim f3
Set f3 = fso.CreateTextFile(resultFile, True)
f3.Write msg
f3.Close

MsgBox msg, vbInformation, "Status Portas CardGameStore"
