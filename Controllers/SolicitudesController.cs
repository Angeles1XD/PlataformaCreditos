using Microsoft.AspNetCore.Mvc;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using System.Linq;
using System.Security.Claims;

// 🔥 REDIS + JSON
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace PlataformaCreditos.Controllers
{
    public class SolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;

        public SolicitudesController(ApplicationDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // =========================
        // LISTADO + CACHE (60s)
        // =========================
        public async Task<IActionResult> Index(string? estado, decimal? minMonto, decimal? maxMonto,
                                   DateTime? fechaInicio, DateTime? fechaFin)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cliente = _context.Clientes
                .FirstOrDefault(c => c.UsuarioId == userId);

            if (cliente == null)
                return View(new List<SolicitudCredito>());

            var cacheKey = $"solicitudes_{userId}";

            var cachedData = await _cache.GetStringAsync(cacheKey);

            List<SolicitudCredito> lista;

            if (cachedData != null)
            {
                lista = JsonSerializer.Deserialize<List<SolicitudCredito>>(cachedData) ?? new List<SolicitudCredito>();
            }
            else
            {
                var query = _context.Solicitudes
                    .Where(s => s.ClienteId == cliente.Id)
                    .AsQueryable();

                // VALIDACIONES
                if (minMonto.HasValue && minMonto < 0)
                    ModelState.AddModelError("", "El monto mínimo no puede ser negativo");

                if (maxMonto.HasValue && maxMonto < 0)
                    ModelState.AddModelError("", "El monto máximo no puede ser negativo");

                if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio > fechaFin)
                    ModelState.AddModelError("", "La fecha inicio no puede ser mayor que la fecha fin");

                if (!ModelState.IsValid)
                    return View(new List<SolicitudCredito>());

                // FILTROS
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

                lista = query
                    .OrderByDescending(s => s.FechaSolicitud)
                    .ToList();

                // 🔥 CACHE 60s
                var options = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(60));

                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(lista),
                    options
                );
            }

            return View(lista);
        }

        // =========================
        // DETALLE + SESIÓN
        // =========================
        public IActionResult Detalle(int id)
        {
            var solicitud = _context.Solicitudes.FirstOrDefault(s => s.Id == id);

            if (solicitud == null)
                return NotFound();

            // 🔥 GUARDAR EN SESIÓN
            HttpContext.Session.SetString("UltimaSolicitud", solicitud.MontoSolicitado.ToString());

            return View(solicitud);
        }

        // =========================
        // CREAR
        // =========================
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(decimal monto)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                ModelState.AddModelError("", "Debe iniciar sesión");
                return View();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cliente = _context.Clientes.FirstOrDefault(c => c.UsuarioId == userId);

            if (cliente == null)
            {
                ModelState.AddModelError("", "Cliente no encontrado");
                return View();
            }

            if (!cliente.Activo)
            {
                ModelState.AddModelError("", "Cliente inactivo");
                return View();
            }

            if (_context.Solicitudes.Any(s =>
                s.ClienteId == cliente.Id &&
                s.Estado == EstadoSolicitud.Pendiente))
            {
                ModelState.AddModelError("", "Ya tienes una solicitud pendiente");
                return View();
            }

            if (monto <= 0)
            {
                ModelState.AddModelError("", "El monto debe ser mayor a 0");
                return View();
            }

            if (monto > cliente.IngresosMensuales * 10)
            {
                ModelState.AddModelError("", "Excede el límite (10x ingresos)");
                return View();
            }

            var solicitud = new SolicitudCredito
            {
                ClienteId = cliente.Id,
                MontoSolicitado = monto,
                FechaSolicitud = DateTime.Now,
                Estado = EstadoSolicitud.Pendiente
            };

            _context.Solicitudes.Add(solicitud);
            _context.SaveChanges();

            // 🔥 INVALIDAR CACHE
            await _cache.RemoveAsync($"solicitudes_{userId}");

            ViewBag.Mensaje = "Solicitud creada ✔";

            return View();
        }

        // =========================
        // APROBAR + INVALIDAR CACHE
        // =========================
        public async Task<IActionResult> Aprobar(int id)
        {
            var solicitud = _context.Solicitudes.FirstOrDefault(s => s.Id == id);

            if (solicitud == null)
                return NotFound();

            var cliente = _context.Clientes.FirstOrDefault(c => c.Id == solicitud.ClienteId);

            if (cliente == null)
                return NotFound();

            if (solicitud.Estado != EstadoSolicitud.Pendiente)
                return BadRequest("Ya fue procesada");

            if (solicitud.MontoSolicitado > cliente.IngresosMensuales * 5)
                return BadRequest("Excede 5x ingresos");

            solicitud.Estado = EstadoSolicitud.Aprobado;
            _context.SaveChanges();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 🔥 INVALIDAR CACHE
            await _cache.RemoveAsync($"solicitudes_{userId}");

            return RedirectToAction("Index");
        }
    }
}