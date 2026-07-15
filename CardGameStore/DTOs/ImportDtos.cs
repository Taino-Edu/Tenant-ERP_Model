// =============================================================================
// ImportDtos.cs — Resultado de uma importação self-service (ver ImportController).
// =============================================================================

namespace CardGameStore.DTOs;

public class ImportRowErrorDto
{
    /// <summary>1-based e conta a linha de cabeçalho — bate com o que o admin vê
    /// se abrir o CSV num editor de planilha comum.</summary>
    public int Linha { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

public class ImportResultDto
{
    public int TotalLinhas { get; set; }
    public int Importados  { get; set; }
    public List<ImportRowErrorDto> Erros { get; set; } = new();
}
