﻿#region

using api.garagecom.Middlewares;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

#endregion

namespace api.garagecom;

public class Startup
{
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

        services.AddMvc()
            .AddMvcOptions(options => options.ModelMetadataDetailsProviders.Add(new CustomMetadataProvider()));

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

        var credPath = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH")!;
        if (string.IsNullOrEmpty(credPath))
            throw new InvalidOperationException(
                "FIREBASE_CREDENTIALS_PATH not set in environment or .env file.");
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(credPath)
        });
        services.AddSingleton(FirebaseApp.DefaultInstance);
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

        app.UseRequestResponseLogging();

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        // changes
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapControllerRoute("Registration", "Registration/{controller=Home}/{action=Index}/{id?}");
            endpoints.MapControllerRoute("Notifications", "Notifications/{controller=Home}/{action=Index}/{id?}");
            endpoints.MapControllerRoute("Administrations", "Administrations/{controller=Home}/{action=Index}/{id?}");
            endpoints.MapControllerRoute("Dashboard", "Dashboard/{controller=Home}/{action=Index}/{id?}");
            endpoints.MapControllerRoute("Posts", "Posts/{controller=Home}/{action=Index}/{id?}");
            endpoints.MapControllerRoute("Cars", "Cars/{controller=Home}/{action=Index}/{id?}");
            endpoints.MapControllerRoute("Profile", "Profile/{controller=Home}/{action=Index}/{id?}");
            endpoints.MapControllerRoute("default", "{controller=Default}/{action=Index}/{id?}");
        });
    }

    private class CustomMetadataProvider : IDisplayMetadataProvider
    {
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            context.DisplayMetadata.ConvertEmptyStringToNull = false;
        }
    }
}