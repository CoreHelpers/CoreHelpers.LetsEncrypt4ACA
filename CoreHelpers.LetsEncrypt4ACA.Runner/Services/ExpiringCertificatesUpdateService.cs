using System;
using Cert.Services;

namespace CoreHelpers.LetsEncrypt4ACA.Runner.Services
{
    public class ExpiringCertificatesUpdateService
    {
        private string _tenantId;
        private string _clientId;
        private string _clientSecret;
        private string _subscriptionId;
        private string _containerAppRg;
        private string _containerAppEnvironment;

        private int _expiringDays;
        private nAcmeEnvironment _acmeEnvironment;
        private string _accountName;
        private string _dn;

        public ExpiringCertificatesUpdateService(string tenantId, string clientId, string clientSecret, string subscriptionId, string containerAppRg, string containerAppEnvironment, string accountName, string dn, int expiringDays, nAcmeEnvironment acmeEnvironment)
        {
            _tenantId = tenantId;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _subscriptionId = subscriptionId;
            _containerAppRg = containerAppRg;
            _containerAppEnvironment = containerAppEnvironment;
            _expiringDays = expiringDays;
            _acmeEnvironment = acmeEnvironment;
            _accountName = accountName;
            _dn = dn;
        }

        public async Task PerformUpdate()
        {
            // get the containerApp Service
            var containerAppService = new AureContainerAppService(_tenantId, _clientId, _clientSecret, _subscriptionId, _containerAppRg, _containerAppEnvironment);

            // find all the certificates expiring during the next 30 days
            Console.WriteLine($"Receiving certificates expiring within {_expiringDays} days...");
            var expiringCertificates = await containerAppService.GetExpiringCertificates(_expiringDays);

            // visit every certificate
            Console.WriteLine($"Visiting {expiringCertificates.Count()} certificates...");
            foreach (var expiringCertificate in expiringCertificates)
            {
                // Domain we are working for
                var domain = expiringCertificate.Data.Properties.SubjectName.Replace("CN=", "");

                // get an instance of the update service
                Console.WriteLine($"Renewing certificate {expiringCertificate.Data.Name} for domain {domain}");
                var domainUpdateService = new DomainUpdateService(_tenantId, _clientId, _clientSecret, _subscriptionId, _containerAppRg, _containerAppEnvironment, domain, _accountName, _dn, _acmeEnvironment);

                // exceute the update
                await domainUpdateService.PerformUpdate();                
            }
        }
    }
}

