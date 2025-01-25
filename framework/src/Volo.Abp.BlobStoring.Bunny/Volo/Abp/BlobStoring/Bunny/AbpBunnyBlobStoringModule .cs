using Volo.Abp.Modularity;

namespace Volo.Abp.BlobStoring.Bunny;

[DependsOn(typeof(AbpBlobStoringModule))]
public class AbpBlobStoringBunnyModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}