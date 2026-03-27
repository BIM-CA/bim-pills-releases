using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using System.IO;

var app = WebApplication.Create(args);
app.UseDefaultFiles();
app.UseStaticFiles();
app.Run("http://localhost:3000");
