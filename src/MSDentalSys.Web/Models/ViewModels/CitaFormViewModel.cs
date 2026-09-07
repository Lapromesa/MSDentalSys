using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MSDentalSys.Web.Models.ViewModels
{
    public class CitaFormViewModel
    {
        [Required(ErrorMessage = "Selecciona un paciente.")]
        [Display(Name = "Paciente")]
        public int PacienteId { get; set; }

        [Display(Name = "Paciente")]
        public string? PacienteNombre { get; set; }

        [Required(ErrorMessage = "Selecciona un odontólogo.")]
        [Display(Name = "Odontólogo")]
        public string OdontologoId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona un servicio odontológico.")]
        [Display(Name = "Servicio odontológico")]
        public int ServicioOdontologicoId { get; set; }

        [Required(ErrorMessage = "Indica la fecha y hora de la cita.")]
        [Display(Name = "Fecha y hora")]
        public DateTime FechaHoraInicio { get; set; } = DateTime.Now.AddHours(1);

        [StringLength(300, ErrorMessage = "Las observaciones no pueden superar los 300 caracteres.")]
        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        public IEnumerable<SelectListItem> Odontologos { get; set; } = [];
        public IEnumerable<SelectListItem> Servicios { get; set; } = [];
    }
}
