using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MSDentalSys.Data.Context;
using MSDentalSys.Data.Models;
using MSDentalSys.Web.Controllers;
using MSDentalSys.Web.Models.ViewModels;
using Xunit;

namespace MSDentalSys.Tests.Controllers;

public class PacientesControllerTests
{
    [Fact]
    public async Task Create_Cumple18MananaSinCedula_EsValido()
    {
        await using var database = await TestDatabase.CreateAsync();
        var result = await database.CreateController().Create(new PacienteFormViewModel
        {
            Nombre = "Paciente", Apellido = "Prueba", FechaNacimiento = DateTime.Today.AddDays(1).AddYears(-18)
        });
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null((await database.Context.Pacientes.SingleAsync()).Cedula);
    }

    [Theory]
    [InlineData("00112345678")]
    [InlineData("001-1234567-8")]
    public async Task Create_CedulaCompleta_Normaliza(string cedula)
    {
        await using var database = await TestDatabase.CreateAsync();
        var result = await database.CreateController().Create(new PacienteFormViewModel
        {
            Nombre = "Paciente", Apellido = "Prueba", FechaNacimiento = DateTime.Today.AddYears(-25), Cedula = cedula
        });
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("001-1234567-8", (await database.Context.Pacientes.SingleAsync()).Cedula);
    }

    [Theory]
    [InlineData("001123", 25)]
    [InlineData("001123456789", 25)]
    [InlineData("ABC00112345678", 25)]
    [InlineData("0011-234567-8", 25)]
    [InlineData("001-123", 10)]
    public async Task Create_CedulaInvalida_NoPersiste(string cedula, int age)
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();
        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente", Apellido = "Prueba", FechaNacimiento = DateTime.Today.AddYears(-age), Cedula = cedula
        });
        Assert.IsType<ViewResult>(result);
        Assert.NotEmpty(controller.ModelState[nameof(PacienteFormViewModel.Cedula)]!.Errors);
        Assert.Empty(await database.Context.Pacientes.ToListAsync());
    }

    [Theory]
    [InlineData("001-1234567-8", "00112345678")]
    [InlineData("00112345678", "001-1234567-8")]
    public async Task Create_DuplicadoEquivalente_NoPersiste(string stored, string input)
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Pacientes.Add(new Paciente { Nombre = "Existente", Apellido = "Prueba", Cedula = stored });
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();
        Assert.IsType<ViewResult>(await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Nuevo", Apellido = "Prueba", Cedula = input
        }));
        Assert.Contains("otro paciente", controller.ModelState["Cedula"]!.Errors[0].ErrorMessage);
        Assert.Equal(1, await database.Context.Pacientes.CountAsync());
    }

    [Theory]
    [InlineData("001-1234567-8")]
    [InlineData("00112345678")]
    public async Task Edit_ConservaCedulaPropiaYNormaliza(string input)
    {
        await using var database = await TestDatabase.CreateAsync();
        var paciente = new Paciente { Nombre = "Paciente", Apellido = "Prueba", Cedula = "00112345678" };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();
        Assert.IsType<RedirectToActionResult>(await database.CreateController().Edit(paciente.PacienteId, new PacienteFormViewModel
        {
            PacienteId = paciente.PacienteId, Nombre = "Paciente", Apellido = "Prueba", Cedula = input
        }));
        await database.Context.Entry(paciente).ReloadAsync();
        Assert.Equal("001-1234567-8", paciente.Cedula);
    }

    [Theory]
    [InlineData("001-1234567-8")]
    [InlineData("00112345678")]
    public async Task Edit_CedulaDeOtroPaciente_Rechaza(string stored)
    {
        await using var database = await TestDatabase.CreateAsync();
        var paciente = new Paciente { Nombre = "Paciente", Apellido = "Prueba" };
        database.Context.Pacientes.AddRange(paciente, new Paciente { Nombre = "Otro", Apellido = "Prueba", Cedula = stored });
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();
        Assert.IsType<ViewResult>(await controller.Edit(paciente.PacienteId, new PacienteFormViewModel
        {
            PacienteId = paciente.PacienteId, Nombre = "Paciente", Apellido = "Prueba", Cedula = "00112345678"
        }));
        Assert.Contains("otro paciente", controller.ModelState["Cedula"]!.Errors[0].ErrorMessage);
        await database.Context.Entry(paciente).ReloadAsync();
        Assert.Null(paciente.Cedula);
    }

    [Fact]
    public async Task Create_Get_CargaSoloSegurosActivos()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Seguros.AddRange(
            database.CreateSeguro("Seguro Activo"),
            database.CreateSeguro("Seguro Inactivo", false));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateController().Create();

        var model = Assert.IsType<PacienteFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { "Seguro Activo" }, model.Seguros.Select(seguro => seguro.Text));
    }

    [Fact]
    public async Task Edit_Get_SinSeguro_CargaSegurosActivos()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Seguros.Add(database.CreateSeguro("Seguro Activo"));
        var paciente = new Paciente { Nombre = "Paciente", Apellido = "Sin Seguro" };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();

        var result = await database.CreateController().Edit(paciente.PacienteId);

        var model = Assert.IsType<PacienteFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { "Seguro Activo" }, model.Seguros.Select(seguro => seguro.Text));
    }

    [Fact]
    public async Task Edit_Get_ConSeguroActivo_ConservaLaSeleccion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seguro = database.CreateSeguro("Seguro Activo");
        database.Context.Seguros.Add(seguro);
        await database.Context.SaveChangesAsync();
        var paciente = new Paciente { Nombre = "Paciente", Apellido = "Asegurado", SeguroId = seguro.SeguroId };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();

        var result = await database.CreateController().Edit(paciente.PacienteId);

        var model = Assert.IsType<PacienteFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        var option = Assert.Single(model.Seguros);
        Assert.Equal(seguro.SeguroId.ToString(), option.Value);
        Assert.True(option.Selected);
    }

    [Fact]
    public async Task Edit_Get_ConSeguroHistoricoInactivo_MuestraSoloEseInactivo()
    {
        await using var database = await TestDatabase.CreateAsync();
        var historico = database.CreateSeguro("Seguro Histórico", false);
        var noAsociado = database.CreateSeguro("Seguro Inactivo No Asociado", false);
        database.Context.Seguros.AddRange(historico, noAsociado);
        await database.Context.SaveChangesAsync();
        var paciente = new Paciente { Nombre = "Paciente", Apellido = "Asegurado", SeguroId = historico.SeguroId };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();

        var result = await database.CreateController().Edit(paciente.PacienteId);

        var model = Assert.IsType<PacienteFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        var option = Assert.Single(model.Seguros);
        Assert.Equal("Seguro Histórico", option.Text);
        Assert.True(option.Selected);
    }

    [Fact]
    public async Task Create_PostInvalido_MantieneSegurosDisponibles()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Seguros.Add(database.CreateSeguro("Seguro Activo"));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateController().Create(new PacienteFormViewModel
        {
            Nombre = "Paciente",
            Apellido = "Sin Seguro Seleccionado",
            TieneSeguro = true
        });

        var model = Assert.IsType<PacienteFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Seguro Activo", Assert.Single(model.Seguros).Text);
    }

    [Fact]
    public async Task Edit_PostInvalido_MantieneSegurosDisponibles()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Seguros.Add(database.CreateSeguro("Seguro Activo"));
        var paciente = new Paciente { Nombre = "Paciente", Apellido = "Sin Seguro" };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();

        var result = await database.CreateController().Edit(paciente.PacienteId, new PacienteFormViewModel
        {
            PacienteId = paciente.PacienteId,
            Nombre = "Paciente",
            Apellido = "Sin Seguro",
            TieneSeguro = true
        });

        var model = Assert.IsType<PacienteFormViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Seguro Activo", Assert.Single(model.Seguros).Text);
    }

    [Fact]
    public async Task Create_ConDatosValidos_CreaPacienteYAntecedenteActivo()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var model = new PacienteFormViewModel
        {
            Nombre = "Test",
            Apellido = "Paciente",
            Cedula = "001-0000001-1",
            FechaNacimiento = new DateTime(1990, 1, 2),
            Sexo = "F",
            Telefono = "809-555-0101",
            Correo = "test.paciente@example.test",
            Alergias = "Ninguna",
            Observaciones = "Registro generado por prueba automatizada"
        };

        var result = await controller.Create(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(PacientesController.Index), redirect.ActionName);

        var paciente = await database.Context.Pacientes
            .Include(p => p.AntecedenteClinico)
            .SingleAsync();

        Assert.Equal("Test", paciente.Nombre);
        Assert.Equal("Paciente", paciente.Apellido);
        Assert.Equal("001-0000001-1", paciente.Cedula);
        Assert.Equal(new DateTime(1990, 1, 2), paciente.FechaNacimiento);
        Assert.Equal("F", paciente.Sexo);
        Assert.Equal("809-555-0101", paciente.Telefono);
        Assert.Equal("test.paciente@example.test", paciente.Correo);
        Assert.True(paciente.Estado);
        Assert.NotNull(paciente.AntecedenteClinico);
        Assert.Equal("Ninguna", paciente.AntecedenteClinico!.Alergias);
        Assert.Equal("Registro generado por prueba automatizada", paciente.AntecedenteClinico.Observaciones);
    }

    [Fact]
    public async Task Create_ConCedulaDuplicada_NoCreaSegundoPacienteYAgregaError()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Pacientes.Add(new Paciente
        {
            Nombre = "Paciente Existente",
            Apellido = "Prueba",
            Cedula = "001-0000001-1",
            Estado = true
        });
        await database.Context.SaveChangesAsync();

        var controller = database.CreateController();
        var model = new PacienteFormViewModel
        {
            Nombre = "Segundo",
            Apellido = "Paciente",
            Cedula = "001-0000001-1"
        };

        var result = await controller.Create(model);

        Assert.IsType<ViewResult>(result);
        Assert.Single(controller.ModelState[nameof(PacienteFormViewModel.Cedula)]!.Errors);
        Assert.Contains("cédula", controller.ModelState[nameof(PacienteFormViewModel.Cedula)]!.Errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await database.Context.Pacientes.CountAsync());
    }

    [Fact]
    public async Task Create_SinCedula_PermiteRegistrarDosPacientes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var firstResult = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Primero",
            Apellido = "Sin Cedula"
        });

        controller = database.CreateController();
        var secondResult = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Segundo",
            Apellido = "Sin Cedula"
        });

        Assert.IsType<RedirectToActionResult>(firstResult);
        Assert.IsType<RedirectToActionResult>(secondResult);
        Assert.Equal(2, await database.Context.Pacientes.CountAsync());
        Assert.All(await database.Context.Pacientes.ToListAsync(), paciente => Assert.Null(paciente.Cedula));
    }

    [Fact]
    public async Task Create_MasculinoConEmbarazoManipulado_LimpiaElValor()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente",
            Apellido = "Masculino",
            Sexo = "Masculino",
            Embarazo = true
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null((await database.Context.Pacientes.Include(p => p.AntecedenteClinico).SingleAsync())
            .AntecedenteClinico!.Embarazo);
    }

    [Fact]
    public async Task Create_FemeninoConEmbarazo_ConservaElValor()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente",
            Apellido = "Femenina",
            Sexo = "Femenino",
            Embarazo = true
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True((await database.Context.Pacientes.Include(p => p.AntecedenteClinico).SingleAsync())
            .AntecedenteClinico!.Embarazo);
    }

    [Fact]
    public async Task Edit_FemeninoAMasculino_NormalizaEmbarazo()
    {
        await using var database = await TestDatabase.CreateAsync();
        var paciente = new Paciente
        {
            Nombre = "Paciente",
            Apellido = "Femenina",
            Sexo = "Femenino",
            AntecedenteClinico = new AntecedenteClinico { Embarazo = true }
        };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();

        var result = await controller.Edit(paciente.PacienteId, new PacienteFormViewModel
        {
            PacienteId = paciente.PacienteId,
            Nombre = "Paciente",
            Apellido = "Masculino",
            Sexo = "Masculino",
            Embarazo = true
        });

        Assert.IsType<RedirectToActionResult>(result);
        var stored = await database.Context.Pacientes
            .Include(p => p.AntecedenteClinico)
            .SingleAsync();
        Assert.Null(stored.AntecedenteClinico!.Embarazo);
    }

    [Fact]
    public async Task Create_AdultoSinCedula_EsRechazado()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Adulto",
            Apellido = "Sin Cedula",
            FechaNacimiento = DateTime.Today.AddYears(-18).AddDays(-1)
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(controller.ModelState[nameof(PacienteFormViewModel.Cedula)]!.Errors,
            error => error.ErrorMessage.Contains("obligatoria", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await database.Context.Pacientes.ToListAsync());
    }

    [Fact]
    public async Task Create_AdultoConCedulaValida_EsAceptado()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Adulto",
            Apellido = "Con Cedula",
            FechaNacimiento = DateTime.Today.AddYears(-18).AddDays(-1),
            Cedula = "001-0000010-1"
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(await database.Context.Pacientes.ToListAsync());
    }

    [Fact]
    public async Task Create_MenorSinCedula_EsAceptado()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Menor",
            Apellido = "Sin Cedula",
            FechaNacimiento = DateTime.Today.AddYears(-17)
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null((await database.Context.Pacientes.SingleAsync()).Cedula);
    }

    [Fact]
    public async Task Create_MenorConCedulaDuplicada_EsRechazado()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.Pacientes.Add(new Paciente
        {
            Nombre = "Paciente",
            Apellido = "Existente",
            Cedula = "001-0000011-1"
        });
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Menor",
            Apellido = "Duplicado",
            FechaNacimiento = DateTime.Today.AddYears(-10),
            Cedula = "001-0000011-1"
        });

        Assert.IsType<ViewResult>(result);
        Assert.Single(await database.Context.Pacientes.ToListAsync());
    }

    [Fact]
    public async Task Create_PacienteExactamenteDe18AniosSinCedula_EsRechazado()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Exactamente",
            Apellido = "Dieciocho",
            FechaNacimiento = DateTime.Today.AddYears(-18)
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Create_SinSeguro_FuerzaSeguroIdNull()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seguro = database.CreateSeguro("Seguro Manipulado");
        database.Context.Seguros.Add(seguro);
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente",
            Apellido = "Sin Seguro",
            TieneSeguro = false,
            SeguroId = seguro.SeguroId
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null((await database.Context.Pacientes.SingleAsync()).SeguroId);
    }

    [Fact]
    public async Task Create_ConSeguroSinSeleccion_EsRechazado()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente",
            Apellido = "Sin Seleccion",
            TieneSeguro = true
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Create_ConSeguroActivo_EsAceptado()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seguro = database.CreateSeguro("Seguro Activo");
        database.Context.Seguros.Add(seguro);
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente",
            Apellido = "Asegurado",
            TieneSeguro = true,
            SeguroId = seguro.SeguroId
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(seguro.SeguroId, (await database.Context.Pacientes.SingleAsync()).SeguroId);
    }

    [Fact]
    public async Task Create_ConSeguroInexistente_EsRechazado()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente",
            Apellido = "Seguro Inexistente",
            TieneSeguro = true,
            SeguroId = 999
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await database.Context.Pacientes.ToListAsync());
    }

    [Fact]
    public async Task Create_ConSeguroInactivo_EsRechazado()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seguro = database.CreateSeguro("Seguro Inactivo", false);
        database.Context.Seguros.Add(seguro);
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();

        var result = await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente",
            Apellido = "Seguro Inactivo",
            TieneSeguro = true,
            SeguroId = seguro.SeguroId
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await database.Context.Pacientes.ToListAsync());
    }

    [Fact]
    public async Task Edit_ConservaSeguroExistente()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seguro = database.CreateSeguro("Seguro Histórico", false);
        var paciente = new Paciente { Nombre = "Paciente", Apellido = "Asegurado", Seguro = seguro };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();

        var result = await controller.Edit(paciente.PacienteId, new PacienteFormViewModel
        {
            PacienteId = paciente.PacienteId,
            Nombre = "Paciente Editado",
            Apellido = "Asegurado",
            TieneSeguro = true,
            SeguroId = seguro.SeguroId
        });

        Assert.IsType<RedirectToActionResult>(result);
        var stored = await database.Context.Pacientes.SingleAsync();
        Assert.Equal("Paciente Editado", stored.Nombre);
        Assert.Equal(seguro.SeguroId, stored.SeguroId);
    }

    [Fact]
    public async Task Create_UnSeguroPuedeAsociarseAVariosPacientes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seguro = database.CreateSeguro("Seguro Familiar");
        database.Context.Seguros.Add(seguro);
        await database.Context.SaveChangesAsync();
        var controller = database.CreateController();

        await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente Uno",
            Apellido = "Familia",
            TieneSeguro = true,
            SeguroId = seguro.SeguroId
        });
        controller = database.CreateController();
        await controller.Create(new PacienteFormViewModel
        {
            Nombre = "Paciente Dos",
            Apellido = "Familia",
            TieneSeguro = true,
            SeguroId = seguro.SeguroId
        });

        Assert.Equal(2, await database.Context.Pacientes.CountAsync(p => p.SeguroId == seguro.SeguroId));
    }

    [Fact]
    public async Task Deactivate_PacienteActivo_CambiaEstadoSinEliminarlo()
    {
        await using var database = await TestDatabase.CreateAsync();
        var paciente = new Paciente
        {
            Nombre = "Activo",
            Apellido = "Para Desactivar",
            Cedula = "001-0000002-2",
            Estado = true
        };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();

        var controller = database.CreateController();
        var result = await controller.Deactivate(paciente.PacienteId);

        Assert.IsType<RedirectToActionResult>(result);
        var stored = await database.Context.Pacientes.SingleAsync(p => p.PacienteId == paciente.PacienteId);
        Assert.False(stored.Estado);
        Assert.Equal(1, await database.Context.Pacientes.CountAsync());
    }

    [Fact]
    public async Task Activate_PacienteInactivo_CambiaEstadoYConservaDatos()
    {
        await using var database = await TestDatabase.CreateAsync();
        var paciente = new Paciente
        {
            Nombre = "Inactivo",
            Apellido = "Para Activar",
            Cedula = "001-0000003-3",
            Telefono = "809-555-0103",
            Estado = false
        };
        database.Context.Pacientes.Add(paciente);
        await database.Context.SaveChangesAsync();

        var controller = database.CreateController();
        var result = await controller.Activate(paciente.PacienteId);

        Assert.IsType<RedirectToActionResult>(result);
        var stored = await database.Context.Pacientes.SingleAsync(p => p.PacienteId == paciente.PacienteId);
        Assert.True(stored.Estado);
        Assert.Equal("Inactivo", stored.Nombre);
        Assert.Equal("Para Activar", stored.Apellido);
        Assert.Equal("001-0000003-3", stored.Cedula);
        Assert.Equal("809-555-0103", stored.Telefono);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, ApplicationDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public ApplicationDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new ApplicationDbContext(options);
            await context.Database.EnsureCreatedAsync();

            return new TestDatabase(connection, context);
        }

        public PacientesController CreateController()
        {
            var httpContext = new DefaultHttpContext();
            var controller = new PacientesController(Context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                }
            };
            controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
            return controller;
        }

        public Seguro CreateSeguro(string name, bool state = true)
        {
            return new Seguro
            {
                Nombre = name,
                Estado = state,
                FechaCreacion = new DateTime(2030, 1, 1, 8, 0, 0)
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
