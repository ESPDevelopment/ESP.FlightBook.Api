using ESP.FlightBook.Api.Extensions;
using ESP.FlightBook.Api.Models;
using ESP.Security.Extensions;
using ESP.Security.Token;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ESP.FlightBook.Api
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        public Startup(IHostingEnvironment env)
        {
            // Initialize configuration builder
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            // Configure development resources, if necessary
            if (env.IsDevelopment())
            {
                builder.AddUserSecrets();
            }

            // Add environment variables
            builder.AddEnvironmentVariables();

            // Build configuration
            Configuration = builder.Build();
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add ESP token key
            services.AddESPTokenKey(Configuration);

            // Add Bearer authorization
            services.AddAuthorization(auth =>
            {
                auth.AddPolicy("Bearer", new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser().Build());
            });

            // Add Entity Framework services to the services container
            services.AddDbContext<ApiDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("ApiConnection"))
            );

            // Require SSL connection
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add(new RequireHttpsAttribute());
            });

            // Add MVC
            services.AddMvc();

            // Add CORS support
            services.AddCors();

            // Add configuration singleton
            services.AddSingleton(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, TokenAuthOptions tokenAuthOptions)
        {
            // Configure logging
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            //app.UseDeveloperExceptionPage();
            //app.UseDatabaseErrorPage();

            // Ensure databases have been migrated and properly seeded
            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>()
                .CreateScope())
            {
                serviceScope.ServiceProvider.GetService<ApiDbContext>()
                    .Database.Migrate();
                serviceScope.ServiceProvider.GetService<ApiDbContext>()
                    .EnsureSeedData();
            }

            // Use ESP token authentication
            app.UseESPTokenAuth(tokenAuthOptions);

            // Use ESP-specific CORS
            app.UseESPCors();

            // Configure MVC
            app.UseMvc();
        }
    }
}
