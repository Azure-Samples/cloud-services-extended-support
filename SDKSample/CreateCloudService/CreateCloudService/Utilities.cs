using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Azure.Management.Compute;
using Microsoft.Azure.Management.Compute.Models;
using Microsoft.Azure.Management.Network;
using Microsoft.Azure.Management.Network.Models;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.Storage.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text;
using System.IO;
using System.Threading;
using Microsoft.Rest.Azure.Authentication;
using CM = Microsoft.Azure.Management.Compute.Models;
using Microsoft.Rest;

namespace CreateCloudService
{
    public class Utilities
    {
        public string originalLocation;
        public const string MultiRole2Worker1WebRolesPackageSasUri = "TestCloudServiceMultiRole_WorkerRole1(Standard_D2_v2)(1)_WorkerRole2(Standard_D1_v2)(1)_WebRole1(Standard_A2_v2)(2).cspkg";
        public const string MultiRole1Worker1WebRolesPackageSasUri = "TestCloudServiceMultiRole_WorkerRole2(Standard_D2_v2)(1)_WebRole1(Standard_A2_v2)(1).cspkg";
        public const string WebRoleSasUri = "HelloWorldTest_WebRole_D2_V2.cspkg";
        public const string WorkerRoleWithInputEndpointSasUri = "HelloWorldWorker_Standard_D2_v2.cspkg";
        public const string RPType = "Microsoft.Compute/cloudServices";
        protected static string m_location = "westus2";
        protected static ResourceManagementClient m_ResourcesClient;
        protected static ComputeManagementClient m_CrpClient;
        protected static StorageManagementClient m_SrpClient;
        protected static NetworkManagementClient m_NrpClient;
        protected static string m_subId;


        public static Extension CreateExtension(string name, string publisher, string type, string version, string forceUpdateTag = null,
                                                                bool autoUpgrade = false, bool enableAutomaticUpgrade = false, string publicConfig = null, string privateConfig = null, List<string> roleAppliedTo = null)
        {
            return new Extension
            {
                Name = name,
                Properties = new CloudServiceExtensionProperties
                {
                    Publisher = publisher,
                    Type = type,
                    TypeHandlerVersion = version,
                    AutoUpgradeMinorVersion = autoUpgrade,
                    Settings = publicConfig,
                    ProtectedSettings = privateConfig,
                    RolesAppliedTo = roleAppliedTo,
                }
            };
        }

        public static Extension CreateRDPExtension(string name)
        {
            string rdpExtensionPublicConfig = "<PublicConfig>" +
                                                "<UserName>adminRdpTest</UserName>" +
                                                "<Expiration>2021-10-27T23:59:59</Expiration>" +
                                             "</PublicConfig>";
            string rdpExtensionPrivateConfig = "<PrivateConfig>" +
                                                  "<Password>VsmrdpTest!</Password>" +
                                               "</PrivateConfig>";

            return CreateExtension(name, "Microsoft.Windows.Azure.Extensions", "RDP", "1.2.1", autoUpgrade: true,
                                                                                              publicConfig: rdpExtensionPublicConfig,
                                                                                              privateConfig: rdpExtensionPrivateConfig);

        }

        protected static StorageAccount CreateStorageAccount(string rgName, string storageAccountName)
        {
            try
            {
                // Create the resource Group.
                var resourceGroup = m_ResourcesClient.ResourceGroups.CreateOrUpdate(
                    rgName,
                    new ResourceGroup
                    {
                        Location = m_location,
                        Tags = new Dictionary<string, string>() { { rgName, DateTime.UtcNow.ToString("u") } }
                    });

                var stoInput = new StorageAccountCreateParameters
                {
                    Location = m_location,
                    Kind = Microsoft.Azure.Management.Storage.Models.Kind.StorageV2,
                    Sku = new Microsoft.Azure.Management.Storage.Models.Sku(SkuName.StandardRAGRS),
                };

                StorageAccount storageAccountOutput = m_SrpClient.StorageAccounts.Create(rgName,
                    storageAccountName, stoInput);
                bool created = false;
                while (!created)
                {
                    Thread.Sleep(600);
                    var stos = m_SrpClient.StorageAccounts.ListByResourceGroup(rgName);
                    created =
                        stos.Any(
                            t =>
                                StringComparer.OrdinalIgnoreCase.Equals(t.Name, storageAccountName));
                }

                return m_SrpClient.StorageAccounts.GetProperties(rgName, storageAccountName);
            }
            catch
            {
                m_ResourcesClient.ResourceGroups.Delete(rgName);
                throw;
            }
        }

        protected static string CreateCspkgSasUrl(string rgName, string fileName)
        {
            string storageAccountName = "saforcspkg";
            StorageAccount storageAccountOutput = CreateStorageAccount(rgName, storageAccountName); // resource group is also created in this method.
            string applicationMediaLink = "";

            var accountKeyResult = m_SrpClient.StorageAccounts.ListKeysWithHttpMessagesAsync(rgName, storageAccountName).Result;
            CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(storageAccountName, accountKeyResult.Body.Keys.FirstOrDefault().Value), useHttps: true);

            var blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("sascontainer");
            container.CreateIfNotExistsAsync().Wait();

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
            blockBlob.UploadFromFileAsync(Path.Combine(Directory.GetCurrentDirectory(), "Resources", fileName)).Wait();

            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            sasConstraints.SharedAccessStartTime = DateTime.UtcNow.AddDays(-1);
            sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddDays(2);
            sasConstraints.Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write;

            //Generate the shared access signature on the blob, setting the constraints directly on the signature.
            string sasContainerToken = blockBlob.GetSharedAccessSignature(sasConstraints);

            //Return the URI string for the container, including the SAS token.
            applicationMediaLink = blockBlob.Uri + sasContainerToken;
            return applicationMediaLink;
        }

        public static CloudService CreateCloudService_NoAsyncTracking(
            string rgName,
            string csName,
            CloudService cloudService)
        {

            var createOrUpdateResponse = CreateCloudServiceGetOperationResponse(rgName,
                                                                                 csName,
                                                                                 cloudService);
            var getResponse = m_CrpClient.CloudServices.Get(rgName, csName);

            return getResponse;

        }

        protected static void UpdateCloudService(string rgName, string csName, CloudService cloudService)
        {
            var createOrUpdateResponse = m_CrpClient.CloudServices.CreateOrUpdate(rgName, csName, cloudService);
        }

        private static CloudService CreateCloudServiceGetOperationResponse(
            string rgName,
            string csName,
            CloudService cloudService)
        {
            CloudService createOrUpdateResponse = m_CrpClient.CloudServices.CreateOrUpdate(rgName, csName, cloudService);
            return createOrUpdateResponse;
        }
        protected static VirtualNetwork CreateVirtualNetwork(string resourceGroupName, string vnetName, string subnetName)
        {

            VirtualNetwork vnetParams = GenerateVnetModel(vnetName, subnetName);
            return m_NrpClient.VirtualNetworks.CreateOrUpdate(resourceGroupName, vnetName, vnetParams);

        }

        protected static PublicIPAddress CreatePublicIP(string publicIPAddressName, string resourceGroupName, string dnsName)
        {
            PublicIPAddress publicIPAddressParams = GeneratePublicIPAddressModel(publicIPAddressName, dnsName);
            PublicIPAddress publicIpAddress = m_NrpClient.PublicIPAddresses.CreateOrUpdate(resourceGroupName, publicIPAddressName, publicIPAddressParams);
            return publicIpAddress;
        }

        protected static PublicIPAddress GeneratePublicIPAddressModel(string publicIPAddressName, string dnsName)
        {
            PublicIPAddress publicIPAddressParams = new PublicIPAddress(name: publicIPAddressName)
            {
                Location = m_location,
                PublicIPAllocationMethod = IPAllocationMethod.Dynamic,
                DnsSettings = new PublicIPAddressDnsSettings()
                {
                    DomainNameLabel = dnsName
                }
            };

            return publicIPAddressParams;
        }
        protected static CloudService GenerateCloudServiceWithNetworkProfile(string resourceGroupName, string serviceName, string cspkgSasUri, string vnetName, string subnetName, string lbName, string lbFrontendName, Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping, string publicIPAddressName)
        {
            CloudService cloudService = GenerateCloudService(serviceName, cspkgSasUri, vnetName, subnetName, roleNameToPropertiesMapping);
            cloudService.Properties.NetworkProfile = GenerateNrpCloudServiceNetworkProfile(publicIPAddressName, resourceGroupName, lbName, lbFrontendName);
            return cloudService;
        }

        protected static CloudService GenerateCloudService(string serviceName,
            string cspkgSasUri,
            string vnetName,
            string subnetName,
            Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping,
            CloudServiceVaultSecretGroup vaultGroup = null,
            List<ServiceConfigurationRoleCertificate> cscfgCerts = null,
            ServiceConfigurationRoleSecurityConfigurations securityConfigurations = null,
            List<Extension> extensions = null)
        {
            CloudService cloudService = new CloudService
            {

                Properties = new CloudServiceProperties
                {
                    RoleProfile = new CloudServiceRoleProfile()
                    {
                        Roles = GenerateRoles(roleNameToPropertiesMapping)
                    },
                    Configuration = GenerateCscfgWithNetworkConfiguration(serviceName, roleNameToPropertiesMapping, vnetName, subnetName, null, cscfgCerts, securityConfigurations),
                    PackageUrl = cspkgSasUri
                },
                Location = m_location
            };
            if (vaultGroup != null)
            {
                cloudService.Properties.OsProfile =
                    new CloudServiceOsProfile
                    {
                        Secrets = new List<CloudServiceVaultSecretGroup>
                        {
                            vaultGroup
                        }
                    };
            }

            if (extensions != null)
            {
                cloudService.Properties.ExtensionProfile = new CloudServiceExtensionProfile
                {
                    Extensions = extensions
                };
            }
            return cloudService;
        }



        protected static string GenerateCscfgWithNetworkConfiguration(string serviceName,
            Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping,
            string vNetName,
            string subnetName,
            ServiceConfigurationNetworkConfigurationAddressAssignmentsReservedIPs reservedIPs = null,
            List<ServiceConfigurationRoleCertificate> cscfgCerts = null,
            ServiceConfigurationRoleSecurityConfigurations securityConfigurations = null,
            int osFamily = 5,
            Setting[] serviceSettings = null)
        {
            string cscfgPlainText = ServiceConfigurationHelpers.GenerateServiceConfiguration(
                serviceName: serviceName,
                osFamily: osFamily,
                osVersion: "*",
                roleNameToPropertiesMapping: roleNameToPropertiesMapping,
                schemaVersion: "2015-04.2.6",
                vNetName: vNetName,
                subnetName: subnetName,
                reservedIPs: reservedIPs,
                certificates: cscfgCerts,
                securityConfigurations: securityConfigurations,
                serviceSettings: serviceSettings
                );

            return cscfgPlainText;
        }

        protected static List<CloudServiceRoleProfileProperties> GenerateRoles(Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping)
        {
            List<CloudServiceRoleProfileProperties> roles = new List<CloudServiceRoleProfileProperties>();

            foreach (string roleName in roleNameToPropertiesMapping.Keys)
            {
                roles.Add(new CloudServiceRoleProfileProperties()
                {
                    Name = roleName,
                    Sku = new CloudServiceRoleSku
                    {
                        Name = roleNameToPropertiesMapping[roleName].RoleInstanceSize,
                        Capacity = roleNameToPropertiesMapping[roleName].InstanceCount,
                        Tier = roleNameToPropertiesMapping[roleName].RoleInstanceSize.IndexOf("_", StringComparison.InvariantCulture) != -1 ? roleNameToPropertiesMapping[roleName].RoleInstanceSize.Substring(0, roleNameToPropertiesMapping[roleName].RoleInstanceSize.IndexOf("_")) : null
                    }
                });
            }

            return roles;
        }

        protected static CloudServiceNetworkProfile GenerateNrpCloudServiceNetworkProfile(string publicIPAddressName, string resourceGroupName, string lbName, string lbFrontEndName)
        {
            var feipConfig = GenerateFrontEndIpConfigurationModel(publicIPAddressName, resourceGroupName, lbFrontEndName);
            CloudServiceNetworkProfile cloudServiceNetworkProfile = new CloudServiceNetworkProfile()
            {
                LoadBalancerConfigurations = new List<LoadBalancerConfiguration>()
                {
                    new LoadBalancerConfiguration()
                    {
                        Name  = lbName,
                        Properties = new LoadBalancerConfigurationProperties()
                        {
                            FrontendIPConfigurations = new List<LoadBalancerFrontendIPConfiguration>()
                            {
                                feipConfig
                            }
                        }
                    }
                }
            };

            return cloudServiceNetworkProfile;
        }

        protected static LoadBalancerFrontendIPConfiguration GenerateFrontEndIpConfigurationModel(string publicIPAddressName, string resourceGroupName, string lbFrontEndName)
        {
            LoadBalancerFrontendIPConfiguration feipConfiguration =
                new LoadBalancerFrontendIPConfiguration()
                {
                    Name = lbFrontEndName,
                    Properties = new LoadBalancerFrontendIPConfigurationProperties()
                    {
                        PublicIPAddress = new CM.SubResource()
                        {
                            Id = $"/subscriptions/{m_subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{publicIPAddressName}",
                        }
                    }
                };

            return feipConfiguration;
        }

        protected static VirtualNetwork GenerateVnetModel(string vnetName, string subnetName)
        {
            VirtualNetwork vnet = new VirtualNetwork(name: vnetName)
            {
                AddressSpace = new AddressSpace
                {
                    AddressPrefixes = new List<string> { "10.0.0.0/16" }
                },
                Subnets = new List<Subnet>
                {
                    new Subnet(name: subnetName)
                    {
                        AddressPrefix = "10.0.0.0/24"
                    }
                },
                Location = m_location
            };

            return vnet;
        }

        protected static ServiceClientCredentials LoginUsingAAD()
        {
            var appId = "{appId}";
            var clientId = "{aadClientAppId}";
            var clientSecret = "{aadAppkey}";

            var domain = "{aadTenantId}";
            var authEndpoint = "https://login.microsoftonline.com";
            var tokenAudience = "https://api.applicationinsights.io/";

            var adSettings = new ActiveDirectoryServiceSettings
            {
                AuthenticationEndpoint = new Uri(authEndpoint),
                TokenAudience = new Uri(tokenAudience),
                ValidateAuthority = true
            };

            // Authenticate with client secret (app key)
            return ApplicationTokenProvider.LoginSilentAsync(domain, clientId, clientSecret, adSettings).GetAwaiter().GetResult();

        }

        public class RoleConfiguration
        {
            public uint InstanceCount { get; set; }

            public string RoleInstanceSize { get; set; }

            public Dictionary<string, string> Settings { get; set; }
        }

    }
}
