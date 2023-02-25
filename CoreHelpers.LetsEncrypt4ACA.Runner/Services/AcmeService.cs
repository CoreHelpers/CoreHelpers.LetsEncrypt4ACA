using System;
using Cert.Extensions;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Org.BouncyCastle.Asn1.X500;

namespace Cert.Services
{
    public enum nAcmeEnvironment
    {
        Production,
        Staging
    }

	public class AcmeService
	{
        private AcmeContext _context;
        private string _targetDomain;

        private string _accountName;
        private string _dn;

        public AcmeService(string targetDomain, string accountName, string dn, nAcmeEnvironment acmeEnvironment = nAcmeEnvironment.Production)
        {
            _targetDomain = targetDomain;
            _accountName = accountName;
            _dn = dn;
            _context = new AcmeContext(acmeEnvironment == nAcmeEnvironment.Production ? WellKnownServers.LetsEncryptV2 : WellKnownServers.LetsEncryptStagingV2);
            Console.WriteLine($"Using Let's-Encryption Environment: {acmeEnvironment.ToString()}");
            Console.WriteLine($"Base-Account is: {accountName}");
        }

		public async Task InitializeAccount()
		{
            // generate the acocunt name
            var accountName = $"{_accountName.Split("@")[0]}+{_targetDomain}@{_accountName.Split("@")[1]}";
            
            // create the account
            await _context.NewAccount(accountName, true);            
        }

        public async Task<Tuple<IOrderContext, IEnumerable<IAuthorizationContext>>> CreateCertificateOrder()
        {
            // oreder a new certificate
            var order = await _context.NewOrder(new[] { _targetDomain, $"*.{_targetDomain}" });

            // generate
            var authz = new List<IAuthorizationContext>();
            foreach(var auth in await order.Authorizations())                            
                authz.Add(auth);                            
            
            return new Tuple<IOrderContext, IEnumerable<IAuthorizationContext>>(order, authz);
        }

        public async Task Validate(IEnumerable<IChallengeContext> challenges, Tuple<IOrderContext, IEnumerable<IAuthorizationContext>> context)
        {
            // let the server know we are ready for validation
            foreach (var challenge in challenges)
            {                
                // announce validation
                await challenge.Validate();
            }            

            // wait until the validation is done
            foreach (var auth in context.Item2)
            {
                // get the deatils
                var authdetail = await auth.Resource();

                while (authdetail.Status != AuthorizationStatus.Valid)
                {
                    await Task.Delay(5000);
                    authdetail = await auth.Resource();

                    if (authdetail.Status != AuthorizationStatus.Valid && authdetail.Status != AuthorizationStatus.Pending)                        
                        throw new Exception($"Validation failed with status {authdetail.Status}");
                }
            }
        }

        public async Task<byte[]> IsserCertificate(Tuple<IOrderContext, IEnumerable<IAuthorizationContext>> context)
        {
            // split the dn
            var splittedDn = _dn.Split('/');
            if (splittedDn.Count() != 5)
                throw new Exception("DN has an invalid format, use Country/State/Locality/Organization/OrgUnit");

            // generate the cert
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await context.Item1.Generate(new CsrInfo
            {
                CountryName = splittedDn[0],
                State = splittedDn[1],
                Locality = splittedDn[2],               
                Organization = splittedDn[3],
                OrganizationUnit = splittedDn[4],
                CommonName = _targetDomain,
            }, privateKey);

            // export to pfx
            var pfxBuilder = cert.ToPfx(privateKey);
            pfxBuilder.AddStagingIssuer();
            return pfxBuilder.Build(_targetDomain, _targetDomain);
        }

        public async Task<Tuple<string, IChallengeContext>> GetDnsChallenge(IAuthorizationContext auth)
        {
            var challenge = await auth.Dns();
            var dnsText =_context.AccountKey.DnsTxt(challenge.Token);
            return new Tuple<string, IChallengeContext>(dnsText, challenge);
        }
    }
}

