using System;
namespace CoreHelpers.LetsEncrypt4ACA.Runner.Helpers
{
	public static class EnvironmentParameters
	{
		public static string ARM_TENANTID = "ARM_TENANTID";
        public static string ARM_CLIENTID = "ARM_CLIENTID";
        public static string ARM_CLIENTSECRET = "ARM_CLIENTSECRET";
        public static string ARM_SUBSCRIPTIONID = "ARM_SUBSCRIPTIONID";

        public static string ACME_ENVIRONMENT = "ACME_ENVIRONMENT";
        public static string ACME_ACCOUNT = "ACME_ACCOUNT";
        public static string ACME_DN = "ACME_DN";

        public static string ACA_RG = "ACA_RG";
        public static string ACA_APPENV = "ACA_APPENV";
        public static string ACA_EXPIRING_DAYS = "ACA_EXPIRING_DAYS";

        

    }
}

