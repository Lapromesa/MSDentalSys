using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MSDentalSys.Data.Context;
using MSDentalSys.Data.Models;
using MSDentalSys.Web.Models.ViewModels;

namespace MSDentalSys.Web.Controllers
{
    [Authorize(Roles = "Administrador,Odontologo,Recepcionista")]
    public class PacientesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PacientesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchTerm)
        {
            var query = _context.Pacientes
                .Include(p => p.AntecedenteClinico)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(p =>
                    EF.Functions.Like(p.Nombre, $"%{term}%") ||
                    EF.Functions.Like(p.Apellido, $"%{term}%") ||
                    (p.Cedula != null && EF.Functions.Like(p.Cedula, $"%{term}%")) ||
                    (p.Telefono != null && EF.Functions.Like(p.Telefono, $"%{term}%")));
            }

            var pacientes = await query
                .OrderBy(p => p.Apellido)
                .ThenBy(p => p.Nombre)
                .ToListAsync();

            ViewData["SearchTerm"] = searchTerm;
            return View(pacientes);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id is null)
            {
                return NotFound();
            }

            var paciente = await _context.Pacientes
                .Include(p => p.AntecedenteClinico)
                .Include(p => p.Seguro)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PacienteId == id);

            return paciente is null ? NotFound() : View(paciente);
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public async Task<IActionResult> Create()
        {
            var model = new PacienteFormViewModel();
            await LoadSegurosAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public async Task<IActionResult> Create(PacienteFormViewModel model)
        {
            NormalizeEmbarazoBySexo(model);
            await ValidateConditionalFieldsAsync(model);

            if (!ModelState.IsValid)
            {
                await LoadSegurosAsync(model);
                return View(model);
            }

            if (await CedulaExistsAsync(model.Cedula, null))
            {
                ModelState.AddModelError(nameof(model.Cedula), "La cédula ya pertenece a otro paciente.");
                await LoadSegurosAsync(model);
                return View(model);
            }

            var paciente = new Paciente
            {
                Nombre = model.Nombre.Trim(),
                Apellido = model.Apellido.Trim(),
                Cedula = NullIfWhiteSpace(model.Cedula),
                FechaNacimiento = model.FechaNacimiento,
                SeguroId = model.SeguroId,
                Sexo = NullIfWhiteSpace(model.Sexo),
                Telefono = NullIfWhiteSpace(model.Telefono),
                Correo = NullIfWhiteSpace(model.Correo),
                Direccion = NullIfWhiteSpace(model.Direccion),
                ContactoEmergencia = NullIfWhiteSpace(model.ContactoEmergencia),
                TelefonoEmergencia = NullIfWhiteSpace(model.TelefonoEmergencia),
                Estado = true,
                FechaRegistro = DateTime.Now,
                AntecedenteClinico = new AntecedenteClinico
                {
                    Alergias = NullIfWhiteSpace(model.Alergias),
                    EnfermedadesSistemicas = NullIfWhiteSpace(model.EnfermedadesSistemicas),
                    MedicamentosActuales = NullIfWhiteSpace(model.MedicamentosActuales),
                    CirugiasPrevias = NullIfWhiteSpace(model.CirugiasPrevias),
                    HabitosRelevantes = NullIfWhiteSpace(model.HabitosRelevantes),
                    Embarazo = model.Embarazo,
                    Observaciones = NullIfWhiteSpace(model.Observaciones),
                    FechaActualizacion = DateTime.Now
                }
            };

            _context.Pacientes.Add(paciente);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (IsCedulaUniqueViolation(exception))
            {
                ModelState.AddModelError(nameof(model.Cedula), "La cédula ya pertenece a otro paciente.");
                await LoadSegurosAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = "Paciente registrado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id is null)
            {
                return NotFound();
            }

            var paciente = await _context.Pacientes
                .Include(p => p.AntecedenteClinico)
                .Include(p => p.Seguro)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PacienteId == id);

            if (paciente is null)
            {
                return NotFound();
            }

            var model = ToFormViewModel(paciente);
            await LoadSegurosAsync(model, paciente.SeguroId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Recepcionista")]
        public async Task<IActionResult> Edit(int id, PacienteFormViewModel model)
        {
            if (id != model.PacienteId)
            {
                return NotFound();
            }

            var paciente = await _context.Pacientes
                .Include(p => p.AntecedenteClinico)
                .Include(p => p.Seguro)
                .FirstOrDefaultAsync(p => p.PacienteId == id);

            if (paciente is null)
            {
                return NotFound();
            }

            NormalizeEmbarazoBySexo(model);
            await ValidateConditionalFieldsAsync(model, paciente.SeguroId);

            if (!ModelState.IsValid)
            {
                await LoadSegurosAsync(model, paciente.SeguroId);
                return View(model);
            }

            if (await CedulaExistsAsync(model.Cedula, id))
            {
                ModelState.AddModelError(nameof(model.Cedula), "La cédula ya pertenece a otro paciente.");
                await LoadSegurosAsync(model, paciente.SeguroId);
                return View(model);
            }

            paciente.Nombre = model.Nombre.Trim();
            paciente.Apellido = model.Apellido.Trim();
            paciente.Cedula = NullIfWhiteSpace(model.Cedula);
            paciente.FechaNacimiento = model.FechaNacimiento;
            paciente.SeguroId = model.SeguroId;
            paciente.Sexo = NullIfWhiteSpace(model.Sexo);
            paciente.Telefono = NullIfWhiteSpace(model.Telefono);
            paciente.Correo = NullIfWhiteSpace(model.Correo);
            paciente.Direccion = NullIfWhiteSpace(model.Direccion);
            paciente.ContactoEmergencia = NullIfWhiteSpace(model.ContactoEmergencia);
            paciente.TelefonoEmergencia = NullIfWhiteSpace(model.TelefonoEmergencia);

            paciente.AntecedenteClinico ??= new AntecedenteClinico
            {
                PacienteId = paciente.PacienteId
            };

            paciente.AntecedenteClinico.Alergias = NullIfWhiteSpace(model.Alergias);
            paciente.AntecedenteClinico.EnfermedadesSistemicas = NullIfWhiteSpace(model.EnfermedadesSistemicas);
            paciente.AntecedenteClinico.MedicamentosActuales = NullIfWhiteSpace(model.MedicamentosActuales);
            paciente.AntecedenteClinico.CirugiasPrevias = NullIfWhiteSpace(model.CirugiasPrevias);
            paciente.AntecedenteClinico.HabitosRelevantes = NullIfWhiteSpace(model.HabitosRelevantes);
            paciente.AntecedenteClinico.Embarazo = model.Embarazo;
            paciente.AntecedenteClinico.Observaciones = NullIfWhiteSpace(model.Observaciones);
            paciente.AntecedenteClinico.FechaActualizacion = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (IsCedulaUniqueViolation(exception))
            {
                ModelState.AddModelError(nameof(model.Cedula), "La cédula ya pertenece a otro paciente.");
                await LoadSegurosAsync(model, paciente.SeguroId);
                return View(model);
            }

            TempData["SuccessMessage"] = "Paciente actualizado correctamente.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var paciente = await _context.Pacientes.FindAsync(id);

            if (paciente is null)
            {
                return NotFound();
            }

            paciente.Estado = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Paciente desactivado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var paciente = await _context.Pacientes.FindAsync(id);

            if (paciente is null)
            {
                return NotFound();
            }

            paciente.Estado = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Paciente activado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateConditionalFieldsAsync(PacienteFormViewModel model, int? currentSeguroId = null)
        {
            var cedula = NullIfWhiteSpace(model.Cedula);
            if (cedula is not null && !System.Text.RegularExpressions.Regex.IsMatch(
                cedula, @"\A(?:[0-9]{11}|[0-9]{3}-[0-9]{7}-[0-9])\z"))
            {
                ModelState.AddModelError(nameof(model.Cedula), "Ingresa una cédula completa de 11 dígitos con formato XXX-XXXXXXX-X.");
            }
            else
            {
                var digits = cedula?.Replace("-", "");
                model.Cedula = digits is null ? null : $"{digits[..3]}-{digits[3..10]}-{digits[10]}";
            }

            if (model.FechaNacimiento.HasValue && CalculateAge(model.FechaNacimiento.Value) >= 18 &&
                string.IsNullOrWhiteSpace(model.Cedula))
            {
                ModelState.AddModelError(nameof(model.Cedula), "La cédula es obligatoria para pacientes de 18 años o más.");
            }

            if (!model.TieneSeguro)
            {
                model.SeguroId = null;
                return;
            }

            if (!model.SeguroId.HasValue)
            {
                ModelState.AddModelError(nameof(model.SeguroId), "Selecciona un seguro médico.");
                return;
            }

            var seguroValido = await _context.Seguros.AnyAsync(seguro =>
                seguro.SeguroId == model.SeguroId.Value &&
                (seguro.Estado || seguro.SeguroId == currentSeguroId));

            if (!seguroValido)
            {
                ModelState.AddModelError(nameof(model.SeguroId), "El seguro seleccionado no existe o está inactivo.");
            }
        }

        private static void NormalizeEmbarazoBySexo(PacienteFormViewModel model)
        {
            if (!string.Equals(model.Sexo, "Femenino", StringComparison.OrdinalIgnoreCase))
            {
                model.Embarazo = null;
            }
        }

        private async Task LoadSegurosAsync(PacienteFormViewModel model, int? currentSeguroId = null)
        {
            model.Seguros = await _context.Seguros
                .AsNoTracking()
                .Where(seguro => seguro.Estado || seguro.SeguroId == currentSeguroId)
                .OrderBy(seguro => seguro.Nombre)
                .Select(seguro => new SelectListItem
                {
                    Value = seguro.SeguroId.ToString(),
                    Text = seguro.Nombre,
                    Selected = seguro.SeguroId == model.SeguroId
                })
                .ToListAsync();
        }

        private static int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;

            if (birthDate.Date > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }

        private static bool IsCedulaUniqueViolation(DbUpdateException exception)
        {
            // Solo traducimos la infracción del índice de cédula; los demás errores se propagan.
            return exception.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
                sql.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Any(error =>
                    (error.Number == 2601 || error.Number == 2627) &&
                    error.Message.Contains("IX_Pacientes_Cedula", StringComparison.Ordinal));
        }

        private async Task<bool> CedulaExistsAsync(string? cedula, int? pacienteId)
        {
            var normalizedCedula = NullIfWhiteSpace(cedula);

            if (normalizedCedula is null)
            {
                return false;
            }

            return await _context.Pacientes.AnyAsync(p =>
                (p.Cedula == normalizedCedula || p.Cedula == normalizedCedula.Replace("-", "")) &&
                (!pacienteId.HasValue || p.PacienteId != pacienteId.Value));
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static PacienteFormViewModel ToFormViewModel(Paciente paciente)
        {
            return new PacienteFormViewModel
            {
                PacienteId = paciente.PacienteId,
                Nombre = paciente.Nombre,
                Apellido = paciente.Apellido,
                Cedula = paciente.Cedula,
                FechaNacimiento = paciente.FechaNacimiento,
                TieneSeguro = paciente.SeguroId.HasValue,
                SeguroId = paciente.SeguroId,
                Sexo = paciente.Sexo,
                Telefono = paciente.Telefono,
                Correo = paciente.Correo,
                Direccion = paciente.Direccion,
                ContactoEmergencia = paciente.ContactoEmergencia,
                TelefonoEmergencia = paciente.TelefonoEmergencia,
                Alergias = paciente.AntecedenteClinico?.Alergias,
                EnfermedadesSistemicas = paciente.AntecedenteClinico?.EnfermedadesSistemicas,
                MedicamentosActuales = paciente.AntecedenteClinico?.MedicamentosActuales,
                CirugiasPrevias = paciente.AntecedenteClinico?.CirugiasPrevias,
                HabitosRelevantes = paciente.AntecedenteClinico?.HabitosRelevantes,
                Embarazo = paciente.AntecedenteClinico?.Embarazo,
                Observaciones = paciente.AntecedenteClinico?.Observaciones
            };
        }
    }
}
