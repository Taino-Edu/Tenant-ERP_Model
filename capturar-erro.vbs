' Roda a API por 30s e captura a saida para diagnostico
Dim sh, fso
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

Dim projectPath
projectPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\CardGameStore"

Dim tmp
tmp = sh.ExpandEnvironmentStrings("%TEMP%")
Dim outFile
outFile = tmp & "\api_startup.txt"

' Roda dotnet run em porta diferente (5001) para nao conflitar com o que pode estar rodando
Dim cmdLine
cmdLine = "cmd /c cd /d """ & projectPath & """ && set ASPNETCORE_ENVIRONMENT=Development && dotnet run --urls http://localhost:5001 > """ & outFile & """ 2>&1"

Dim proc
Set proc = sh.Exec(cmdLine)

' Le saida por ate 30 segundos
Dim output, elapsed, chunk
output = ""
elapsed = 0

Do While elapsed < 30000
    WScript.Sleep 500
    elapsed = elapsed + 500

    ' Verifica se processo terminou (crasha)
    If proc.Status <> 0 Then
        ' Processo terminou - lemos o restante
        Do While Not proc.StdOut.AtEndOfStream
            output = output & proc.StdOut.ReadLine() & Chr(10)
        Loop
        Exit Do
    End If

    ' Verifica se iniciou com sucesso
    If fso.FileExists(outFile) Then
        If fso.GetFile(outFile).Size > 100 Then
            ' Tem conteudo - verifica se tem "Application started" ou erro
            Dim f2
            Set f2 = fso.OpenTextFile(outFile, 1)
            Dim conteudo
            conteudo = f2.ReadAll()
            f2.Close
            If InStr(conteudo, "Application started") > 0 Or _
               InStr(conteudo, "Now listening on") > 0 Or _
               InStr(conteudo, "Exception") > 0 Or _
               InStr(conteudo, "ERRO") > 0 Then
                output = conteudo
                Exit Do
            End If
        End If
    End If
Loop

' Mata o processo (se ainda rodando)
On Error Resume Next
proc.Terminate
On Error GoTo 0

' Le o arquivo de saida se output esta vazio
If output = "" And fso.FileExists(outFile) Then
    Dim f3
    Set f3 = fso.OpenTextFile(outFile, 1)
    output = f3.ReadAll()
    f3.Close
End If

' Salva na pasta vendasMTG
Dim resultFile
resultFile = fso.GetParentFolderName(WScript.ScriptFullName) & "\api-startup.txt"
Dim f4
Set f4 = fso.CreateTextFile(resultFile, True)
f4.Write output
f4.Close

' Exibe resultado (truncado)
Dim exibir
If Len(output) > 2500 Then
    exibir = Left(output, 800) & Chr(10) & Chr(10) & "=== FINAL ===" & Chr(10) & Right(output, 1500)
Else
    exibir = output
End If

MsgBox exibir, vbInformation, "API Startup Log"
