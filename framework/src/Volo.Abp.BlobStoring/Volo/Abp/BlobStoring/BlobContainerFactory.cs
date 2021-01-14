﻿using System;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;

namespace Volo.Abp.BlobStoring
{
    public class BlobContainerFactory : IBlobContainerFactory, ITransientDependency
    {
        protected IBlobProviderSelector ProviderSelector { get; }

        protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }

        protected ICurrentTenant CurrentTenant { get; }

        protected ICancellationTokenProvider CancellationTokenProvider { get; }

        protected IServiceProvider ServiceProvider { get; }

        protected IBlobNormalizeNamingFactory BlobNormalizeNamingFactory { get; }

        public BlobContainerFactory(
            IBlobContainerConfigurationProvider configurationProvider,
            ICurrentTenant currentTenant,
            ICancellationTokenProvider cancellationTokenProvider,
            IBlobProviderSelector providerSelector,
            IServiceProvider serviceProvider,
            IBlobNormalizeNamingFactory blobNormalizeNamingFactory)
        {
            ConfigurationProvider = configurationProvider;
            CurrentTenant = currentTenant;
            CancellationTokenProvider = cancellationTokenProvider;
            ProviderSelector = providerSelector;
            ServiceProvider = serviceProvider;
            BlobNormalizeNamingFactory = blobNormalizeNamingFactory;
        }

        public virtual IBlobContainer Create(string name)
        {
            var configuration = ConfigurationProvider.Get(name);

            return new BlobContainer(
                name,
                configuration,
                ProviderSelector.Get(name),
                CurrentTenant,
                CancellationTokenProvider,
                BlobNormalizeNamingFactory,
                ServiceProvider
            );
        }
    }
}
