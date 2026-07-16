namespace CenarioMaritimo
{
    /// <summary>
    /// Contrato comum de georreferenciamento: converte uma posição local do Unity
    /// (X = Leste, Z = Norte, em metros) para latitude/longitude (graus).
    /// Implementado tanto pelo cenário fictício (plano tangente) quanto pelo
    /// cenário real (UTM), para que a embarcação exiba lat/lon nos dois casos.
    /// </summary>
    public interface IGeoReference
    {
        (double lat, double lon) LocalParaGeografica(float x, float z);
    }
}
