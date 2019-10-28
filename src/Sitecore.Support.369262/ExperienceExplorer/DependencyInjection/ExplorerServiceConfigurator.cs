using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;
using Sitecore.ExperienceExplorer.Analytics.DataSources;
using ProfileDataSource = Sitecore.Support.ExperienceExplorer.Analytics.DataSources.ProfileDataSource;

namespace Sitecore.Support.ExperienceExplorer.DependencyInjection
{
    public class ExplorerServiceConfigurator : IServicesConfigurator
    {
        public void Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IProfileDataSource, ProfileDataSource>();
        }
    }
}