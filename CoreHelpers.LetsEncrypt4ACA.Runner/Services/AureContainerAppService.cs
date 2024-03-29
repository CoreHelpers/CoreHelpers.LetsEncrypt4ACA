﻿using System;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Org.BouncyCastle.Ocsp;

namespace Cert.Services
{
	public class AureContainerAppService
	{        
        private TokenCredential _tokenCredential;
        private ResourceIdentifier _managedEnvironmentResourceId;
        private ResourceIdentifier _subscriptionResourceId;        

        public AureContainerAppService(string tenantId, string clientId, string clientSecret, string subscriptionId, string resourceGroup, string containerAppEnvironment)
        {            
            _tokenCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);           
            _managedEnvironmentResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.App/managedEnvironments/{containerAppEnvironment}");            
            _subscriptionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}");
        }

        public async Task<IReadOnlyList<ContainerAppManagedEnvironmentCertificateResource>> GetExpiringCertificates(int expiryDays = 30)
        {
            // build the arm client
            var client = new ArmClient(_tokenCredential);
            {
                // the reference timestamp
                var currentDateTime = DateTime.Now;

                // lookup the managed environment
                var managedEnvironment = await client.GetContainerAppManagedEnvironmentResource(_managedEnvironmentResourceId).GetAsync();

                // get teh expiring certificates
                var containerAppCertificates = new List<ContainerAppManagedEnvironmentCertificateResource>();

                // get all certificates
                var allCertificates = managedEnvironment.Value.GetContainerAppManagedEnvironmentCertificates();

                // visit every certificate
                await foreach (var containerAppCertificate in allCertificates)
                {
                    // containerAppCertificate.Data.Tags.ContainsKey("IssuerName")
                    // containerAppCertificate.Data.Tags.ContainsKey("ENdpoint")
                    
                    if ((containerAppCertificate.Data.Properties.ExpireOn.Value - currentDateTime).TotalDays > expiryDays)
                    {
                        continue;
                    }

                    containerAppCertificates.Add(containerAppCertificate);                    
                }

                return containerAppCertificates;
            }
        }

        public async Task<ContainerAppManagedEnvironmentCertificateResource> UploadCertificate(string targetDomain, byte[] certificateData, string password)
        {
            // build the arm client
            var client = new ArmClient(_tokenCredential);
            {
                // lookup the managed environment
                var managedEnvironment = await client.GetContainerAppManagedEnvironmentResource(_managedEnvironmentResourceId).GetAsync();

                // build a new certificate date model
                var appCertificate = new ContainerAppCertificateData(managedEnvironment.Value.Data.Location);
                appCertificate.Properties = new ContainerAppCertificateProperties();
                appCertificate.Properties.Password = password;
                appCertificate.Properties.Value = certificateData;

                // define a unique certificate name
                var certificateName = $"{targetDomain}-{Guid.NewGuid()}".Replace("-","");
                if (certificateName.Length > 61)
                    certificateName = certificateName.Substring(0, 61);

                // upload the certificate (this call is replacing the old one)
                var resource = await managedEnvironment.Value.GetContainerAppManagedEnvironmentCertificates().CreateOrUpdateAsync(WaitUntil.Completed, certificateName, appCertificate);

                // doen
                return resource.Value;
            }
        }

        public async Task<IEnumerable<ResourceIdentifier>> UpdateCustomDomains(string targetDomain, ContainerAppManagedEnvironmentCertificateResource certificateResource)
        {
            var result = new List<ResourceIdentifier>();

            // build the arm client
            var client = new ArmClient(_tokenCredential);
            {
                var subscription = await client.GetSubscriptionResource(_subscriptionResourceId).GetAsync();
                var containerApps = subscription.Value.GetContainerAppsAsync();

                await foreach (var containerApp in containerApps)
                {
                    if (containerApp.Data.ManagedEnvironmentId != _managedEnvironmentResourceId)
                        continue;

                    var ingress = containerApp.Data.Configuration.Ingress;

                    foreach(var customDomain in ingress.CustomDomains.Where(d => d.Name.Equals(targetDomain)))
                    {
                        // remember that we replaced this certificate
                        if (!result.Contains(customDomain.CertificateId))
                            result.Add(customDomain.CertificateId);

                        // update
                        customDomain.CertificateId = certificateResource.Id;
                        customDomain.BindingType = ContainerAppCustomDomainBindingType.SniEnabled;                        
                    }
                    
                    var newContainerAppData = new ContainerAppData(containerApp.Data.Location)
                    {
                        Configuration = new ContainerAppConfiguration
                        {
                            Ingress = ingress
                        }
                    };

                    await containerApp.UpdateAsync(WaitUntil.Completed, newContainerAppData);
                }               
            }

            // done
            return result;
        }

        public async Task DeleteReplacedCertificates(IEnumerable<ResourceIdentifier> replacedCertificates)
        {
            // build the arm client
            var client = new ArmClient(_tokenCredential);
            {
                // lookup the managed environment
                var managedEnvironment = await client.GetContainerAppManagedEnvironmentResource(_managedEnvironmentResourceId).GetAsync();

                foreach(var replacedCertificate in replacedCertificates)
                {
                    Console.WriteLine($"Deleting certificate {replacedCertificate.Name}");
                    var certificateResource = await managedEnvironment.Value.GetContainerAppManagedEnvironmentCertificateAsync(replacedCertificate.Name);
                    await certificateResource.Value.DeleteAsync(WaitUntil.Completed);
                }                
            }
        }
    }
}

