using Microsoft.AspNetCore.Mvc;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using System.Linq;
using System.Security.Claims;

namespace PlataformaCreditos.Controllers
{
    public class SolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SolicitudesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // LISTADO + FILTROS (PREGUNTA 2)
        // =========================
        public IActionResult Index(string? estado, decimal? minMonto, decimal? maxMonto,
                                   DateTime? fechaInicio, DateTime? fechaFin)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cliente = _context.Clientes
                .FirstOrDefault(c => c.UsuarioId == userId);

            if (cliente == null)
                return View(new List<SolicitudCredito>());

            var query = _context.Solicitudes
                .Where(s => s.ClienteId == cliente.Id)
                .AsQueryable();

            // 🔴 VALIDACIONES
            if (minMonto.HasValue && minMonto < 0)
                ModelState.AddModelError("", "El monto mínimo no puede ser negativo");

            if (maxMonto.HasValue && maxMonto < 0)
                ModelState.AddModelError("", "El monto máximo no puede ser negativo");

            if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio > fechaFin)
                ModelState.AddModelError("", "La fecha inicio no puede ser mayor que la fecha fin");

            if (!ModelState.IsValid)
                return View(new List<SolicitudCredito>());

            // 🔵 FILTROS
            if (!string.IsNullOrEmpty(estado) &&
                Enum.TryParse<EstadoSolicitud>(estado, out var estadoEnum))
            {
                query = query.Where(s => s.Estado == estadoEnum);
            }

            if (minMonto.HasValue)
                query = query.Where(s => s.MontoSolicitado >= minMonto);

            if (maxMonto.HasValue)
                query = query.Where(s => s.MontoSolicitado <= maxMonto);

            if (fechaInicio.HasValue)
                query = query.Where(s => s.FechaSolicitud >= fechaInicio);

            if (fechaFin.HasValue)
                query = query.Where(s => s.FechaSolicitud <= fechaFin);

            var lista = query
                .OrderByDescending(s => s.FechaSolicitud)
                .ToList();

            return View(lista);
        }

        // =========================
        // DETALLE (PREGUNTA 2)
        // =========================
        public IActionResult Detalle(int id)
        {
            var solicitud = _context.Solicitudes.FirstOrDefault(s => s.Id == id);

            if (solicitud == null)
                return NotFound();

            return View(solicitud);
        }

        // =========================
        // CREAR (GET)
        // =========================
        public IActionResult Crear()
        {
            return View();
        }

        // =========================
        // CREAR (POST) (PREGUNTA 3)
        // =========================
        [HttpPost]
        public IActionResult Crear(decimal monto)
        {
            // 🔴 VALIDAR AUTENTICACIÓN
            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                ModelState.AddModelError("", "Debe iniciar sesión");
                return View();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cliente = _context.Clientes
                .FirstOrDefault(c => c.UsuarioId == userId);

            if (cliente == null)
            {
                ModelState.AddModelError("", "Cliente no encontrado");
                return View();
            }

            // 🔴 CLIENTE ACTIVO
            if (!cliente.Activo)
            {
                ModelState.AddModelError("", "Cliente inactivo");
                return View();
            }

            // 🔴 SOLO 1 PENDIENTE
            if (_context.Solicitudes.Any(s =>
                s.ClienteId == cliente.Id &&
                s.Estado == EstadoSolicitud.Pendiente))
            {
                ModelState.AddModelError("", "Ya tienes una solicitud pendiente");
                return View();
            }

            // 🔴 MONTO > 0
            if (monto <= 0)
            {
                ModelState.AddModelError("", "El monto debe ser mayor a 0");
                return View();
            }

            // 🔴 MÁXIMO 10x INGRESOS
            if (monto > cliente.IngresosMensuales * 10)
            {
                ModelState.AddModelError("", "El monto supera el límite permitido (10x ingresos)");
                return View();
            }

            // ✅ CREAR
            var solicitud = new SolicitudCredito
            {
                ClienteId = cliente.Id,
                MontoSolicitado = monto,
                FechaSolicitud = DateTime.Now,
                Estado = EstadoSolicitud.Pendiente
            };

            _context.Solicitudes.Add(solicitud);
            _context.SaveChanges();

            ViewBag.Mensaje = "Solicitud creada correctamente ✔";

            return View();
        }

        // =========================
        // APROBAR
        // =========================
        public IActionResult Aprobar(int id)
        {
            var solicitud = _context.Solicitudes.FirstOrDefault(s => s.Id == id);

            if (solicitud == null)
                return NotFound();

            var cliente = _context.Clientes.FirstOrDefault(c => c.Id == solicitud.ClienteId);

            if (cliente == null)
                return NotFound();

            // 🔴 SOLO SI ES PENDIENTE
            if (solicitud.Estado != EstadoSolicitud.Pendiente)
                return BadRequest("La solicitud ya fue procesada");

            // 🔴 REGLA 5x
            if (solicitud.MontoSolicitado > cliente.IngresosMensuales * 5)
                return BadRequest("No se puede aprobar: excede 5x ingresos");

            solicitud.Estado = EstadoSolicitud.Aprobado;
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }
}