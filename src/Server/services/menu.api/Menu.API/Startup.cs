﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using AutoMapper;
using IdentityModel;
using Menu.API.Abstraction.Facades;
using Menu.API.Abstraction.Managers;
using Menu.API.Abstraction.Repositories;
using Menu.API.Abstraction.Services;
using Menu.API.Data;
using Menu.API.Facades;
using Menu.API.Managers;
using Menu.API.Models;
using Menu.API.Repositories;
using Menu.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Steeltoe.Common.Discovery;
using Steeltoe.Discovery.Client;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.Swagger;

namespace Menu.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddAuthorization();

            var identityUrl = Configuration["InternalIdentityUrl"];
            var externalIdentityUrl = Configuration["ExternalIdentityUrl"];
            
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.Authority = identityUrl;
                options.RequireHttpsMetadata = false;
                options.Audience = "menu-api";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = JwtClaimTypes.Name,
                    RoleClaimType = JwtClaimTypes.Role,
                    ValidateIssuer = false
                };
            });

            var connectionString = Configuration.GetConnectionString("MenuDatabaseConnectionString");
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });

            services.AddCors(o => o.AddPolicy("ServerPolicy", builder =>
            {
                builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("WWW-Authenticate");
            }));

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Swashbuckle.AspNetCore.Swagger.Info
                {
                    Title = "Restaurant - Menu HTTP API",
                    Version = "v1",
                    TermsOfService = "Terms Of Service"
                });

                options.AddSecurityDefinition("oauth2", new OAuth2Scheme
                {
                    Type = "oauth2",
                    Flow = "implicit",
                    AuthorizationUrl = $"{externalIdentityUrl}/connect/authorize",
                    TokenUrl = $"{externalIdentityUrl}/connect/token",
                    Scopes = new Dictionary<string, string>()
                    {
                        { "menu-api", "Restaurant Menu Api" }
                    }
                });
                options.OperationFilter<SecurityRequirementsOperationFilter>();
            });

            services.AddScoped<IAmazonS3>(provider => 
            {
                var configuration = provider.GetService<IConfiguration>();
                var  = "AKIA53WX4PRAJMOEMCPC";
                var  = "9IQpaBuifonqDDXs82CG6aSFtVkIXk6WY3AsCaLW";
                var amazonInstance = new AmazonS3Client(, , RegionEndpoint.EUCentral1);
                return amazonInstance;
            });

            services.AddScoped<IFileUploadManager, LocalFileUploadManager>();
            services.AddScoped<IFileInfoFacade, FileInfoFacade>();
            services.AddScoped<IRepository<Category>, CategoryRepository>();
            services.AddScoped<IRepository<Food>, FoodRepository>();
            services.AddScoped<IRepository<FoodPicture>, PictureRepository>();
            services.AddScoped<IFoodPictureService, FoodPictureService>();
            services.AddAutoMapper(typeof(Startup).GetTypeInfo().Assembly);
            services.AddDiscoveryClient(Configuration);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseAuthentication();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDiscoveryClient();
            }
            else
            {
                app.UseHsts();
            }

            app.UseCors("ServerPolicy");
            app.UseMvcWithDefaultRoute();
            app.UseStaticFiles();
            app.UseMvc();

            var logger = loggerFactory.CreateLogger("init");
            var pathBase = Configuration["PATH_BASE"];
            var routePrefix = (!string.IsNullOrEmpty(pathBase) ? pathBase : string.Empty);

            if (!string.IsNullOrEmpty(pathBase))
            {
                logger.LogDebug($"Using PATH BASE '{pathBase}'");
                app.UsePathBase(pathBase);
            }
            app.UseSwagger(c =>
            {
                if (routePrefix != string.Empty)
                {
                    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                    {
                        swaggerDoc.Schemes = new List<string>() { httpReq.Scheme };
                        swaggerDoc.BasePath = routePrefix;
                    });
                }
            }).UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint($"{routePrefix}/swagger/v1/swagger.json", "Menu.API V1");
                c.OAuthClientId("menu-api-swagger-ui");
                c.OAuthAppName("Menu API Swagger UI");
            });
        }
    }
}
