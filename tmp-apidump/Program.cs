using System;
using System.Linq;
using System.Reflection;

static void Dump(Type t)
{
    if (t == null) { Console.WriteLine("  (tipo nao encontrado)"); return; }
    Console.WriteLine("=== " + t.FullName + " ===");
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        if (m.IsSpecialName) continue;
        var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({pars})");
    }
    foreach (var p in t.GetProperties())
        Console.WriteLine($"  prop: {p.PropertyType.Name} {p.Name}");
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        Console.WriteLine($"  field: {f.FieldType.Name} {f.Name}");
}

var asmDfeUtils = typeof(DFe.Utils.ConfiguracaoCsc).Assembly;
var asmClasses = typeof(NFe.Classes.NFe).Assembly;
var asmUtils = typeof(NFe.Utils.InformacoesSuplementares.ExtinfNFeSupl).Assembly;

Dump(asmDfeUtils.GetType("DFe.Utils.FuncoesXml"));
Dump(asmUtils.GetType("NFe.Utils.NFe.ChaveFiscal"));
Dump(asmClasses.GetTypes().FirstOrDefault(t => t.Name == "enviNFe"));
Dump(asmClasses.GetTypes().FirstOrDefault(t => t.Name == "retEnviNFe"));
Dump(asmUtils.GetType("NFe.Utils.NFe.ExtNFe"));

