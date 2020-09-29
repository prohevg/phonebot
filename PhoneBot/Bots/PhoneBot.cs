// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using PhoneBot.BusinessLogic;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneBot
{
    public class PhoneBot : ActivityHandler
    {
        #region fields

        private readonly ILogger<PhoneBot> _logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        public PhoneBot(ILogger<PhoneBot> logger)
        {
            _logger = logger;
        }

        #endregion

        #region override 

        protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            try
            {
                var members = await TeamsInfo.GetMembersAsync(turnContext, cancellationToken);

                if (members.Count() != 2)
                {
                    return await SendErrorActivityAsync(turnContext, Resource.ErrorUserCountinChat);
                }

                var config = AuthenticationConfig.ReadFromJsonFile("appsettings.json");
                var result = await GetToken(turnContext.Activity.Conversation.TenantId, config);

                if (result == null || string.IsNullOrEmpty(result.AccessToken))
                {
                    return await SendErrorActivityAsync(turnContext, Resource.ErrorGetToken);
                }

                var user1 = members.ElementAt(0);
                var user2 = members.ElementAt(1);

                var simpleGraphClient = new SimpleGraphClient(config, result.AccessToken);
                var userPhone1 = await simpleGraphClient.GetUserPhoneAsync(user1.AadObjectId);
                var userPhone2 = await simpleGraphClient.GetUserPhoneAsync(user2.AadObjectId);

                if (string.IsNullOrEmpty(userPhone1))
                {
                    return await SendErrorActivityAsync(turnContext, string.Format(Resource.ErrorUserPhoneMissing, user1.Name));
                }

                if (string.IsNullOrEmpty(userPhone2))
                {
                    return await SendErrorActivityAsync(turnContext, string.Format(Resource.ErrorUserPhoneMissing, user2.Name));
                }

                await CallUp(config, userPhone1, userPhone2, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                await turnContext.SendActivityAsync($"Sorry(:");
                await turnContext.SendActivityAsync(e.Message);
            }

            return await base.OnInvokeActivityAsync(turnContext, cancellationToken);
        }

        /// <summary>
        /// Autorization and get token
        /// </summary>        
        private async Task<AuthenticationResult> GetToken(string tenantId, AuthenticationConfig config)
        {
            var app = ConfidentialClientApplicationBuilder.Create(config.MicrosoftAppId)
                    .WithClientSecret(config.MicrosoftAppPassword)
                    .WithAuthority(new Uri(config.Authority(tenantId)))
                    .Build();

            var scopes = new string[] { $"{config.ApiUrl}.default" };

            return await app
                .AcquireTokenForClient(scopes)
                .ExecuteAsync();
        }

        /// <summary>
        /// Send error message to chat
        /// </summary>
        private async Task<InvokeResponse> SendErrorActivityAsync(ITurnContext<IInvokeActivity> turnContext, string message)
        {
            await turnContext.SendActivityAsync(message);
            
            return new InvokeResponse
            {
                Body = new ResourceResponse(turnContext.Activity.Id ?? string.Empty),
                Status = (int)HttpStatusCode.OK
            };
        }

        #endregion

        #region private 

        /// <summary>
        /// Call up to user
        /// </summary>
        /// <param name="config">Application configuration</param>
        /// <param name="from">User 1 phone number</param>
        /// <param name="to">User 2 phone number</param>
        /// <param name="cancellationToken">Token</param>
        private async Task CallUp(AuthenticationConfig config, string from, string to, CancellationToken cancellationToken)
        {
            using (var httpClient = new HttpClient())
            {
                var uri = string.Format(config.AstrericsApp, GetNumber(from), GetNumber(to));

                _logger.LogDebug($"trying to call: {uri}");
                
                var result = await httpClient.GetAsync(uri, cancellationToken);
                if (!result.IsSuccessStatusCode)
                {
                    var content = await result.Content.ReadAsStringAsync();

                    _logger.LogDebug($"Error: {uri}, Message: {content}");

                    throw new Exception(content);
                }
            }
        }

        /// <summary>
        /// Take only last 5 digit in phone number
        /// </summary>
        /// <param name="number">Phone number</param>
        /// <returns>Phone number for url</returns>
        private string GetNumber(string number)
        {
            if (string.IsNullOrEmpty(number) || number.Length < 5)
            {
                return number;
            }

            number = number.Replace("-", "");
            return new string(number.TakeLast(5).ToArray());
        }

        #endregion
    }
}
