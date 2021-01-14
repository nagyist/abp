﻿using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Volo.Abp.BlobStoring
{
    public class BlobNormalizeNamingFactory : IBlobNormalizeNamingFactory, ITransientDependency
    {
        protected IServiceProvider ServiceProvider { get; }

        public BlobNormalizeNamingFactory(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public (string containerName, string blobName) NormalizeNaming(
            BlobContainerConfiguration configuration,
            string containerName,
            string blobName)
        {

            if (!configuration.NamingNormalizers.Any())
            {
                return (containerName, blobName);
            }

            using (var scope = ServiceProvider.CreateScope())
            {
                foreach (var normalizerType in configuration.NamingNormalizers)
                {
                    var normalizer = scope.ServiceProvider
                        .GetRequiredService(normalizerType)
                        .As<IBlobNamingNormalizer>();

                    containerName = containerName.IsNullOrWhiteSpace()? containerName: normalizer.NormalizeContainerName(containerName);
                    blobName = blobName.IsNullOrWhiteSpace()? blobName: normalizer.NormalizeBlobName(blobName);
                }

                return (containerName, blobName);
            }
        }

        public string NormalizeContainerName(BlobContainerConfiguration configuration, string containerName)
        {
            if (!configuration.NamingNormalizers.Any())
            {
                return containerName;
            }

            return NormalizeNaming(configuration, containerName, null).containerName;
        }

        public string NormalizeBlobName(BlobContainerConfiguration configuration, string blobName)
        {
            if (!configuration.NamingNormalizers.Any())
            {
                return blobName;
            }

            return NormalizeNaming(configuration, null, blobName).blobName;
        }
    }
}
