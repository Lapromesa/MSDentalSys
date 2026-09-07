# MSDentalSys

## Descripción

MSDentalSys es un sistema web de gestión clínica odontológica desarrollado para apoyar las operaciones administrativas y clínicas básicas de una clínica dental.

## Objetivo del sistema

El sistema busca centralizar la gestión de pacientes, citas, servicios odontológicos y usuarios internos, además del control de acceso y la consulta de información operativa.

## Tecnologías utilizadas

- ASP.NET Core MVC
- .NET 9
- Entity Framework Core 9
- SQL Server
- ASP.NET Core Identity
- Razor Views
- HTML, CSS y JavaScript
- xUnit
- SQLite InMemory para pruebas aisladas
- Microsoft.AspNetCore.Mvc.Testing para pruebas de integración HTTP

## Arquitectura

La solución está organizada en tres proyectos:

- `MSDentalSys.Data`: contexto de Entity Framework Core, entidades, migraciones y datos iniciales.
- `MSDentalSys.Web`: aplicación ASP.NET Core MVC, controladores, ViewModels, vistas y recursos web.
- `MSDentalSys.Tests`: pruebas unitarias, de integración y de infraestructura.

```text
MSDentalSys/
├── MSDentalSys.sln
├── global.json
├── src/
│   ├── MSDentalSys.Data/
│   └── MSDentalSys.Web/
├── tests/
│   └── MSDentalSys.Tests/
└── docs/prototipos/
```

## Roles del sistema

Los roles definidos son `Administrador`, `Odontologo` y `Recepcionista`.

### Administrador

Puede gestionar completamente los pacientes, citas, servicios, usuarios y el catálogo de seguros médicos, además de consultar las estadísticas generales del sistema. También puede acceder a los módulos clínicos según los permisos establecidos.

### Recepcionista

Puede consultar, registrar y editar administrativamente los pacientes; también puede registrar y gestionar administrativamente citas, consultar servicios y acceder a las estadísticas generales. No puede administrar usuarios ni seguros médicos.

### Odontologo

Puede consultar pacientes y servicios, consultar sus citas y actualizar los estados clínicos permitidos de una cita. No puede crear ni editar pacientes ni administrar seguros médicos. También puede registrar atenciones, diagnósticos, tratamientos y evoluciones clínicas únicamente para sus atenciones asignadas. El Dashboard filtra sus estadísticas de citas por odontólogo, mientras que el total de pacientes activos es global. No puede administrar usuarios ni crear o reagendar citas administrativamente.

## Módulos implementados

- Autenticación y cierre de sesión.
- Dashboard dinámico.
- Pacientes.
- Citas.
- Servicios odontológicos.
- Administración de usuarios.
- Seguros médicos.
- Atención odontológica.
- Diagnósticos.
- Tratamientos.
- Evoluciones clínicas.

El flujo clínico implementado es:

```text
Cita
  → Atención odontológica
      → Diagnósticos
      → Tratamientos
      → Evoluciones clínicas
```

## Reglas de negocio importantes

### Pacientes

- La activación y desactivación es lógica; el registro no se elimina físicamente.
- Para pacientes de 18 años o más, la cédula es obligatoria.
- Para pacientes menores de 18 años, la cédula es opcional.
- Si un menor informa cédula, se aplican las validaciones de formato y unicidad.
- Un paciente puede tener o no seguro médico; si tiene uno, debe seleccionarse un seguro válido del catálogo.
- Los seguros inactivos no se utilizan para nuevas asociaciones.
- Los seguros no se eliminan físicamente; se administran mediante activación y desactivación.

### Citas

Los estados utilizados son `Pendiente`, `Confirmada`, `Atendida`, `Cancelada` y `No asistió`.

`Cancelada` y `Atendida` son estados finales. El sistema evita conflictos de horario para un mismo odontólogo y una cita cancelada no bloquea ese horario.

### Usuarios

- Al crear usuarios se permiten los roles `Odontologo` y `Recepcionista`.
- El administrador inicial está protegido frente a desactivación y cambio de rol.
- La activación y desactivación de usuarios es lógica.

### Servicios

- Los servicios pueden activarse y desactivarse lógicamente.
- Cada servicio puede registrar una duración estimada en minutos.

### Atención odontológica

- Una cita puede tener cero o una atención odontológica.
- Una atención conserva el paciente y el odontólogo asignados a la cita.
- Una atención puede registrar múltiples diagnósticos, tratamientos y evoluciones clínicas.
- Los diagnósticos, tratamientos y evoluciones no se eliminan físicamente desde los módulos clínicos.

### Tratamientos

- Solo se pueden asociar servicios odontológicos activos.
- Los estados permitidos son `Planificado`, `En progreso` y `Completado`.
- Un tratamiento completado no vuelve a un estado anterior.

## Base de datos

En ejecución normal, la aplicación utiliza SQL Server mediante Entity Framework Core. El acceso se centraliza en `ApplicationDbContext`. El proyecto `MSDentalSys.Data` contiene las migraciones existentes y `ApplicationDbContextFactory` permite crear el contexto para operaciones de design-time.

Las cadenas de conexión, contraseñas y secretos no forman parte de esta documentación.

## Configuración

La configuración general se encuentra en `appsettings.json` y `appsettings.Development.json`. Los datos sensibles se gestionan mediante User Secrets cuando corresponde. No se incluyen valores secretos en el repositorio.

## Configuración local para desarrollo

La aplicación obtiene la conexión de base de datos mediante `ConnectionStrings:DefaultConnection`. `Program.cs` requiere ese valor para iniciar la aplicación. El repositorio puede incluir una conexión base sin credenciales, pero cada desarrollador puede sobrescribirla localmente mediante User Secrets. Los User Secrets no se almacenan ni se transfieren mediante Git.

Para configurar una conexión local, sustituye `TU_SERVIDOR` por el nombre de la instancia SQL Server instalada en tu equipo:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=TU_SERVIDOR;Database=MSDentalSysDB;Trusted_Connection=True;TrustServerCertificate=True;" --project .\src\MSDentalSys.Web\MSDentalSys.Web.csproj
dotnet user-secrets set "SeedAdmin:Password" "TU_CLAVE_SEGURA" --project .\src\MSDentalSys.Web\MSDentalSys.Web.csproj
```

Algunos nombres de servidor posibles son `.\SQLEXPRESS`, `localhost` y `(localdb)\MSSQLLocalDB`; no todos funcionaran automaticamente. Cada integrante debe utilizar el nombre de instancia SQL Server que tenga instalado. Si una rama o copia del proyecto no contiene una conexion base, debe configurarse `ConnectionStrings:DefaultConnection` mediante User Secrets antes de ejecutar la aplicacion.

## Flujo de migraciones para el equipo

Ejecuta estos comandos PowerShell desde la raíz de la solución. Se requiere un SDK compatible con `global.json` (9.0.316) y `dotnet-ef` 9. Comprueba la herramienta con `dotnet ef --version`; si falta, instala mediante `dotnet tool install --global dotnet-ef --version "9.0.*"`.

Web y EF CLI utilizan `ConnectionStrings:DefaultConnection`. La fábrica carga, en prioridad creciente, `appsettings.json`, `appsettings.{Environment}.json`, User Secrets de Web en Development, variables de entorno y argumentos. La variable `ConnectionStrings__DefaultConnection` permite sobrescribir la conexión. No necesitas editar la fábrica para cambiar de instancia.

El entorno procede de `--environment`, después `DOTNET_ENVIRONMENT`, después `ASPNETCORE_ENVIRONMENT`; por defecto es Production. EF CLI no depende de `launchSettings.json`: indica Development para cargar User Secrets. La fábrica busca el proyecto Web desde el directorio actual y las ubicaciones de ejecución/ensamblado, ascendiendo por el repositorio. Puedes indicar su carpeta mediante `--contentRoot RUTA_A_WEB` después de `--`. Este mecanismo requiere el proyecto fuente.

### Recibir y aplicar migraciones

Configura primero los User Secrets de conexión y `SeedAdmin:Password` con los comandos anteriores. Cada integrante conserva sus valores fuera de Git.

```powershell
git pull
dotnet ef database update --project .\src\MSDentalSys.Data --startup-project .\src\MSDentalSys.Web --context ApplicationDbContext -- --environment Development
dotnet run --project .\src\MSDentalSys.Web -- --environment Development
```

Aplicar migraciones existentes es necesario al preparar una base nueva y al recibir cambios de esquema. EF registra las aplicadas en `__EFMigrationsHistory`; no requiere una copia física de otra base. La aplicación no aplica migraciones automáticamente.

Al iniciar Web se ejecutan los seeders de roles, administrador inicial y seguros, salvo en Testing. El esquema debe existir primero. `SeedAdmin:Password` se necesita para crear el administrador si todavía no existe; no cambia su contraseña si ya existe. La fábrica de EF CLI no ejecuta seeders.

### Generar una migración

Solo quien cambia el modelo genera una migración, después de integrar los cambios del equipo:

```powershell
dotnet ef migrations add NombreDescriptivoDelCambio --project .\src\MSDentalSys.Data --startup-project .\src\MSDentalSys.Web --context ApplicationDbContext --output-dir Migrations -- --environment Development
```

Revisa `Up()` y `Down()`, aplica la migración localmente con el comando anterior y verifica el resultado. Comparte en Git los cambios del modelo, el archivo de migración, su `.Designer.cs` y `ApplicationDbContextModelSnapshot.cs` juntos.

- No generes otra migración para un cambio que ya llegó mediante Git.
- No modifiques migraciones compartidas/aplicadas salvo una decisión consciente del equipo. Coordina cambios simultáneos y conflictos del snapshot.
- Las migraciones representan tablas, columnas, relaciones e índices. Pacientes, citas, diagnósticos, tratamientos y otros datos operativos no se comparten mediante migraciones.
- Los archivos físicos y respaldos SQL Server (`.mdf`, `.ldf`, `.ndf`, `.bak`) no se suben a Git.
- Los seeders mantienen datos iniciales controlados; no copian los datos operativos de otro integrante.

Para listar migraciones sin conectarse a SQL Server:

```powershell
dotnet ef migrations list --no-connect --project .\src\MSDentalSys.Data --startup-project .\src\MSDentalSys.Web --context ApplicationDbContext -- --environment Development
```

## Ejecución

Se requiere una conexión SQL Server correctamente configurada para ejecutar la aplicación normalmente.

```powershell
dotnet restore
dotnet build .\MSDentalSys.sln
dotnet run --project .\src\MSDentalSys.Web\MSDentalSys.Web.csproj
```

## Pruebas automatizadas

La solución cuenta con pruebas para los módulos administrativos y clínicos, Login/autenticación, autorización HTTP e infraestructura.

Estado actual: **196 pruebas correctas**, incluidas las comprobaciones de la fábrica y del bloqueo de Identity sin SQL Server real.

Las pruebas de datos utilizan SQLite InMemory y no utilizan `MSDentalSysDB`. Las pruebas HTTP usan `WebApplicationFactory` en el entorno `Testing`, con una base SQLite aislada y un esquema de autenticación exclusivo para Tests.

```powershell
dotnet test .\MSDentalSys.sln
```

## Seguridad

ASP.NET Core Identity bloquea temporalmente la cuenta durante 60 segundos al alcanzar cinco intentos fallidos consecutivos de inicio de sesión. La política aplica a todos los usuarios internos (Administrador, Odontologo y Recepcionista) con `LockoutEnabled` habilitado. Un acceso correcto antes del límite reinicia el contador; durante el bloqueo se rechaza incluso la contraseña correcta. Las cuentas nuevas creadas mediante UserManager tienen el bloqueo habilitado; esta configuración no corrige cuentas históricas que lo tengan deshabilitado.

- ASP.NET Core Identity gestiona usuarios y contraseñas.
- La autorización se define mediante `[Authorize]` y roles.
- Las acciones POST utilizan protección antiforgery cuando corresponde.
- Las desactivaciones son lógicas.
- El administrador inicial está protegido por reglas específicas del sistema.
- La autorización clínica valida el rol y, para el odontólogo, la asignación de la atención odontológica.
- Los módulos clínicos no realizan eliminación física de atenciones, diagnósticos, tratamientos ni evoluciones.

## Estado actual del proyecto

Los módulos administrativos y clínicos indicados en esta documentación están implementados y validados mediante pruebas automatizadas. El proyecto se encuentra en una etapa avanzada y de cierre técnico; aún pueden existir mejoras funcionales, de usabilidad, documentación y despliegue antes de considerarlo completamente finalizado.

## Autor / contexto académico

Proyecto desarrollado como parte del monográfico para optar por el título de Licenciatura en Informática en la Universidad Autónoma de Santo Domingo (UASD).
