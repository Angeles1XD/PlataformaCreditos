using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using System.Linq;

namespace PlataformaCreditos.Controllers
{
    [Authorize(Roles = "Analista")]
    public class AnalistaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnalistaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // 🔹 LISTAR PENDIENTES
        // =====================================
        public IActionResult Index()
        {
            var pendientes = _context.Solicitudes
                .Where(s => s.Estado == EstadoSolicitud.Pendiente)
                .ToList();

            return View(pendientes);
        }

        // =====================================
        // 🔹 APROBAR SOLICITUD
        // =====================================
        public IActionResult Aprobar(int id)
        {
            var solicitud = _context.Solicitudes.FirstOrDefault(s => s.Id == id);

            if (solicitud == null)
                return NotFound();

            // ❌ No procesar si ya está aprobada/rechazada
            if (solicitud.Estado != EstadoSolicitud.Pendiente)
                return BadRequest("La solicitud ya fue procesada");

            var cliente = _context.Clientes.FirstOrDefault(c => c.Id == solicitud.ClienteId);

            if (cliente == null)
                return BadRequest("Cliente no encontrado");

            // ❌ VALIDACIÓN: máximo 5 veces ingresos
            if (solicitud.MontoSolicitado > cliente.IngresosMensuales * 5)
                return BadRequest("El monto excede 5 veces los ingresos");

            solicitud.Estado = EstadoSolicitud.Aprobado;

            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // =====================================
        // 🔹 FORMULARIO RECHAZAR
        // =====================================
        public IActionResult Rechazar(int id)
        {
            return View();
        }

        // =====================================
        // 🔹 PROCESAR RECHAZO
        // =====================================
        [HttpPost]
        public IActionResult Rechazar(int id, string motivo)
        {
            var solicitud = _context.Solicitudes.FirstOrDefault(s => s.Id == id);

            if (solicitud == null)
                return NotFound();

            // ❌ VALIDACIÓN: motivo obligatorio
            if (string.IsNullOrWhiteSpace(motivo))
            {
                ModelState.AddModelError("", "El motivo es obligatorio");
                return View();
            }

            // ❌ No procesar si ya está aprobada/rechazada
            if (solicitud.Estado != EstadoSolicitud.Pendiente)
                return BadRequest("La solicitud ya fue procesada");

            solicitud.Estado = EstadoSolicitud.Rechazado;
            solicitud.MotivoRechazo = motivo;

            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }
}