using System;
using Azure;
using System.Linq;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

namespace Cert.Services
{
	public class AzureDnsService
	{
		private string _targetDomain;
        private TokenCredential _tokenCredential;
		private ResourceIdentifier _subscriptionResourceId;

        public AzureDnsService(string tenantId, string clientId, string clientSecret, string subscriptionId, string targetDomain)
		{
			_targetDomain = targetDomain;
            _tokenCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
			_subscriptionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}");
        }

		public async Task<DnsZoneResource?> VerifyZoneForDomain()
		{
			// build the arm client
			var client = new ArmClient(_tokenCredential);
			{
				var subscription = await client.GetSubscriptionResource(_subscriptionResourceId).GetAsync();
				if (subscription == null || subscription.Value == null)
					return null;

				var dnsZones = subscription.Value.GetDnsZonesAsync();
                
                await foreach (var dnsZone in dnsZones)
                {
					if (dnsZone.HasData && _targetDomain.ToLower().EndsWith(dnsZone.Data.Name.ToLower()))
						return dnsZone;					
                }
            }

			// at this point we did not find any dns zone
			return null;
		}

		
		public async Task UpdateChallenges(IEnumerable<string> dnsChallenges, DnsZoneResource dnsZoneResource)
		{
			// define the acm-challenge name
			var neededRecordName = $"_acme-challenge.{_targetDomain}";
			var indexOfPostFix = neededRecordName.IndexOf(dnsZoneResource.Data.Name);
			if (indexOfPostFix == -1)
				throw new Exception("Base Domain not found");

			neededRecordName = neededRecordName.Substring(0, indexOfPostFix).TrimEnd('.');

            // delete the old record set if exists            
            var recordSet = await FindTxtRecord(dnsZoneResource, neededRecordName);
            if (recordSet != null)
                await recordSet.DeleteAsync(WaitUntil.Completed);

            // create a new recird set
            var newRecordSet = new DnsTxtRecordData();
            newRecordSet.TtlInSeconds = 60;

			foreach(var value in dnsChallenges)
				newRecordSet.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { value } });

            var recordSets = dnsZoneResource.GetDnsTxtRecords();
            await recordSets.CreateOrUpdateAsync(WaitUntil.Completed, neededRecordName, newRecordSet);
        }

		private async Task<DnsTxtRecordResource?> FindTxtRecord(DnsZoneResource dnsZoneResource, string name)
		{
			try
			{
				var response = await dnsZoneResource.GetDnsTxtRecordAsync(name);
				return response.Value;
			} catch (Azure.RequestFailedException e)
			{
				if (e.Status == 404)
					return null;

				throw e;
			}
        }
	}
}

