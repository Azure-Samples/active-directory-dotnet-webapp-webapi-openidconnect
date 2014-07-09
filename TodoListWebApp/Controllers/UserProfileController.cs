using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using System.Threading.Tasks;
using TodoListWebApp.Models;
using System.Security.Claims;
using Microsoft.Owin.Security.OpenIdConnect;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using TodoListWebApp.Utils;
using System.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace TodoListWebApp.Controllers
{
    [Authorize]
    public class UserProfileController : Controller
    {
        private string graphResourceId = "https://graph.windows.net";
        private string graphUserUrl = "https://graph.windows.net/{0}/me?api-version=2013-11-08";
        private const string TenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string appKey = ConfigurationManager.AppSettings["ida:AppKey"];

        //
        // GET: /UserProfile/
        public async Task<ActionResult> Index()
        {
            //
            // Retrieve the user's name, tenantID, and access token since they are parameters used to query the Graph API.
            //
            UserProfile profile;
            AuthenticationResult result = null;

            try
            {
                string tenantId = ClaimsPrincipal.Current.FindFirst(TenantIdClaimType).Value;
                string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
                AuthenticationContext authContext = new AuthenticationContext(Startup.Authority, new NaiveSessionCache(userObjectID));
                ClientCredential credential = new ClientCredential(clientId, appKey);
                result = authContext.AcquireTokenSilent(graphResourceId, credential, new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));

                //
                // Call the Graph API and retrieve the user's profile.
                //
                string requestUrl = String.Format(
                    CultureInfo.InvariantCulture,
                    graphUserUrl,
                    HttpUtility.UrlEncode(tenantId));
                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                HttpResponseMessage response = await client.SendAsync(request);

                //
                // Return the user's profile in the view.
                //
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    profile = JsonConvert.DeserializeObject<UserProfile>(responseString);
                }
                else
                {
                    //
                    // If the call failed, then drop the current access token and show the user an error indicating they might need to sign-in again.
                    //
                    var todoTokens = authContext.TokenCache.ReadItems().Where(a => a.Resource == graphResourceId);
                    foreach (TokenCacheItem tci in todoTokens)
                        authContext.TokenCache.DeleteItem(tci);

                    profile = new UserProfile();
                    profile.DisplayName = " ";
                    profile.GivenName = " ";
                    profile.Surname = " ";
                    ViewBag.ErrorMessage = "UnexpectedError";
                }

                return View(profile);
            }
            catch (Exception ee)
            {
                //
                // If the user doesn't have an access token, they need to re-authorize.
                //

                //
                // If refresh is set to true, the user has clicked the link to be authorized again.
                //
                if (Request.QueryString["reauth"] == "True")
                {
                    //
                    // Send an OpenID Connect sign-in request to get a new set of tokens.
                    // If the user still has a valid session with Azure AD, they will not be prompted for their credentials.
                    // The OpenID Connect middleware will return to this controller after the sign-in response has been handled.
                    //
                    HttpContext.GetOwinContext().Authentication.Challenge(OpenIdConnectAuthenticationDefaults.AuthenticationType);
                }

                //
                // The user needs to re-authorize.  Show them a message to that effect.
                //
                profile = new UserProfile();
                profile.DisplayName = " ";
                profile.GivenName = " ";
                profile.Surname = " ";
                ViewBag.ErrorMessage = "AuthorizationRequired";

                return View(profile);

            }
        }
    }
}