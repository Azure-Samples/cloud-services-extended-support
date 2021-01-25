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
            var creds = LoginUsingAAD();
            m_subId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            m_ResourcesClient = new ResourceManagementClient(creds);
            m_NrpClient = new NetworkManagementClient(creds);
            m_CrpClient = new ComputeManagementClient(creds);
            m_SrpClient = new StorageManagementClient(creds);

            // Create Resource Group
            Console.WriteLine("--------Start create group--------");
            var resourceGroups = m_ResourcesClient.ResourceGroups;
            
            var rgName = "QuickStartRG";
            var resourceGroup = new ResourceGroup(m_location);
            var csName = "ContosoCS";
            string cloudServiceName = "HelloWorldTest_WebRole";
            string publicIPAddressName = "ContosoCSpip";
            string vnetName = "Contosocsvnet";
            string subnetName = "Contososubnet";
            string dnsName = "Contosodns";
            string lbName = "Contosolb";
            string lbfeName = "Contosolbfe";
            string roleInstanceSize = "Standard_D2_v2";
            resourceGroup = await resourceGroups.CreateOrUpdateAsync(rgName, resourceGroup);
            Console.WriteLine("--------Finish create group--------");

            CreateVirtualNetwork(rgName, vnetName, subnetName);
            PublicIPAddress publicIPAddress = CreatePublicIP(publicIPAddressName, rgName, dnsName);

            // Define Configurations
            Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping = new Dictionary<string, RoleConfiguration>
            {
                { "HelloWorldTest1", new RoleConfiguration { InstanceCount = 1, RoleInstanceSize =  roleInstanceSize} }
            };

            ///
            /// Create: Create a multi-role CloudService with 2 WorkerRoles, 1 WebRole, and RDP Extension.
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

            cloudService.Properties.ExtensionProfile = new CloudServiceExtensionProfile()
            {
                Extensions = new List<Extension>()
            };
            cloudService.Properties.ExtensionProfile.Extensions.Add(rdpExtension);
            CloudService getResponse = CreateCloudService_NoAsyncTracking(
            rgName,
            csName,
            cloudService);

            // Delete resource group if necessary
            //Console.WriteLine("--------Start delete group--------");
            // await (await resourceGroups.BeginDeleteAsync(rgName)).WaitForCompletionAsync();
            //Console.WriteLine("--------Finish delete group--------");
            //Console.ReadKey();
        }

    }
}
