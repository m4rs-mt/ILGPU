using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorSampleApp.MandelbrotExplorer;

namespace BlazorSampleApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // In process host without streams, note this will get loaded by each web "session" without awareness of other users
            services.AddScoped<BlazorSampleApp.MandelbrotExplorer.IMandelbrotBasic, BlazorSampleApp.MandelbrotExplorer.MandelbrotBasic>();

            // Out of process host singleton instance of ILGPU Host 
            services.AddSingleton<ILGPUWebHost.IComputeHost, ILGPUWebHost.ComputeHost>();

            // In process compute session access for accelerator streams
            services.AddScoped<BlazorSampleApp.MandelbrotExplorer.IMandelbrotClient, BlazorSampleApp.MandelbrotExplorer.MandelbrotClient>(); // multiple stream instances




            services.AddRazorPages();
            services.AddServerSideBlazor();
           
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
