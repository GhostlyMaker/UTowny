using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenMod.API.Plugins;
using Autofac;
using Microsoft.Extensions.Configuration;
using UTowny.Api;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Persistence.Database;
using UTowny.Persistence.Repositories;
using UTowny.Services;

namespace UTowny.Plugin;
public sealed class ServiceConfigurator : IPluginContainerConfigurator
{
 public void ConfigureContainer(IPluginServiceConfigurationContext context)
 {
  var normalized = context.Configuration.AsEnumerable().Where(x => x.Value != null)
    .ToDictionary(x => x.Key.Replace("_", ""), x => x.Value);
  var config = new ConfigurationBuilder().AddInMemoryCollection(normalized).Build();
  var options = config.Get<UTownyOptions>() ?? new UTownyOptions();
  var validation = new ConfigValidator().Validate(null, options);
  if (validation.Failed) throw new InvalidOperationException(validation.FailureMessage);
  context.ContainerBuilder.RegisterInstance(Options.Create(options)).As<IOptions<UTownyOptions>>();
  context.ContainerBuilder.RegisterType<DatabaseConnectionFactory>().As<IDatabaseConnectionFactory>().SingleInstance();
  context.ContainerBuilder.RegisterType<SchemaMigrator>().As<ISchemaMigrator>().SingleInstance();
  context.ContainerBuilder.RegisterType<PlayerRepository>().As<IPlayerRepository>().SingleInstance();
  context.ContainerBuilder.RegisterType<WorldRepository>().As<IWorldRepository>().SingleInstance();
  context.ContainerBuilder.RegisterType<WorldStateCache>().As<IWorldStateCache>().SingleInstance();
  context.ContainerBuilder.RegisterType<EconomyService>().As<IEconomyService>().SingleInstance();
  context.ContainerBuilder.RegisterType<PlaytimeService>().As<IPlaytimeService>().SingleInstance();
  context.ContainerBuilder.RegisterType<AuthorizationService>().As<IAuthorizationService>().SingleInstance();
  context.ContainerBuilder.RegisterType<TownService>().As<ITownService>().SingleInstance();
  context.ContainerBuilder.RegisterType<GridService>().As<IGridService>().SingleInstance();
  context.ContainerBuilder.RegisterType<ClaimService>().As<IClaimService>().SingleInstance();
  context.ContainerBuilder.RegisterType<PlotService>().As<IPlotService>().SingleInstance();
  context.ContainerBuilder.RegisterType<UTownyApi>().As<IUTownyApi>().SingleInstance();
 }
}
