using Bindito.Core;
using Timberborn.Buildings;
using Timberborn.TemplateInstantiation;
using Timberborn.WorkSystem;

namespace Calloatti.BotStorage
{
  [Context("Game")]
  public class BotStorageConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<BotStorageBuilding>().AsTransient();
      Bind<BotStorageBannerSetter>().AsTransient();
      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      var builder = new TemplateModule.Builder();

      builder.AddDecorator<BotStorageBuildingSpec, BotStorageBuilding>();
      builder.AddDecorator<BotStorageBuildingSpec, WaitInsideIdlyWorkplaceBehavior>();
      builder.AddDecorator<BotStorageBuildingSpec, BotStorageBannerSetter>();
      builder.AddDecorator<BotStorageBuildingSpec, PausableBuilding>();

      return builder.Build();
    }
  }
}
