using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Cliente> Clientes { get; set; } = null!;
        public DbSet<Servicio> Servicios { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tablas principales
            modelBuilder.Entity<Cliente>(e =>
            {
                e.ToTable("clientes");
                e.HasKey(x => x.Id);
                e.Property(x => x.NombreCliente).HasMaxLength(255).IsRequired();
                e.Property(x => x.Correo).HasMaxLength(255).IsRequired();
            });

            modelBuilder.Entity<Servicio>(e =>
            {
                e.ToTable("servicios");
                e.HasKey(x => x.Id);
                e.Property(x => x.NombreServicio).HasMaxLength(255).IsRequired();
                e.Property(x => x.Descripcion).HasMaxLength(255);
            });

            // Relación M:N explícita con tabla intermedia "cliente_servicios"
            modelBuilder.Entity<Cliente>()
                .HasMany(c => c.Servicios)
                .WithMany(s => s.Clientes)
                .UsingEntity<Dictionary<string, object>>(
                    "cliente_servicios",
                    right => right
                        .HasOne<Servicio>()
                        .WithMany()
                        .HasForeignKey("servicio_id")
                        .HasConstraintName("fk_cliente_servicios_servicio")
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left
                        .HasOne<Cliente>()
                        .WithMany()
                        .HasForeignKey("cliente_id")
                        .HasConstraintName("fk_cliente_servicios_cliente")
                        .OnDelete(DeleteBehavior.Cascade),
                    join =>
                    {
                        join.ToTable("cliente_servicios");
                        join.HasKey("cliente_id", "servicio_id");
                    }
                );
        }
    }
}
