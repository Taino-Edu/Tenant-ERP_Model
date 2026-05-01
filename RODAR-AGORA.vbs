Dim shell
Set shell = CreateObject("WScript.Shell")
shell.Run "cmd /c """ & CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName) & "\INICIAR-TUDO.bat""", 1, False
Set shell = Nothing
