using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Azure.Management.Compute;
using Microsoft.Azure.Management.Compute.Models;
using Microsoft.Azure.Management.Network;
using Microsoft.Azure.Management.Network.Models;
using Microsoft.Azure.Management.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;


namespace CreateCloudService
{
    public class Program: Utilities
    {
        protected static string AdminUsername = "<username>";
        protected static string AdminPassword = "<password>";
        
        static async Task Main(string[] args)
        {
            var creds = new LoginHelper();
            m_subId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            m_ResourcesClient = new ResourceManagementClient(creds);
            m_NrpClient = new NetworkManagementClient(creds);
            m_CrpClient = new ComputeManagementClient(creds);
            m_SrpClient = new StorageManagementClient(creds);
            m_ResourcesClient.SubscriptionId = m_subId;
            m_NrpClient.SubscriptionId = m_subId;
            m_CrpClient.SubscriptionId = m_subId;
            m_SrpClient.SubscriptionId = m_subId;

            // Initialize variable names
            var rgName = "QuickStartRG";
            var resourceGroup = new ResourceGroup(m_location);
            var csName = "ContosoCS";
            string cloudServiceName = "HelloWorldTest_WebRole";
            string publicIPAddressName = "contosoCSpip1";
            string vnetName = "contosocsvnet1";
            string subnetName = "contososubnet1";
            string dnsName = "contosodns1";
            string lbName = "contosolb1";
            string lbfeName = "contosolbfe1";
            string roleInstanceSize = "Standard_D2_v2";

            // Create Resource Group
            Console.WriteLine("--------Start create group--------");
            var resourceGroups = m_ResourcesClient.ResourceGroups;        
            resourceGroup = await resourceGroups.CreateOrUpdateAsync(rgName, resourceGroup);
            Console.WriteLine("--------Finish create group--------");

            // Create Resource Group
            Console.WriteLine("--------Creating Virtual Network--------");
            CreateVirtualNetwork(rgName, vnetName, subnetName);
            Console.WriteLine("--------Finish Virtual Network--------");

            Console.WriteLine("--------Creating Public IP--------");
            PublicIPAddress publicIPAddress = CreatePublicIP(publicIPAddressName, rgName, dnsName);
            Console.WriteLine("--------Finish Public IP--------");

            // Define Configurations to add roles
            Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping = new Dictionary<string, RoleConfiguration>
            {
                { "HelloWorldTest1", new RoleConfiguration { InstanceCount = 1, RoleInstanceSize =  roleInstanceSize} }
            };

            ///
            /// Create: Create 1 WebRole, and RDP Extension.
            ///

            string rdpExtensionPublicConfig = "<PublicConfig>" +
                                                "<UserName>adminRdpTest</UserName>" +
                                                "<Expiration>2021-10-27T23:59:59</Expiration>" +
                                             "</PublicConfig>";
            string rdpExtensionPrivateConfig = "<PrivateConfig>" +
                                                  "<Password>VsmrdpTest!</Password>" +
                                               "</PrivateConfig>";

            Extension rdpExtension = CreateExtension("RDPExtension", "Microsoft.Windows.Azure.Extensions", "RDP", "1.2.1", autoUpgrade: true,
                                                                                              publicConfig: rdpExtensionPublicConfig,
                                                                                              privateConfig: rdpExtensionPrivateConfig);

            // Generate Cloud Service Object
            CloudService cloudService = GenerateCloudServiceWithNetworkProfile(
                resourceGroupName: rgName,
                serviceName: cloudServiceName,
                cspkgSasUri: CreateCspkgSasUrl(rgName, WebRoleSasUri),
                roleNameToPropertiesMapping: roleNameToPropertiesMapping,
                        publicIPAddressName: publicIPAddressName,
                        vnetName: vnetName,
                        subnetName: subnetName,
                        lbName: lbName,
                        lbFrontendName: lbfeName);

            // Add Extension Profile 
            cloudService.Properties.ExtensionProfile = new CloudServiceExtensionProfile()
            {
                Extensions = new List<Extension>()
                {
                    rdpExtension
                }
            };

            // Create Cloud Service
            Console.WriteLine("--------Creating Cloud Service--------");
            CloudService getResponse = CreateCloudService_NoAsyncTracking(
            rgName,
            csName,
            cloudService);
            Console.WriteLine("--------Finish Cloud Service--------");

            Console.WriteLine(getResponse.ToString());

            //Delete resource group 
            Console.WriteLine("--------Start delete group--------");
            await resourceGroups.DeleteAsync(rgName);
            Console.WriteLine("--------Finish delete group--------");
            Console.ReadKey();
        }

    }
}
