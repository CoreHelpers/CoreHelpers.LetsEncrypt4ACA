using System.Security.AccessControl;
using Cert.Services;
using CoreHelpers.LetsEncrypt4ACA.Runner.Helpers;
using CoreHelpers.LetsEncrypt4ACA.Runner.Services;

// check all the mandatory and optional parameters - ACME Protocol
var accountName = EnvironmentHelpers.ValidateParameter(EnvironmentParameters.ACME_ACCOUNT);

// Country/State/Locality/Organization/OrgUnit
var certificateDn = EnvironmentHelpers.ValidateParameter(EnvironmentParameters.ACME_DN); 
var optionalLetsEncryptEnvironment = Enum.Parse<nAcmeEnvironment>(EnvironmentHelpers.ReadOptionalParameter(EnvironmentParameters.ACME_ENVIRONMENT, "Production"));

// check all the mandatory and optional parameters - ARM Access 
var tenantId = EnvironmentHelpers.ValidateParameter(EnvironmentParameters.ARM_TENANTID);
var clientId = EnvironmentHelpers.ValidateParameter(EnvironmentParameters.ARM_CLIENTID);
var clientSecret = EnvironmentHelpers.ValidateParameter(EnvironmentParameters.ARM_CLIENTSECRET);
var subscriptionId = EnvironmentHelpers.ValidateParameter(EnvironmentParameters.ARM_SUBSCRIPTIONID);

// check all the mandatory and optionalparameters - Container App details
var containerAppRg = EnvironmentHelpers.ValidateParameter(EnvironmentParameters.ACA_RG);
var containerAppEnvironment = EnvironmentHelpers.ValidateParameter(EnvironmentParameters.ACA_APPENV);
var optionalExpiringDays = Convert.ToInt32(EnvironmentHelpers.ReadOptionalParameter(EnvironmentParameters.ACA_EXPIRING_DAYS, "30"));

// check if we need to create a certificate
var optionalCreateDomain = EnvironmentHelpers.ReadOptionalParameter(EnvironmentParameters.ACA_CREATE_FOR_DOMAIN, "");

// execute the operation
if (!String.IsNullOrEmpty(optionalCreateDomain))
{  
    // get the update service
    Console.WriteLine("Initializing the certificate creation service");
    var certificateCreationService = new CertificateInitializingService(tenantId, clientId, clientSecret, subscriptionId, containerAppRg, containerAppEnvironment, accountName, certificateDn, optionalExpiringDays, optionalLetsEncryptEnvironment);

    // execute the update
    Console.WriteLine("Performing Certificate creation");
    await certificateCreationService.PerformCreation(optionalCreateDomain);
}
else
{
    // get the update service
    Console.WriteLine("Initializing the expiring certificaes update service");
    var expiringCertificatesUpdateService = new ExpiringCertificatesUpdateService(tenantId, clientId, clientSecret, subscriptionId, containerAppRg, containerAppEnvironment, accountName, certificateDn, optionalExpiringDays, optionalLetsEncryptEnvironment);

    // execute the update
    Console.WriteLine("Performing Certificate updates");
    await expiringCertificatesUpdateService.PerformUpdate();
}

