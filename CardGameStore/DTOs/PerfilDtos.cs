using System.ComponentModel.DataAnnotations;

namespace CardGameStore.DTOs;

public class PerfilDto
{
    public Guid     Id           { get; set; }
    public string   Nome         { get; set; } = string.Empty;
    public string[] Permissoes   { get; set; } = [];
    public DateTime CriadoEm     { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public int      TotalUsuarios { get; set; }
}

public class CriarPerfilRequest
{
    [Required, MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    public string[] Permissoes { get; set; } = [];
}

public class AtualizarPerfilRequest
{
    [MaxLength(100)]
    public string? Nome { get; set; }

    public string[]? Permissoes { get; set; }
}
