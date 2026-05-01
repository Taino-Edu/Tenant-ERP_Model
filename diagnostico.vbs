Dim sh, fso
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

Dim tmp
tmp = sh.ExpandEnvironmentStrings("%TEMP%")
Dim outFile
outFile = tmp & "\dotnet_build.txt"

' Roda dotnet build e captura saida
Dim projectPath
projectPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\CardGameStore"

sh.Run "cmd /c cd /d """ & projectPath & """ && dotnet build 2>&1 > """ & outFile & """", 0, True

' Le resultado
Dim resultado
resultado = ""
If fso.FileExists(outFile) Then
    Dim f
    Set f = fso.OpenTextFile(outFile, 1)
    resultado = f.ReadAll()
    f.Close
End If

' Salva no disco (na pasta vendasMTG)
Dim resultFile
resultFile = fso.GetParentFolderName(WScript.ScriptFullName) & "\build-resultado.txt"
Dim f2
Set f2 = fso.CreateTextFile(resultFile, True)
f2.Write resultado
f2.Close

' Mostra apenas as ultimas 2000 chars (MsgBox tem limite)
Dim exibir
If Len(resultado) > 2000 Then
    exibir = "...(truncado)..." & Chr(10) & Right(resultado, 2000)
Else
    exibir = resultado
End If

MsgBox exibir, vbInformation, "Resultado dotnet build"
