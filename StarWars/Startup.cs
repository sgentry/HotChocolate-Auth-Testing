using System.Security.Claims;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Playground;
using HotChocolate.Execution.Configuration;
using HotChocolate.Stitching;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StarWars.Data;
using StarWars.Types;

namespace StarWars
{
    public class Startup
    {
        const string GRAPHQL_ENDPOINT = "/graphql";

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Add the custom services like repositories etc ...
            services.AddSingleton<CharacterRepository>();
            services.AddSingleton<ReviewRepository>();

            services.AddSingleton<Query>();
            services.AddSingleton<Mutation>();
            services.AddSingleton<Subscription>();

            // Add in-memory event provider
            var eventRegistry = new InMemoryEventRegistry();
            services.AddSingleton<IEventRegistry>(eventRegistry);
            services.AddSingleton<IEventSender>(eventRegistry);

            /*Authorization not working*/
            services.AddStitchedSchema(builder => builder
                //.AddSchemaFromHttp("remoteA")

                .AddSchema("starwars", Schema.Create(config =>
                {
                    /* When I register auth here, I don't see the claims principle in ContextData at AuthorizeAsync
                     * in AuthorizeDirectiveType, while debugging. I do see it earlier in the pipeline where it's
                     * added to the builder, in QueryMiddlewareBase.
                     */
                    config.RegisterAuthorizeDirectiveType();

                    config.RegisterQueryType<QueryType>();
                    config.RegisterMutationType<MutationType>();
                    config.RegisterSubscriptionType<SubscriptionType>();

                    config.RegisterType<HumanType>();
                    config.RegisterType<DroidType>();
                    config.RegisterType<EpisodeType>();
                }))

                .AddSchemaConfiguration(config =>
                {
                    /*
                     * Calling register auth here doesn't invoke auth check in starwars schema.
                     */
                    //config.RegisterAuthorizeDirectiveType();
                    config.RegisterExtendedScalarTypes();
                    config.RegisterType<PaginationAmountType>();
                })

                //.AddExtensionsFromFile("Extensions.graphql")
            );

            /*Authorization works this way*/
            //services.AddGraphQL(sp => Schema.Create(c =>
            //{
            //    c.RegisterServiceProvider(sp);

            //    // Adds the authorize directive and
            //    // enable the authorization middleware.
            //    c.RegisterAuthorizeDirectiveType();

            //    c.RegisterQueryType<QueryType>();
            //    c.RegisterMutationType<MutationType>();
            //    c.RegisterSubscriptionType<SubscriptionType>();

            //    c.RegisterType<HumanType>();
            //    c.RegisterType<DroidType>();
            //    c.RegisterType<EpisodeType>();
            //}),
            //new QueryExecutionOptions
            //{
            //    TracingPreference = TracingPreference.Always
            //});

            // Add Authorization Policy
            services.AddAuthorization(options =>
            {
                options.AddPolicy("HasCountry", policy =>
                    policy.RequireAssertion(context =>
                        context.User.HasClaim(c =>
                            (c.Type == ClaimTypes.Country))));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseWebSockets();
            app.UseGraphiQL(GRAPHQL_ENDPOINT);
            app.UsePlayground(new PlaygroundOptions
            {
                QueryPath = GRAPHQL_ENDPOINT,
                SubscriptionPath = GRAPHQL_ENDPOINT
            });

            app.UseGraphQL(new QueryMiddlewareOptions
            {
                Path = GRAPHQL_ENDPOINT,
                OnCreateRequest = (ctx, builder, ct) =>
                {
                    var identity = new ClaimsIdentity("abc");
                    identity.AddClaim(new Claim(ClaimTypes.Country, "us"));
                    ctx.User = new ClaimsPrincipal(identity);
                    builder.SetProperty(nameof(ClaimsPrincipal), ctx.User);
                    return Task.CompletedTask;
                }
            });
           
        }
    }
}
