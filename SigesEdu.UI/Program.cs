using SigesEdu.Application.Services;
using SigesEdu.Domain.Interfaces;
using SigesEdu.Infrastructure.Persistence;
using SigesEdu.Infrastructure.Persistence.Repositories;
using SigesEdu.Infrastructure.Security;
using SigesEdu.UI.Workers;
using SigesEdu.Infrastructure.Reports;

var builder = WebApplication.CreateBuilder(args);

// Soporte MVC
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Cadena de conexión e inyección de persistencia
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                      ?? "Data Source=sigesedu.db";

builder.Services.AddSingleton<IDbConnectionFactory>(new SQLiteConnectionFactory(connectionString));
builder.Services.AddScoped<IDbInitializer, DbInitializer>();

// === AQUÍ INYECTAMOS EL UNIT OF WORK REAL ===
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Soporte para variables de sesión (Requerido por el interceptor RBAC)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Inyección de dependencias del subsistema de seguridad y persistencia
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IAuthService, AuthService>(); // <-- AuthService restaurado
builder.Services.AddScoped<IMotorEstadosMatriculaService, MotorEstadosMatriculaService>();
builder.Services.AddScoped<ICupoTransaccionalService, CupoTransaccionalService>();
builder.Services.AddScoped<IPrelacionAcademicaService, PrelacionAcademicaService>();
builder.Services.AddHostedService<LiberacionCuposWorker>();
builder.Services.AddScoped<ITesoreriaAppService, TesoreriaAppService>();
builder.Services.AddScoped<IMatriculaAppService, MatriculaAppService>();
builder.Services.AddScoped<ICalendarioEscolarService, CalendarioEscolarService>();
builder.Services.AddScoped<ICalificacionesAppService, CalificacionesAppService>();
builder.Services.AddScoped<IPdfReportGenerator, PdfReportGenerator>();
builder.Services.AddScoped<IReporteAcademicoAppService, ReporteAcademicoAppService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

var app = builder.Build();

// Inicialización y verificación de base de datos en arranque
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    dbInitializer.Initialize();
}

// Configuración de pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();