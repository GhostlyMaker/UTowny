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
  context.ContainerBuilder.RegisterType<UTowny.Protection.InteractionAdapter>().SingleInstance();
  context.ContainerBuilder.RegisterType<UTowny.Visualization.ClaimToolService>().SingleInstance();
  context.ContainerBuilder.RegisterType<DomainEventPublisher>().SingleInstance();
  context.ContainerBuilder.RegisterType<ConfigurationService>().SingleInstance();
  context.ContainerBuilder.RegisterType<BackgroundQueue>().SingleInstance();
  context.ContainerBuilder.RegisterType<MutationGate>().SingleInstance();
  context.ContainerBuilder.RegisterType<ExtendedSchema>().SingleInstance();
  context.ContainerBuilder.RegisterType<TownManagementService>().As<ITownManagementService>().SingleInstance();
  // The activator's plugin accessor is empty until OnLoadAsync completes.
  var workingDirectory = context.WorkingDirectory;
  context.ContainerBuilder.Register(c => new DatabaseConnectionFactory(workingDirectory, c.Resolve<IOptions<UTownyOptions>>()))
    .As<IDatabaseConnectionFactory>().SingleInstance();
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
  context.ContainerBuilder.RegisterType<UTownyApi>().AsSelf().As<IUTownyApi>().SingleInstance();
  context.ContainerBuilder.RegisterType<NationService>().AsSelf().As<INationService>().SingleInstance();
  context.ContainerBuilder.RegisterType<WarService>().AsSelf().As<IWarService>().SingleInstance();
  context.ContainerBuilder.RegisterType<ScheduledService>().AsSelf().SingleInstance();
  context.ContainerBuilder.RegisterType<LandManagementService>().AsSelf().SingleInstance();
  context.ContainerBuilder.RegisterType<AdminService>().AsSelf().SingleInstance();
  context.ContainerBuilder.RegisterType<UTowny.Commands.CommandRouter>().AsSelf().SingleInstance();
  context.ContainerBuilder.RegisterType<ShopService>().AsSelf().As<IShopService>().SingleInstance();
  context.ContainerBuilder.RegisterType<TeleportService>().AsSelf().SingleInstance();
  context.ContainerBuilder.RegisterType<UTowny.Protection.ProtectionService>().AsSelf().As<UTowny.Protection.IProtectionService>().SingleInstance();
 }
}
