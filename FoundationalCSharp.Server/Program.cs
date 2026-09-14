var builder = WebApplication.CreateBuilder(args);
// إضافة الخدمات
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDynamicOrigins", builder =>
    {
        builder.SetIsOriginAllowed(origin => true) // السماح بأي نطاق ديناميكيًا
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials(); // السماح بالمصادقة عبر الكوكيز والتوكنات المحمية
    });
});

var app = builder.Build();

// تفعيل Swagger دائمًا أثناء التطوير
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll"); // تطبيق CORS
//app.UseHttpsRedirection();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.MapHub<GameHub>("/gameHub");

app.Run();