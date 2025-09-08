using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ---------- DB ----------
var connString = builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? throw new InvalidOperationException("No se encontró la cadena de conexión para la base de datos.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connString).UseSnakeCaseNamingConvention()
);

// (opcional) JSON
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// ---------- CORS ----------
builder.Services.AddCors();

var app = builder.Build();

// ---------- EnsureCreated ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ---------- Orígenes permitidos ----------
var allowedEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
var allowed = string.IsNullOrWhiteSpace(allowedEnv) ? "*" : allowedEnv;
var allowAnyOrigin = string.Equals(allowed.Trim(), "*", StringComparison.Ordinal);

string? PickAllowedOrigin(HttpContext ctx)
{
    var origin = ctx.Request.Headers["Origin"].ToString();
    if (string.IsNullOrEmpty(origin)) return null;
    if (allowAnyOrigin) return origin;
    var list = allowed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return Array.Exists(list, o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)) ? origin : null;
}

// ---------- Manejador de errores seguro ----------
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        // CORS en error
        var origin = PickAllowedOrigin(context);
        if (!string.IsNullOrEmpty(origin))
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var payload = new { error = ex?.Message };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    });
});

// ---------- Preflight muy temprano ----------
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Vary"] = "Origin";

    if (string.Equals(ctx.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
    {
        var origin = PickAllowedOrigin(ctx);
        if (!string.IsNullOrEmpty(origin))
            ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;

        var reqHeaders = ctx.Request.Headers["Access-Control-Request-Headers"].ToString();
        var reqMethod  = ctx.Request.Headers["Access-Control-Request-Method"].ToString();

        ctx.Response.Headers["Access-Control-Allow-Headers"] = string.IsNullOrEmpty(reqHeaders) ? "*" : reqHeaders;
        ctx.Response.Headers["Access-Control-Allow-Methods"] = string.IsNullOrEmpty(reqMethod)  ? "*" : reqMethod;

        ctx.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next();
});

// ---------- CORS global ----------
if (allowAnyOrigin)
    app.UseCors(p => p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod());
else
{
    var origins = allowed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    app.UseCors(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
}

// (¡no añadimos “sello CORS” después del next!)

// ---------- Swagger (Dev) ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---------- Health ----------
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));
app.MapGet("/dbcheck", async (AppDbContext db) =>
{
    await db.Database.ExecuteSqlRawAsync("select 1");
    return Results.Ok(new { db = "ok" });
});

// ======================= CLIENTES (DTO + Include) =======================
app.MapGet("/api/clientes", async (AppDbContext db) =>
    await db.Clientes
        .AsNoTracking()
        .Include(c => c.Servicios)
        .Select(c => new ClienteDto
        {
            Id = c.Id,
            NombreCliente = c.NombreCliente,
            Correo = c.Correo,
            Servicios = c.Servicios
                .Select(s => new ServicioMiniDto { Id = s.Id, NombreServicio = s.NombreServicio })
                .ToList()
        })
        .ToListAsync()
);

app.MapGet("/api/clientes/{id:int}", async (int id, AppDbContext db) =>
{
    var c = await db.Clientes
        .AsNoTracking()
        .Include(x => x.Servicios)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (c is null) return Results.NotFound();

    var dto = new ClienteDto
    {
        Id = c.Id,
        NombreCliente = c.NombreCliente,
        Correo = c.Correo,
        Servicios = c.Servicios
            .Select(s => new ServicioMiniDto { Id = s.Id, NombreServicio = s.NombreServicio })
            .ToList()
    };

    return Results.Ok(dto);
});

app.MapPost("/api/clientes", async (CreateClienteDto dto, AppDbContext db) =>
{
    if (dto is null) return Results.BadRequest("Body requerido.");
    if (string.IsNullOrWhiteSpace(dto.NombreCliente) || string.IsNullOrWhiteSpace(dto.Correo))
        return Results.BadRequest("nombreCliente y correo son obligatorios.");
    if (dto.ServicioIds is null || dto.ServicioIds.Count == 0)
        return Results.BadRequest("Debe seleccionar al menos un servicio.");

    var servicios = await db.Servicios
        .Where(s => dto.ServicioIds.Contains(s.Id))
        .ToListAsync();

    var faltantes = dto.ServicioIds.Except(servicios.Select(s => s.Id)).ToList();
    if (faltantes.Count > 0)
        return Results.BadRequest(new { message = "Algunos servicios no existen.", faltantes });

    var c = new Cliente
    {
        NombreCliente = dto.NombreCliente.Trim(),
        Correo = dto.Correo.Trim(),
        Servicios = servicios
    };

    db.Clientes.Add(c);
    await db.SaveChangesAsync();

    var result = new ClienteDto
    {
        Id = c.Id,
        NombreCliente = c.NombreCliente,
        Correo = c.Correo,
        Servicios = servicios
            .Select(s => new ServicioMiniDto { Id = s.Id, NombreServicio = s.NombreServicio })
            .ToList()
    };

    return Results.Created($"/api/clientes/{c.Id}", result);
});

app.MapPut("/api/clientes/{id:int}", async (int id, ClienteBasicUpdDto upd, AppDbContext db) =>
{
    var c = await db.Clientes.FindAsync(id);
    if (c is null) return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(upd.NombreCliente)) c.NombreCliente = upd.NombreCliente.Trim();
    if (!string.IsNullOrWhiteSpace(upd.Correo))        c.Correo        = upd.Correo.Trim();

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/api/clientes/{id:int}", async (int id, AppDbContext db) =>
{
    var c = await db.Clientes.FindAsync(id);
    if (c is null) return Results.NotFound();
    db.Clientes.Remove(c);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ======================= SERVICIOS =======================
app.MapGet("/api/servicios", async (AppDbContext db) =>
    await db.Servicios.AsNoTracking().ToListAsync());

app.MapGet("/api/servicios/{id:int}", async (int id, AppDbContext db) =>
    await db.Servicios.FindAsync(id) is Servicio s ? Results.Ok(s) : Results.NotFound());

app.MapPost("/api/servicios", async (Servicio s, AppDbContext db) =>
{
    db.Servicios.Add(s);
    await db.SaveChangesAsync();
    return Results.Created($"/api/servicios/{s.Id}", s);
});

app.MapPut("/api/servicios/{id:int}", async (int id, Servicio upd, AppDbContext db) =>
{
    var s = await db.Servicios.FindAsync(id);
    if (s is null) return Results.NotFound();
    s.NombreServicio = upd.NombreServicio;
    s.Descripcion    = upd.Descripcion;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/api/servicios/{id:int}", async (int id, AppDbContext db) =>
{
    var s = await db.Servicios.FindAsync(id);
    if (s is null) return Results.NotFound();
    db.Servicios.Remove(s);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

// ======================= DTOs =======================
public class ServicioMiniDto
{
    public int Id { get; set; }
    public string NombreServicio { get; set; } = string.Empty;
}
public class ClienteDto
{
    public int Id { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public List<ServicioMiniDto> Servicios { get; set; } = new();
}
public class CreateClienteDto
{
    public string NombreCliente { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public List<int> ServicioIds { get; set; } = new();
}
public class ClienteBasicUpdDto
{
    public string? NombreCliente { get; set; }
    public string? Correo { get; set; }
}
