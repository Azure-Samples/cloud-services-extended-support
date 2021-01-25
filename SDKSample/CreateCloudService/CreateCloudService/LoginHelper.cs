using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;

namespace CreateCloudService
{
    public class LoginHelper : ServiceClientCredentials
    {
        private string AuthToken { get; set; }
        public override void InitializeServiceClient<T>(ServiceClient<T> client)
        {
            string tenantId  = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            string clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            string clientCredentials = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            var authenticationContext = new AuthenticationContext("https://login.windows.net/"+ tenantId);
            var credential = new ClientCredential(clientId: clientId, clientSecret: clientCredentials);

            var result = authenticationContext.AcquireTokenAsync(resource: "https://management.core.windows.net/", clientCredential: credential);

            if (result == null) throw new InvalidOperationException("Failed to obtain the JWT token");

            AuthToken = result.Result.AccessToken;
        }
        public override async Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException("request");

            if (AuthToken == null) throw new InvalidOperationException("Token Provider Cannot Be Null");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //request.Version = new Version(apiVersion);
            await base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}
