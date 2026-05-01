Dim fso, f, sh
Set fso = CreateObject("Scripting.FileSystemObject")
Set sh = CreateObject("WScript.Shell")

Dim filePath
filePath = fso.GetParentFolderName(WScript.ScriptFullName) & "\frontend\node_modules\next\dist\cli\next-dev.js"

Dim resultado
resultado = "Testando: " & filePath & Chr(10) & Chr(10)

If fso.FileExists(filePath) Then
    resultado = resultado & "EXISTE no diretorio: SIM" & Chr(10)
    
    Dim fileObj
    Set fileObj = fso.GetFile(filePath)
    resultado = resultado & "Tamanho: " & fileObj.Size & " bytes" & Chr(10)
    resultado = resultado & "Atributos: " & fileObj.Attributes & Chr(10)
    
    ' Tenta abrir e ler o arquivo
    On Error Resume Next
    Dim ts
    Set ts = fso.OpenTextFile(filePath, 1)
    If Err.Number = 0 Then
        Dim conteudo
        If Not ts.AtEndOfStream Then
            conteudo = ts.Read(100)
        End If
        ts.Close
        resultado = resultado & "Legivel: SIM" & Chr(10)
        resultado = resultado & "Primeiros chars: " & Left(conteudo, 50) & Chr(10)
    Else
        resultado = resultado & "Legivel: NAO - Erro: " & Err.Description & Chr(10)
    End If
    On Error GoTo 0
Else
    resultado = resultado & "EXISTE no diretorio: NAO" & Chr(10)
End If

' Testar node_modules/next/dist/bin/next
Dim binPath
binPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\frontend\node_modules\.bin\next.cmd"
resultado = resultado & Chr(10) & "next.cmd existe: " & fso.FileExists(binPath)

MsgBox resultado, vbInformation, "Diagnostico next-dev.js"
