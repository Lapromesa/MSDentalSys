using Microsoft.Extensions.DependencyInjection;
using MSDentalSys.Data.Context;
using MSDentalSys.Data.Models;
using System.Text.RegularExpressions;
using Xunit;

namespace MSDentalSys.Tests.Integration;

public class PacienteFormTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory factory;
    public PacienteFormTests(CustomWebApplicationFactory factory) => this.factory = factory;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Formulario_IncluyeCedulaCondicionalYScriptCompartido(bool edit)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Administrador");
        var url = "/Pacientes/Create";
        if (edit)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var paciente = new Paciente { Nombre = "Paciente", Apellido = "Prueba" };
            context.Pacientes.Add(paciente);
            await context.SaveChangesAsync();
            url = $"/Pacientes/Edit/{paciente.PacienteId}";
        }
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var input = Regex.Match(html, "<input\\b[^>]*\\bid=\"Cedula\"[^>]*>").Value;
        Assert.NotEmpty(input);
        Assert.Contains("inputmode=\"numeric\"", input);
        Assert.Contains("type=\"text\"", input);
        Assert.Contains("maxlength=\"13\"", input);
        Assert.Contains("aria-required=\"false\"", input);
        Assert.DoesNotMatch(@"\srequired(?:\s|=|>)", input);
        Assert.DoesNotContain("data-val-required", input);
        Assert.Contains("/js/pacientes-form.js", html);
        Assert.Contains("id=\"cedula-required\"", html);
        Assert.Contains("id=\"cedula-help\"", html);
    }
}
