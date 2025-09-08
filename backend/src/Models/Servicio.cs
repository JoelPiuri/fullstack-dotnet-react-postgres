namespace Api.Models
{
    public class Servicio
    {
        public int Id { get; set; }
        public string NombreServicio { get; set; } = string.Empty;
        public string? Descripcion { get; set; }

        // Relación M:N
        public ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
    }
}
