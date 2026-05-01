' Testa se a API esta respondendo no localhost:5000
Dim http, fso, f, resultado
Set http = CreateObject("MSXML2.ServerXMLHTTP.6.0")
Set fso = CreateObject("Scripting.FileSystemObject")

resultado = ""
Dim tentativas, ok
tentativas = 0
ok = False

Do While tentativas < 20 And Not ok
    tentativas = tentativas + 1
    On Error Resume Next
    http.Open "GET", "http://localhost:5000/health", False
    http.setTimeouts 2000, 2000, 5000, 5000
    http.Send
    If Err.Number = 0 And http.Status = 200 Then
        resultado = "API OK! Status: " & http.Status & Chr(10) & "Resposta: " & http.ResponseText
        ok = True
    Else
        resultado = "Tentativa " & tentativas & "/20 - API nao respondeu ainda (aguardando...)"
        WScript.Sleep 5000
    End If
    On Error GoTo 0
Loop

If Not ok Then
    resultado = "API nao subiu apos " & tentativas & " tentativas. Verifique a janela 'CardGameStore API' para erros."
End If

' Salvar resultado
Dim scriptDir
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
Set f = fso.CreateTextFile(scriptDir & "\api-status.txt", True)
f.Write resultado
f.Close

MsgBox resultado, vbInformation, "Status da API CardGameStore"
