using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models;

public class Cliente
{
    public int Id { get; set; }

    public string? UsuarioId { get; set; }  // 👈 nullable para evitar error

    [Range(0.01, double.MaxValue, ErrorMessage = "Ingresos deben ser mayores a 0")]
    public decimal IngresosMensuales { get; set; }

    public bool Activo { get; set; }

    public List<SolicitudCredito>? Solicitudes { get; set; } // 👈 nullable
}