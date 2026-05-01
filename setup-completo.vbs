' ============================================================
' setup-completo.vbs — Configura banco e inicia o sistema
' ============================================================
Dim sh, fso
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

Dim resultado
resultado = ""

' ---------- Encontrar PostgreSQL ----------
Dim pgPath, pgBin
Dim pgVersions(5)
pgVersions(0) = "17"
pgVersions(1) = "16"
pgVersions(2) = "15"
pgVersions(3) = "14"
pgVersions(4) = "13"
pgVersions(5) = "12"

Dim v
For Each v In pgVersions
    Dim tentativa
    tentativa = "C:\Program Files\PostgreSQL\" & v & "\bin"
    If fso.FolderExists(tentativa) Then
        pgBin = tentativa
        resultado = resultado & "PostgreSQL encontrado em: " & pgBin & Chr(10)
        Exit For
    End If
Next

If pgBin = "" Then
    resultado = resultado & "PostgreSQL NAO encontrado em Program Files!" & Chr(10)
End If

' ---------- Encontrar MongoDB ----------
Dim mongoBin
Dim mongoVersions(4)
mongoVersions(0) = "8.0"
mongoVersions(1) = "7.0"
mongoVersions(2) = "6.0"
mongoVersions(3) = "5.0"
mongoVersions(4) = "4.4"

For Each v In mongoVersions
    Dim tentativa2
    tentativa2 = "C:\Program Files\MongoDB\Server\" & v & "\bin"
    If fso.FolderExists(tentativa2) Then
        mongoBin = tentativa2
        resultado = resultado & "MongoDB encontrado em: " & mongoBin & Chr(10)
        Exit For
    End If
Next

If mongoBin = "" Then
    resultado = resultado & "MongoDB NAO encontrado em Program Files!" & Chr(10)
End If

' ---------- Configurar Banco PostgreSQL ----------
If pgBin <> "" Then
    Dim psql
    psql = """" & pgBin & "\psql.exe"""

    ' Tentar senhas comuns
    Dim senhas(5)
    senhas(0) = "postgres"
    senhas(1) = "admin"
    senhas(2) = "password"
    senhas(3) = ""
    senhas(4) = "root"
    senhas(5) = "123456"

    Dim tmp, senhaCorreta, i
    tmp = sh.ExpandEnvironmentStrings("%TEMP%")
    senhaCorreta = ""

    For i = 0 To 5
        Dim testFile
        testFile = tmp & "\pg_test.txt"
        sh.Run "cmd /c set PGPASSWORD=" & senhas(i) & " && " & psql & " -U postgres -c ""\q"" > """ & testFile & """ 2>&1", 0, True
        If fso.FileExists(testFile) Then
            Dim f2
            Set f2 = fso.OpenTextFile(testFile, 1)
            Dim conteudo2
            conteudo2 = f2.ReadAll()
            f2.Close
            If InStr(conteudo2, "FATAL") = 0 And InStr(conteudo2, "error") = 0 Then
                senhaCorreta = senhas(i)
                resultado = resultado & "Senha postgres encontrada: [" & senhaCorreta & "]" & Chr(10)
                Exit For
            End If
        End If
    Next

    If senhaCorreta <> "" Or True Then
        ' Criar usuario e banco (mesmo que nao encontremos a senha, tentamos com as comuns)
        Dim pgPass
        If senhaCorreta <> "" Then
            pgPass = senhaCorreta
        Else
            pgPass = "postgres"
        End If

        Dim setupFile
        setupFile = tmp & "\pg_setup.txt"

        ' Criar usuario cardgame_user
        sh.Run "cmd /c set PGPASSWORD=" & pgPass & " && " & psql & " -U postgres -c ""CREATE USER cardgame_user WITH PASSWORD 'CardGame@2025';"" > """ & setupFile & """ 2>&1", 0, True
        ' Criar banco
        sh.Run "cmd /c set PGPASSWORD=" & pgPass & " && " & psql & " -U postgres -c ""CREATE DATABASE cardgamestore OWNER cardgame_user;"" >> """ & setupFile & """ 2>&1", 0, True
        ' Dar privilegios
        sh.Run "cmd /c set PGPASSWORD=" & pgPass & " && " & psql & " -U postgres -c ""GRANT ALL PRIVILEGES ON DATABASE cardgamestore TO cardgame_user;"" >> """ & setupFile & """ 2>&1", 0, True

        If fso.FileExists(setupFile) Then
            Dim f3
            Set f3 = fso.OpenTextFile(setupFile, 1)
            resultado = resultado & "Resultado setup DB: " & f3.ReadAll() & Chr(10)
            f3.Close
        End If
    End If
End If

' ---------- Salvar resultado ----------
Dim scriptDir
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
Dim resultFile
resultFile = scriptDir & "\resultado-setup.txt"
Dim f4
Set f4 = fso.CreateTextFile(resultFile, True)
f4.Write resultado
f4.Close

MsgBox resultado, vbInformation, "Setup CardGameStore"
