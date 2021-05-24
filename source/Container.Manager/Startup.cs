using System;
using System.Text;
using Docker.DotNet;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpLab.Container.Manager.Internal;

namespace SharpLab.Container.Manager {
    public class Startup {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<SessionDebugLog>();

            // TODO: proper DI, e.g. Autofac
            var executionAuthorization = "Bearer " + (
                Environment.GetEnvironmentVariable("SHARPLAB_CONTAINER_HOST_ACCESS_TOKEN")
                ?? throw new Exception("Required environment variable SHARPLAB_CONTAINER_HOST_ACCESS_TOKEN was not provided.")
            );
            services.AddSingleton(new ExecutionEndpointSettings(executionAuthorization));
            services.AddSingleton<ExecutionEndpoint>();

            services.AddSingleton<DockerClientConfiguration>();

            services.AddSingleton<ContainerNameFormat>();
            services.AddSingleton<ContainerPool>();

            services.AddHostedService<ContainerAllocationWorker>();
            services.AddSingleton<ContainerCleanupWorker>();
            services.AddHostedService(c => c.GetRequiredService<ContainerCleanupWorker>());

            services.AddSingleton<ExecutionHandler>();
            services.AddSingleton<StdinWriter>();
            services.AddSingleton<StdoutReader>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseRouting();

            app.UseEndpoints(endpoints => {
                var okBytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("OK"));
                endpoints.MapGet("/status", context => {
                    context.Response.ContentType = "text/plain";
                    return context.Response.BodyWriter.WriteAsync(okBytes).AsTask();
                });

                var endpoint = app.ApplicationServices.GetRequiredService<ExecutionEndpoint>();
                endpoints.MapPost("/", endpoint.ExecuteAsync);
            });
        }
    }
}
