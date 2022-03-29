
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;


var builder = WebApplication.CreateBuilder(args);


// In process host without streams, note this will get loaded by each web "session" without awareness of other users
builder.Services.AddScoped<BlazorSampleApp.MandelbrotExplorer.IMandelbrotBasic, BlazorSampleApp.MandelbrotExplorer.MandelbrotBasic>();

// Out of process host singleton instance of ILGPU Host 
builder.Services.AddSingleton<BlazorSampleApp.ILGPUWebHost.IComputeHost, BlazorSampleApp.ILGPUWebHost.ComputeHost>();

// In process compute session access for multiple accelerator streams
builder.Services.AddScoped<BlazorSampleApp.MandelbrotExplorer.IMandelbrotClient, BlazorSampleApp.MandelbrotExplorer.MandelbrotClient>(); 



// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
