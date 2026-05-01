' Testa a API com launchSettings.json (perfil http) por 45 segundos
Dim sh, fso
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

Dim projectPath
projectPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\CardGameStore"

Dim tmp
tmp = sh.ExpandEnvironmentStrings("%TEMP%")
Dim outFile
outFile = tmp & "\api_test.txt"

' Usa dotnet run com --launch-profile http (garante ASPNETCORE_ENVIRONMENT=Development)
Dim cmdLine
cmdLine = "cmd /c cd /d """ & projectPath & """ && dotnet run --launch-profile http --urls http://localhost:5001 > """ & outFile & """ 2>&1"

Dim proc
Set proc = sh.Exec(cmdLine)

' Aguarda ate 45 segundos
Dim elapsed
elapsed = 0

Do While elapsed < 45000
    WScript.Sleep 1000
    elapsed = elapsed + 1000

    ' Verifica se o arquivo de saida tem conteudo relevante
    If fso.FileExists(outFile) Then
        If fso.GetFile(outFile).Size > 50 Then
            Dim f
            Set f = fso.OpenTextFile(outFile, 1)
            Dim conteudo
            conteudo = f.ReadAll()
            f.Close
            ' Para se achou resultado (sucesso ou erro)
            If InStr(conteudo, "Now listening on") > 0 Or _
               InStr(conteudo, "Application started") > 0 Or _
               InStr(conteudo, "Exception") > 0 Or _
               InStr(conteudo, "Unhandled") > 0 Or _
               InStr(conteudo, "ERRO") > 0 Or _
               InStr(conteudo, "SQLite") > 0 Then
                Exit Do
            End If
        End If
    End If
Loop

' Mata o processo
On Error Resume Next
proc.Terminate
On Error GoTo 0

' Le resultado final
Dim output
output = ""
If fso.FileExists(outFile) Then
    Dim f2
    Set f2 = fso.OpenTextFile(outFile, 1)
    output = f2.ReadAll()
    f2.Close
End If

' Salva resultado
Dim resultFile
resultFile = fso.GetParentFolderName(WScript.ScriptFullName) & "\api-teste-resultado.txt"
Dim f3
Set f3 = fso.CreateTextFile(resultFile, True)
f3.Write output
f3.Close

' Exibe (truncado)
Dim exibir
If Len(output) > 2500 Then
    exibir = Left(output, 1000) & Chr(10) & Chr(10) & "=== FIM ===" & Chr(10) & Right(output, 1200)
Else
    exibir = output
End If

If exibir = "" Then
    exibir = "Nenhuma saida capturada. Verifique se o projeto esta na pasta correta."
End If

MsgBox exibir, vbInformation, "Teste API CardGameStore"
