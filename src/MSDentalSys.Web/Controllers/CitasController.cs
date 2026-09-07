using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MSDentalSys.Data.Context;
using MSDentalSys.Data.Models;
using MSDentalSys.Web.Models.ViewModels;

namespace MSDentalSys.Web.Controllers
{
    [Authorize(Roles = "Administrador,Odontologo,Recepcionista")]
    public class CitasController : Controller
    {
        private static readonly string[] EstadosPermitidos =
        [
            "Pendiente",
            "Confirmada",
            "Atendida",
            "Cancelada",
            "No asistió"
        ];

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CitasController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? fecha, string? estado, int? pacienteId)
        {
            var query = _context.Citas
                .Include(c => c.Paciente)
                .Include(c => c.Odontologo)
                .Include(c => c.ServicioOdontologico)
                .AsNoTracking()
                .AsQueryable();

            if (User.IsInRole("Odontologo"))
            {
                query = query.Where(c => c.OdontologoId == User.FindFirstValue(ClaimTypes.NameIdentifier));
            }

            if (fecha.HasValue)
            {
                var dayStart = fecha.Value.Date;
                var nextDay = dayStart.AddDays(1);
                query = query.Where(c => c.FechaHoraInicio >= dayStart && c.FechaHoraInicio < nextDay);
            }

            if (!string.IsNullOrWhiteSpace(estado) && EstadosPermitidos.Contains(estado))
            {
                query = query.Where(c => c.EstadoCita == estado);
            }

            if (pacienteId.HasValue)
            {
                query = query.Where(c => c.PacienteId == pacienteId.Value);
            }

            var citas = await query
                .OrderBy(c => c.FechaHoraInicio)
                .ToListAsync();

            ViewData["Fecha"] = fecha?.ToString("yyyy-MM-dd");
            ViewData["Estado"] = estado;
            ViewData["PacienteId"] = pacienteId;
            ViewData["PacienteNombre"] = pacienteId.HasValue
                ? await _context.Pacientes
                    .Where(p => p.PacienteId == pacienteId.Value)
                    .Select(p => p.Nombre + " " + p.Apellido)
                    .SingleOrDefaultAsync()
                : null;

            return View(citas);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null)
            {
                return NotFound();
            }

            var cita = await GetCitaQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CitaId == id);

            if (cita is null)
            {
                return NotFound();
            }

            if (!CanAccessAppointment(cita))
            {
                return Forbid();
            }

            ViewData["Atencion"] = await _context.AtencionesOdontologicas
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.CitaId == cita.CitaId);

            return View(cita);
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public async Task<IActionResult> Create()
        {
            var model = new CitaFormViewModel
            {
                FechaHoraInicio = RoundToNextHalfHour(DateTime.Now.AddHours(1))
            };

            await LoadFormOptionsAsync(model);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarPacientes(string? termino)
        {
            if (string.IsNullOrWhiteSpace(termino))
            {
                return Json(Array.Empty<object>());
            }

            var term = termino.Trim();
            var pacientes = await _context.Pacientes
                .AsNoTracking()
                .Where(p => p.Estado &&
                    (EF.Functions.Like(p.Nombre, $"%{term}%") ||
                     EF.Functions.Like(p.Apellido, $"%{term}%") ||
                     (p.Cedula != null && EF.Functions.Like(p.Cedula, $"%{term}%"))))
                .OrderBy(p => p.Apellido)
                .ThenBy(p => p.Nombre)
                .Take(10)
                .Select(p => new
                {
                    id = p.PacienteId,
                    nombreCompleto = p.Nombre + " " + p.Apellido,
                    cedula = p.Cedula
                })
                .ToListAsync();

            return Json(pacientes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public async Task<IActionResult> Create(CitaFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadFormOptionsAsync(model);
                return View(model);
            }

            if (!await IsActivePatientAsync(model.PacienteId))
            {
                ModelState.AddModelError(nameof(model.PacienteId), "El paciente seleccionado no existe o está inactivo.");
            }

            if (!await IsActiveOdontologistAsync(model.OdontologoId))
            {
                ModelState.AddModelError(nameof(model.OdontologoId), "El odontólogo seleccionado no existe, no tiene el rol requerido o está inactivo.");
            }

            if (!await IsActiveServiceAsync(model.ServicioOdontologicoId))
            {
                ModelState.AddModelError(nameof(model.ServicioOdontologicoId), "El servicio seleccionado no existe o está inactivo.");
            }

            if (await HasScheduleConflictAsync(model.OdontologoId, model.FechaHoraInicio, null))
            {
                ModelState.AddModelError(nameof(model.FechaHoraInicio), "El odontólogo ya tiene otra cita en esa fecha y hora.");
            }

            if (!ModelState.IsValid)
            {
                await LoadFormOptionsAsync(model);
                return View(model);
            }

            _context.Citas.Add(new Cita
            {
                PacienteId = model.PacienteId,
                OdontologoId = model.OdontologoId,
                ServicioOdontologicoId = model.ServicioOdontologicoId,
                FechaHoraInicio = model.FechaHoraInicio,
                EstadoCita = "Pendiente",
                Observaciones = NullIfWhiteSpace(model.Observaciones),
                FechaCreacion = DateTime.Now
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cita registrada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public async Task<IActionResult> Reschedule(int? id)
        {
            if (id is null)
            {
                return NotFound();
            }

            var cita = await _context.Citas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CitaId == id);

            if (cita is not null && IsFinalStatus(cita.EstadoCita))
            {
                return RedirectFinalAppointment(cita.EstadoCita, cita.CitaId);
            }

            return cita is null
                ? NotFound()
                : View(new ReagendarCitaViewModel
                {
                    CitaId = cita.CitaId,
                    FechaHoraInicio = cita.FechaHoraInicio
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public async Task<IActionResult> Reschedule(int id, ReagendarCitaViewModel model)
        {
            if (id != model.CitaId)
            {
                return NotFound();
            }

            var cita = await _context.Citas.FirstOrDefaultAsync(c => c.CitaId == id);

            if (cita is null)
            {
                return NotFound();
            }

            if (IsFinalStatus(cita.EstadoCita))
            {
                return RedirectFinalAppointment(cita.EstadoCita, id);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await HasScheduleConflictAsync(cita.OdontologoId, model.FechaHoraInicio, id))
            {
                ModelState.AddModelError(nameof(model.FechaHoraInicio), "El odontólogo ya tiene otra cita en esa fecha y hora.");
                return View(model);
            }

            cita.FechaHoraInicio = model.FechaHoraInicio;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cita reagendada correctamente.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public Task<IActionResult> Confirm(int id)
        {
            return ChangeStatusAsync(id, "Confirmada", "Cita confirmada correctamente.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public Task<IActionResult> Cancel(int id)
        {
            return ChangeStatusAsync(id, "Cancelada", "Cita cancelada correctamente.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, ActualizarEstadoCitaViewModel model)
        {
            var cita = await _context.Citas.FindAsync(id);

            if (cita is null)
            {
                return NotFound();
            }

            if (!CanAccessAppointment(cita))
            {
                return Forbid();
            }

            if (IsFinalStatus(cita.EstadoCita))
            {
                return RedirectFinalAppointment(cita.EstadoCita, id);
            }

            if (id != model.CitaId || !EstadosPermitidos.Contains(model.EstadoCita))
            {
                TempData["ErrorMessage"] = "El estado seleccionado no es válido.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (model.EstadoCita == "Atendida")
            {
                TempData["ErrorMessage"] = "Una cita solo puede marcarse como atendida al registrar su atención odontológica.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (User.IsInRole("Odontologo"))
            {
                if (model.EstadoCita != EstadosPermitidos[4])
                {
                    TempData["ErrorMessage"] = "El odontólogo solo puede marcar citas como No asistió.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            return await ChangeStatusAsync(id, model.EstadoCita, "Estado de la cita actualizado correctamente.");
        }

        private async Task<IActionResult> ChangeStatusAsync(int id, string status, string message)
        {
            var cita = await _context.Citas.FindAsync(id);

            if (cita is null)
            {
                return NotFound();
            }

            if (IsFinalStatus(cita.EstadoCita))
            {
                return RedirectFinalAppointment(cita.EstadoCita, id);
            }

            cita.EstadoCita = status;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }

        private IActionResult RedirectFinalAppointment(string status, int id)
        {
            TempData["ErrorMessage"] = status == "Atendida"
                ? "La cita atendida es un estado final y no puede modificarse."
                : "La cita cancelada es un estado final y no puede modificarse.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private static bool IsFinalStatus(string status)
        {
            return status == "Cancelada" || status == "Atendida";
        }

        private bool CanAccessAppointment(Cita cita)
        {
            return !User.IsInRole("Odontologo") ||
                cita.OdontologoId == User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        private IQueryable<Cita> GetCitaQuery()
        {
            return _context.Citas
                .Include(c => c.Paciente)
                .Include(c => c.Odontologo)
                .Include(c => c.ServicioOdontologico);
        }

        private async Task LoadFormOptionsAsync(CitaFormViewModel model)
        {
            var odontologos = (await _userManager.GetUsersInRoleAsync("Odontologo"))
                .Where(u => u.Estado)
                .OrderBy(u => u.Apellido)
                .ThenBy(u => u.Nombre)
                .ToList();

            var servicios = await _context.ServiciosOdontologicos
                .Where(s => s.Estado)
                .OrderBy(s => s.Nombre)
                .AsNoTracking()
                .ToListAsync();

            model.PacienteNombre = model.PacienteId > 0
                ? await _context.Pacientes
                    .Where(p => p.PacienteId == model.PacienteId && p.Estado)
                    .Select(p => p.Nombre + " " + p.Apellido)
                    .SingleOrDefaultAsync()
                : null;
            model.Odontologos = odontologos.Select(o => new SelectListItem
            {
                Value = o.Id,
                Text = $"{o.Nombre} {o.Apellido}",
                Selected = o.Id == model.OdontologoId
            });
            model.Servicios = servicios.Select(s => new SelectListItem
            {
                Value = s.ServicioOdontologicoId.ToString(),
                Text = s.Nombre,
                Selected = s.ServicioOdontologicoId == model.ServicioOdontologicoId
            });
        }

        private async Task<bool> IsActivePatientAsync(int pacienteId)
        {
            return await _context.Pacientes.AnyAsync(p => p.PacienteId == pacienteId && p.Estado);
        }

        private async Task<bool> IsActiveOdontologistAsync(string odontologoId)
        {
            if (string.IsNullOrWhiteSpace(odontologoId))
            {
                return false;
            }

            var user = await _userManager.FindByIdAsync(odontologoId);
            return user is not null && user.Estado && await _userManager.IsInRoleAsync(user, "Odontologo");
        }

        private async Task<bool> IsActiveServiceAsync(int servicioId)
        {
            return await _context.ServiciosOdontologicos.AnyAsync(s => s.ServicioOdontologicoId == servicioId && s.Estado);
        }

        private async Task<bool> HasScheduleConflictAsync(string odontologoId, DateTime dateTime, int? excludedCitaId)
        {
            return await _context.Citas.AnyAsync(c =>
                c.OdontologoId == odontologoId &&
                c.FechaHoraInicio == dateTime &&
                c.EstadoCita != "Cancelada" &&
                (!excludedCitaId.HasValue || c.CitaId != excludedCitaId.Value));
        }

        private static DateTime RoundToNextHalfHour(DateTime value)
        {
            var roundedMinutes = value.Minute < 30 ? 30 : 0;
            var rounded = new DateTime(value.Year, value.Month, value.Day, value.Hour, roundedMinutes, 0);
            return value.Minute < 30 ? rounded : rounded.AddHours(1);
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
