using Microsoft.Graph;
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace PhoneBot.BusinessLogic
{
    /// <summary>
    /// Wrapper for the Microsoft Graph API
    /// </summary>
    public class SimpleGraphClient
    {
        #region fields

        private readonly AuthenticationConfig _config;
        private readonly string _token;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Application configuration</param>
        /// <param name="token">Token</param>
        public SimpleGraphClient(AuthenticationConfig config, string token)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            _config = config;
            _token = token;
        }

        #endregion

        #region public

        /// <summary>
        /// Gets the user's phone
        /// </summary>
        /// <param name="aadUser">Azure AD identficator</param>
        /// <returns>First phone number</returns>
        public async Task<string> GetUserPhoneAsync(string aadUser)
        {
            var graphClient = GetAuthenticatedClient();
            var user = await graphClient.Users[aadUser]
                    .Request()
                    .GetAsync();

            return user != null 
                ? user.BusinessPhones.FirstOrDefault()
                : null;
        }
        
        #endregion

        #region private

        /// <summary>
        /// Get an Authenticated Microsoft Graph client using the token issued to the user.
        /// </summary>
        private GraphServiceClient GetAuthenticatedClient()
        {
            var graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    requestMessage =>
                    {
                        // Append the access token to the request.
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", _token);

                        // Get event times in the current time zone.
                        requestMessage.Headers.Add("Prefer", "outlook.timezone=\"" + TimeZoneInfo.Local.Id + "\"");

                        return Task.CompletedTask;
                    }));
            return graphClient;
        }

        #endregion
    }
}
