namespace Api.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;

        // Relación M:N
        public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
    }
}
