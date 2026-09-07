# Arquitectura de MSDentalSys

## Configuración y migraciones compartidas

```text
Web -> configuración -> ApplicationDbContext -> SQL Server
EF CLI -> ApplicationDbContextFactory -> misma configuración DefaultConnection
       -> ApplicationDbContext -> SQL Server
```

La fábrica de Data lee la configuración Web sin referencia Data -> Web. Busca el proyecto desde el directorio actual y las ubicaciones de ejecución/ensamblado, ascendiendo por la estructura del repositorio; admite `--contentRoot` para indicar la carpeta Web. Requiere el proyecto fuente y lee su `UserSecretsId` del `.csproj`, sin duplicarlo.

El entorno procede de `--environment`, `DOTNET_ENVIRONMENT` o `ASPNETCORE_ENVIRONMENT`, en ese orden; por defecto es Production. La configuración se carga en prioridad creciente: JSON general, JSON del entorno, User Secrets de Web solo en Development, variables de entorno y argumentos. `ConnectionStrings__DefaultConnection` sobrescribe la conexión. Los comandos de desarrollo indican `-- --environment Development`.

La fábrica solo configura el contexto y el ensamblado de migraciones Data: no abre conexiones, aplica migraciones ni ejecuta seeders. Web mantiene la configuración estándar de ASP.NET Core.

Las migraciones versionadas representan la evolución del esquema. Cada base registra las aplicadas en `__EFMigrationsHistory`, mediante `MigrationId` y `ProductVersion`. El snapshot sirve de referencia para generar cambios del modelo, no como historial de una base local.

Los seeders de roles, administrador y seguros se ejecutan al iniciar Web, salvo en Testing, después de preparar el esquema mediante EF CLI. Los datos operativos (pacientes, citas, diagnósticos y tratamientos) permanecen en cada base y no se distribuyen mediante migraciones. El procedimiento está en «Flujo de migraciones para el equipo» del README.

## Arquitectura general

MSDentalSys utiliza una arquitectura MVC organizada en proyectos separados por responsabilidad. La aplicación web consume la capa de datos y el proyecto de pruebas consume la aplicación y, cuando necesita probar directamente persistencia o entidades, también la capa de datos.

```text
MSDentalSys.Tests
        ↓
MSDentalSys.Web
        ↓
MSDentalSys.Data

MSDentalSys.Tests ───────→ MSDentalSys.Data
```

No existen referencias desde `MSDentalSys.Data` o `MSDentalSys.Web` hacia `MSDentalSys.Tests`.

## Responsabilidades por proyecto

### MSDentalSys.Data

Contiene la persistencia y el modelo de dominio relacionado con ella:

- `Context/ApplicationDbContext.cs`: contexto EF Core y configuración de relaciones, índices y restricciones.
- `Context/ApplicationDbContextFactory.cs`: creación del contexto para design-time.
- `Models/`: entidades persistentes, incluyendo pacientes, citas, servicios, seguros, usuarios y antecedentes clínicos.
- `Migrations/`: migraciones existentes de Entity Framework Core.
- `InitialData/`: seeders de roles, del administrador inicial y del catálogo verificado de seguros.

### MSDentalSys.Web

Contiene la interfaz y la lógica de aplicación MVC:

- `Controllers/`: reciben solicitudes HTTP y coordinan las operaciones del sistema. Incluye `AccountController`, `DashboardController`, `PacientesController`, `CitasController`, `SegurosController`, `ServiciosController`, `UsuariosController`, `AtencionesController`, `DiagnosticosController`, `TratamientosController` y `EvolucionesClinicasController`.
- `Models/ViewModels/`: modelos específicos para formularios y vistas; no sustituyen a las entidades persistentes. Incluye `AtencionOdontologicaCreateViewModel`, `DiagnosticoCreateViewModel`, `TratamientoCreateViewModel`, `EvolucionClinicaCreateViewModel` y `SeguroFormViewModel`, además de los ViewModels administrativos.
- `Views/`: vistas Razor.
- `wwwroot/`: recursos estáticos.
- `Program.cs`: configuración de servicios, Identity, persistencia, middleware y rutas.
- `appsettings*.json`: configuración de la aplicación sin documentar aquí valores sensibles.

### MSDentalSys.Tests

Contiene las pruebas automatizadas:

- `Controllers/`: pruebas de las acciones administrativas y clínicas con contextos aislados.
- `Integration/`: pruebas HTTP con `WebApplicationFactory`, entorno `Testing` y autenticación de claims.
- `InfrastructureTests.cs`: validación mínima de la infraestructura xUnit.

## Separación de responsabilidades

- **Entidades persistentes**: representan los datos almacenados y sus relaciones en `MSDentalSys.Data.Models`.
- **ViewModels**: representan los datos que reciben formularios o que consumen vistas concretas.
- **Controllers**: aplican las reglas de la aplicación, validan solicitudes y producen resultados MVC.
- **Views**: presentan la información mediante Razor.
- **ApplicationDbContext**: conecta el modelo persistente con EF Core y configura relaciones, índices y restricciones.
- **Migraciones**: describen la evolución del esquema de base de datos.
- **Pruebas**: verifican comportamiento con SQLite InMemory y, para HTTP, con una factory aislada.

### Seguros médicos

La entidad `Seguro` contiene `SeguroId`, `Nombre`, `Estado` y `FechaCreacion`. La relación es `Seguro 1:N Paciente`: un paciente puede no tener seguro mediante `Paciente.SeguroId` nullable, y un seguro puede asociarse a múltiples pacientes.

El módulo web correspondiente está compuesto por `SegurosController`, `SeguroFormViewModel` y `Views/Seguros`. Solo el rol `Administrador` administra el catálogo. El catálogo no modela coberturas, pólizas, reclamaciones ni facturación.

`SeguroSeeder`, ubicado en `MSDentalSys.Data/InitialData`, carga el catálogo inicial verificado de forma idempotente, conserva registros manuales y no se ejecuta en el entorno `Testing`.

## Árbol detallado

```text
MSDentalSys/
├── MSDentalSys.sln
├── global.json
├── src/
│   ├── MSDentalSys.Data/
│   │   ├── Context/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   └── ApplicationDbContextFactory.cs
│   │   ├── InitialData/
│   │   │   ├── AdminSeeder.cs
│   │   │   ├── RoleSeeder.cs
│   │   │   └── SeguroSeeder.cs
│   │   ├── Migrations/
│   │   ├── Models/
│   │   └── MSDentalSys.Data.csproj
│   └── MSDentalSys.Web/
│       ├── Controllers/
│       │   ├── AtencionesController.cs
│       │   ├── DiagnosticosController.cs
│       │   ├── EvolucionesClinicasController.cs
│       │   ├── SegurosController.cs
│       │   ├── TratamientosController.cs
│       │   └── ... controladores administrativos y de autenticación
│       ├── Models/
│       │   └── ViewModels/
│       │       ├── AtencionOdontologicaCreateViewModel.cs
│       │       ├── DiagnosticoCreateViewModel.cs
│       │       ├── EvolucionClinicaCreateViewModel.cs
│       │       ├── SeguroFormViewModel.cs
│       │       ├── TratamientoCreateViewModel.cs
│       │       └── ... ViewModels administrativos
│       ├── Properties/
│       ├── Views/
│       │   ├── Atenciones/
│       │   ├── Diagnosticos/
│       │   ├── EvolucionesClinicas/
│       │   ├── Seguros/
│       │   ├── Tratamientos/
│       │   └── ... vistas administrativas y compartidas
│       ├── wwwroot/
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── MSDentalSys.Web.csproj
├── tests/
│   └── MSDentalSys.Tests/
│       ├── Controllers/
│       ├── Integration/
│       │   ├── AuthorizationIntegrationTests.cs
│       │   └── CustomWebApplicationFactory.cs
│       ├── InfrastructureTests.cs
│       └── MSDentalSys.Tests.csproj
└── docs/prototipos/
```

La carpeta `docs/prototipos/` conserva el prototipo visual histórico y no forma parte de la infraestructura automatizada nueva.

## Relaciones clínicas principales

- `Cita` 1 : 0..1 `Atención odontológica`.
- `Atención odontológica` 1 : N `Diagnósticos`.
- `Atención odontológica` 1 : N `Tratamientos`.
- `Atención odontológica` 1 : N `Evoluciones clínicas`.
- `Servicio odontológico` 1 : N `Tratamientos`.
- `Seguro` 1 : N `Paciente`.

El flujo de la aplicación es:

```text
Cita
  → Atención odontológica
      → Diagnósticos
      → Tratamientos
      → Evoluciones clínicas
```
