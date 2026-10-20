using Cosmo.Application.Abstractions;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cosmo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
         this IServiceCollection services,
         IConfiguration configuration)
    {
        services.AddDbContext<ICosmoDbContext, CosmoDbContext>((serviceProvider, options) =>
        {
            var connectionStrings = serviceProvider
                .GetRequiredService<IOptions<ConnectionStringOptions>>().Value;
            options.UseNpgsql(connectionStrings.CosmoDatabase);
        });

        services.AddScoped<IUserResolver, UserResolver>();

        return services;
    }
}
