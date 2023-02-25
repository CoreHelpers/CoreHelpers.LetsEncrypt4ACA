using System;
using Cert.Services;
using Certes.Acme;

namespace CoreHelpers.LetsEncrypt4ACA.Runner.Services
{
	public class DomainUpdateService
	{
        private string _tenantId;
        private string _clientId;
        private string _clientSecret;
        private string _subscriptionId;
        private string _containerAppRg;
        private string _containerAppEnvironment;
        private string _targetDomain;
        private nAcmeEnvironment _acmeEnvironment;
        private string _accountName;
        private string _dn;

        public DomainUpdateService(string tenantId, string clientId, string clientSecret, string subscriptionId, string containerAppRg, string containerAppEnvironment, string targetDomain, string accountName, string dn, nAcmeEnvironment acmeEnvironment)
		{
            _tenantId = tenantId;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _subscriptionId = subscriptionId;
            _containerAppRg = containerAppRg;
            _containerAppEnvironment = containerAppEnvironment;
            _targetDomain = targetDomain;
            _acmeEnvironment = acmeEnvironment;
            _accountName = accountName;
            _dn = dn;
		}

		public async Task PerformUpdate()
		{
            // get the containerApp Service
            var containerAppService = new AureContainerAppService(_tenantId, _clientId, _clientSecret, _subscriptionId, _containerAppRg, _containerAppEnvironment);

            // get the acme service
            var acmeService = new AcmeService(_targetDomain, _accountName, _dn, _acmeEnvironment);

            // get the dns service
            var dnsService = new AzureDnsService(_tenantId, _clientId, _clientSecret, _subscriptionId, _targetDomain);

            // get the dns client
            var dnsClientService = new DnsClientService(_targetDomain);

            // check that we have a dns zone for the target domain
            Console.WriteLine("Verifying of a Azure DNS zone exists for the domain...");
            var dnsZone = await dnsService.VerifyZoneForDomain();
            if (dnsZone == null)
            {
                Console.WriteLine($"No DNS zone for domain {_targetDomain} available, ignoring");
                return;
            }

            // initialize the service
            Console.WriteLine("Initializing the ACME Let's encrypt account");
            await acmeService.InitializeAccount();

            // create a certificate order
            Console.WriteLine("Creating a certificate order...");
            var orderTuple = await acmeService.CreateCertificateOrder();

            // create the DNS challange
            var dnsChallengeTexts = new List<string>();
            var dnsChallengeContext = new List<IChallengeContext>();

            Console.WriteLine("Updating Azure DNS with dns-1 challanges...");
            foreach (var auth in orderTuple.Item2)
            {
                var dnsChallenge = await acmeService.GetDnsChallenge(auth);
                dnsChallengeTexts.Add(dnsChallenge.Item1);
                dnsChallengeContext.Add(dnsChallenge.Item2);
                Console.WriteLine($"DNS-Challenge: {dnsChallenge.Item1}");
            }

            // registere the dns challenges
            Console.WriteLine("Performing update...");
            await dnsService.UpdateChallenges(dnsChallengeTexts, dnsZone);

            // wait until the dns responses correctly
            Console.WriteLine("Waiting until the Azure DNS zone is responding correctly...");
            if (!await dnsClientService.CheckDnsChallenge(dnsChallengeTexts))
            {
                Console.WriteLine("Error: Failed to get the dns repsonses");
                return;
            }

            // we are ready to validate
            Console.WriteLine("Validating the Let's encrypt challenges...");
            await acmeService.Validate(dnsChallengeContext, orderTuple);

            // issue the certificate
            Console.WriteLine("Issuing a new certificate...");
            var certificateData = await acmeService.IsserCertificate(orderTuple);

            // upload
            Console.WriteLine("Uploading the certificate to the managed environment...");
            var certificateResource = await containerAppService.UploadCertificate(_targetDomain, certificateData, _targetDomain);

            // update the container app custom domains
            Console.WriteLine("Updating the custom domains in the container apps");
            var replacedCertificates = await containerAppService.UpdateCustomDomains(_targetDomain, certificateResource);

            Console.WriteLine($"Removing #{replacedCertificates.Count()} replaced certificates");
            await containerAppService.DeleteReplacedCertificates(replacedCertificates);
        }
	}
}

