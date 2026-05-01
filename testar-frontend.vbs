' Testa npm run dev no frontend e captura o erro
Dim sh, fso
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

Dim frontendPath
frontendPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\frontend"

Dim tmp
tmp = sh.ExpandEnvironmentStrings("%TEMP%")
Dim outFile
outFile = tmp & "\frontend_test.txt"

' Roda npm run dev por 30 segundos
Dim cmdLine
cmdLine = "cmd /c cd /d """ & frontendPath & """ && npm run dev > """ & outFile & """ 2>&1"

Dim proc
Set proc = sh.Exec(cmdLine)

' Aguarda ate 30 segundos ou ate ter conteudo
Dim elapsed
elapsed = 0

Do While elapsed < 30000
    WScript.Sleep 1000
    elapsed = elapsed + 1000

    If fso.FileExists(outFile) Then
        If fso.GetFile(outFile).Size > 50 Then
            Dim f
            Set f = fso.OpenTextFile(outFile, 1)
            Dim conteudo
            If Not f.AtEndOfStream Then
                conteudo = f.ReadAll()
            End If
            f.Close
            ' Para se encontrou erro ou pronto
            If InStr(conteudo, "error") > 0 Or _
               InStr(conteudo, "Error") > 0 Or _
               InStr(conteudo, "Local") > 0 Or _
               InStr(conteudo, "ready") > 0 Or _
               InStr(conteudo, "warn") > 0 Then
                Exit Do
            End If
        End If
    End If
Loop

' Mata o processo
On Error Resume Next
proc.Terminate
On Error GoTo 0

' Le resultado
Dim output
output = ""
If fso.FileExists(outFile) Then
    If fso.GetFile(outFile).Size > 0 Then
        Dim f2
        Set f2 = fso.OpenTextFile(outFile, 1)
        If Not f2.AtEndOfStream Then
            output = f2.ReadAll()
        End If
        f2.Close
    End If
End If

' Salva resultado
Dim resultFile
resultFile = fso.GetParentFolderName(WScript.ScriptFullName) & "\frontend-erro.txt"
Dim f3
Set f3 = fso.CreateTextFile(resultFile, True)
f3.Write output
f3.Close

If output = "" Then
    output = "Nenhuma saida capturada. npm pode nao estar no PATH."
End If

' Exibe (truncado)
Dim exibir
If Len(output) > 2000 Then
    exibir = Left(output, 800) & Chr(10) & "..." & Chr(10) & Right(output, 1000)
Else
    exibir = output
End If

MsgBox exibir, vbInformation, "Diagnostico Frontend Next.js"
