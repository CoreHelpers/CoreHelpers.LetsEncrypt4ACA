using System;
using DnsClient;

namespace Cert.Services
{
	public class DnsClientService
	{
        private readonly LookupClient _lookupClient;
        private string _targetDomain;

        public DnsClientService(string targetDomain)
		{
            _lookupClient = new LookupClient();
            _targetDomain = targetDomain;
		}

        public async Task<bool> CheckDnsChallenge(IEnumerable<string> dnsChallenges)
        {
            var result = false;
            var counter = 0;

            while(!result && counter <= 300)
            {
                counter++;

                // give the caches 10 seconds
                await Task.Delay(10000);

                // check 
                result = await CheckDnsChallengeInternal(dnsChallenges);
            }

            return result;
        }

        public async Task<bool> CheckDnsChallengeInternal(IEnumerable<string> dnsChallenges)
        {
            IDnsQueryResponse queryResult;

            try
            {
                queryResult = await _lookupClient.QueryAsync($"_acme-challenge.{_targetDomain}", QueryType.TXT);
            }
            catch (DnsResponseException ex)
            {
                return false;
            }

            var txtRecords = queryResult.Answers
                                        .OfType<DnsClient.Protocol.TxtRecord>()
                                        .ToArray();

            if (txtRecords.Length == 0)
            {
                return false;
            }

            foreach (var challenge in dnsChallenges)
            {
                if (!txtRecords.Any(x => x.Text.Contains(challenge)))
                {
                    return false;
                }                
            }

            return true;
        }
    }
}

