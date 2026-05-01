' Reinstala dependencias do frontend usando o caminho completo do npm
Dim sh, fso
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

Dim frontendPath
frontendPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\frontend"

Dim tmp
tmp = sh.ExpandEnvironmentStrings("%TEMP%")
Dim outFile
outFile = tmp & "\npm_install.txt"

' Descobre onde o Node.js/npm esta instalado
Dim npmPath
npmPath = ""

' Locais comuns do npm no Windows
Dim locais(4)
locais(0) = "C:\Program Files\nodejs\npm.cmd"
locais(1) = "C:\Program Files (x86)\nodejs\npm.cmd"
locais(2) = sh.ExpandEnvironmentStrings("%APPDATA%\npm\npm.cmd")
locais(3) = "C:\Users\" & sh.ExpandEnvironmentStrings("%USERNAME%") & "\AppData\Roaming\npm\npm.cmd"
locais(4) = sh.ExpandEnvironmentStrings("%ProgramFiles%\nodejs\npm.cmd")

Dim i
For i = 0 To 4
    If fso.FileExists(locais(i)) Then
        npmPath = locais(i)
        Exit For
    End If
Next

' Tenta via where se nao encontrou
If npmPath = "" Then
    Dim whereFile
    whereFile = tmp & "\where_npm.txt"
    sh.Run "cmd /c where npm > """ & whereFile & """ 2>&1", 0, True
    If fso.FileExists(whereFile) Then
        If fso.GetFile(whereFile).Size > 0 Then
            Dim fw
            Set fw = fso.OpenTextFile(whereFile, 1)
            If Not fw.AtEndOfStream Then
                Dim linha
                linha = fw.ReadLine()
                If InStr(linha, "npm") > 0 Then
                    npmPath = Trim(linha)
                End If
            End If
            fw.Close
        End If
    End If
End If

If npmPath = "" Then
    MsgBox "npm NAO encontrado! Verifique se Node.js esta instalado.", vbCritical, "Erro"
    WScript.Quit
End If

MsgBox "npm encontrado em: " & Chr(10) & npmPath & Chr(10) & Chr(10) & "Iniciando npm install no frontend..." & Chr(10) & "(Isso pode demorar 1-2 minutos)", vbInformation, "npm install"

' Roda npm install
Dim cmdLine
cmdLine = "cmd /c cd /d """ & frontendPath & """ && """ & npmPath & """ install > """ & outFile & """ 2>&1"

sh.Run cmdLine, 0, True

' Le resultado
Dim output
output = ""
If fso.FileExists(outFile) Then
    If fso.GetFile(outFile).Size > 0 Then
        Dim f
        Set f = fso.OpenTextFile(outFile, 1)
        If Not f.AtEndOfStream Then
            output = f.ReadAll()
        End If
        f.Close
    End If
End If

' Salva resultado
Dim resultFile
resultFile = fso.GetParentFolderName(WScript.ScriptFullName) & "\npm-install-resultado.txt"
Dim f2
Set f2 = fso.CreateTextFile(resultFile, True)
f2.Write output
f2.Close

If Len(output) > 2000 Then
    output = Left(output, 800) & Chr(10) & "..." & Chr(10) & Right(output, 1000)
End If

If output = "" Then
    output = "Nenhuma saida. Verifique npm-install-resultado.txt"
End If

MsgBox output, vbInformation, "Resultado npm install"
