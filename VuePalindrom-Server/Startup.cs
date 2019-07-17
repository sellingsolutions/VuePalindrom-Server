using Starcounter.Startup.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Starcounter.Startup.Routing;

namespace VuePalindrom_Server
{
    internal class Startup : IStartup
    {
        public void Configure(IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.ApplicationServices.GetRouter().RegisterAllFromCurrentAssembly();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouter();
        }
    }
}