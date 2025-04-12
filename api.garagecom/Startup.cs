using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Http.Features;

namespace api.garagecom
{
    public class Startup
    {
        private class CustomMetadataProvider : IDisplayMetadataProvider
        {
            public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
            {
                context.DisplayMetadata.ConvertEmptyStringToNull = false;

            }
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetIsOriginAllowed(_ => true)
                    .AllowCredentials());
            });

            services.AddMvc().AddMvcOptions(options => options.ModelMetadataDetailsProviders.Add(new CustomMetadataProvider()));

            services.AddHttpContextAccessor();
            services.Configure<FormOptions>(options =>
            {
                options.ValueCountLimit = int.MaxValue;
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartBodyLengthLimit = int.MaxValue;
                options.MemoryBufferThreshold = int.MaxValue;
            });

            services.AddOptions();
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.None;
                options.Secure = CookieSecurePolicy.Always;
            });
        }

        [Obsolete]
        public void Configure(IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Append("X-Frame-Options", "DENY");
                await next();
            });
            app.UseRouting();
            app.UseCors("CorsPolicy");

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // changes
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapControllerRoute(name: "Registration", pattern: "Registration/{controller=Home}/{action=Index}/{id?}");
                endpoints.MapControllerRoute(name: "default", pattern: "{controller=Default}/{action=Index}/{id?}");
            });
        }
    }
}
