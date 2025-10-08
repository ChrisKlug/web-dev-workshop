﻿using Bazinga.AspNetCore.Authentication.Basic;
using Microsoft.Extensions.DependencyInjection;

namespace WebDevWorkshop.Testing;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = BasicAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = BasicAuthenticationDefaults.AuthenticationScheme;
        })
        .AddBasicAuthentication(creds =>
            Task.FromResult(
                creds.username.Equals("test", StringComparison.InvariantCultureIgnoreCase)
                && creds.password == "test"
            )
        );
        return services;
    }
}
