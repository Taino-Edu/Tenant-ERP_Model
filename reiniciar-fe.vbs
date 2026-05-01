Dim sh
Set sh = CreateObject("WScript.Shell")

' Mata node.exe
sh.Run "taskkill /f /im node.exe", 0, True

' Aguarda 2 segundos
WScript.Sleep 2000

' Inicia o frontend
sh.Run "cmd /k """ & fso_GetFolder() & "\start-fe.bat""", 1, False

Function fso_GetFolder()
    Dim fso
    Set fso = CreateObject("Scripting.FileSystemObject")
    fso_GetFolder = fso.GetParentFolderName(WScript.ScriptFullName)
End Function
