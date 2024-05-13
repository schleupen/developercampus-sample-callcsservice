using System;
using System.Collections.Generic;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text.RegularExpressions;
using CallCSServiceFromDotNet4Oidc.Schleupen.CS.PI.SB.Sessions.CreateSessionTokenActivityService.v3_5;
using CallCSServiceFromDotNet4Oidc.Schleupen.CS.AP.ZD.Personen.PersonEntityService.v3_0;

namespace CallCSServiceFromDotNet4
{
	static class WcfConfiguration
	{
		private const string Host = "TODO-HOST";

		internal static EndpointAddress CreateEndpointAddress()
		{
			var uriBuilder = new UriBuilder(Uri.UriSchemeHttps, Host)
			{
				Path = "Schleupen/Service Bus/Broker/BrokerService.svc"
			};

			return new EndpointAddress(uriBuilder.Uri.ToString());
		}

		internal static Binding CreateHttpsBinding()
		{
			var binding = new BasicHttpsBinding(BasicHttpsSecurityMode.Transport);
			binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;

			return binding;
		}
	}

	static class OidcProviderClient
	{
		private const string OpenIdProviderUri = "TODO-OPENIDPROVIDER-URI";
		private const string OidcClientId = "TODO-OIDC-CLIENTID";
		private const string OidcClientSecret = "TODO-OIDC-CLIENTSECRET";

		public static string GetAccessToken()
		{
			var httpClient = new HttpClient();
			var nameValueCollection = new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("client_id", OidcClientId),
				new KeyValuePair<string, string>("client_secret", OidcClientSecret),
				new KeyValuePair<string, string>("grant_type", "client_credentials")
			};

			HttpResponseMessage response = httpClient.PostAsync(new Uri(OpenIdProviderUri), new FormUrlEncodedContent(nameValueCollection)).GetAwaiter().GetResult();
			string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			return Regex.Match(responseContent, "({.*\"access_token\":\")(.*)(\",\"expires_in\".*})").Groups[2].Value;
		}
	}

	static class OperationContextScopeExtensions
	{
		public static void AddBearerTokenToHttpHeader(this OperationContextScope _, string accessToken)
		{
			var requestMessage = new HttpRequestMessageProperty();
			requestMessage.Headers["Authorization"] = $"Bearer {accessToken}";
			OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = requestMessage;
		}
	}

	static class SessionTokenCreator
	{
		internal static string Create(string elementName, string elementTypeName, string accessToken)
		{
			using (var createSessionTokenClient = new CreateSessionTokenActivityServiceClient(
					   WcfConfiguration.CreateHttpsBinding(),
					   WcfConfiguration.CreateEndpointAddress()))
			{
				try
				{
					var elementQualification = new ElementByNameQualificationContract();
					elementQualification.ElementName = elementName;
					elementQualification.ElementTypeName = elementTypeName;
					elementQualification.ViewName = "Standard";

					var request = new ExecuteRequest(false, elementQualification, null, null);

					using (var scope = new OperationContextScope(createSessionTokenClient.InnerChannel))
					{
						scope.AddBearerTokenToHttpHeader(accessToken);
						ExecuteResponse response = createSessionTokenClient.Execute(request);
						return response.SessionToken;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error: Failed to get a session token ...");
					Console.WriteLine(ex.ToString());
					return null;
				}
				finally
				{
					createSessionTokenClient.Close();
				}
			}
		}
	}

	class Program
	{
		static int Main(string[] args)
		{
			Console.WriteLine("Hello CS!");
			Console.WriteLine("Demonstrates how to call a CS Service via the CS-Broker");

			// Create access token for authentication per OIDC ...
			string accessToken = OidcProviderClient.GetAccessToken();
			if (string.IsNullOrEmpty(accessToken))
			{
				Console.WriteLine("Error: Failed to get an access token ...");
				return -1;
			}

			Console.WriteLine("\nAccessToken obtained ...");

			// Create a session tooken for the system structure element we want to work on ...
			// (this corresponds to PS example step 1)
			string sessionToken = SessionTokenCreator.Create("LieferantMoersHuelsdonk1", "Mandant", accessToken);
			if (sessionToken == null)
			{
				return -1;
			}

			Console.WriteLine("\nSession Token created ...");

			// Select the service to call and create a client ...
			// (this corresponds to PS example step 2) 
			using (var personEntityServiceClient = new PersonEntityServiceClient(
						WcfConfiguration.CreateHttpsBinding(),
						WcfConfiguration.CreateEndpointAddress()))
			{
				Console.WriteLine("\nPersonEntityServiceClient created ...");

				// Fill parameters for calling the operation (request)  ...
				// (this corresponds to PS example step 2)
				var request = new QueryRequest();
				request.SessionToken = sessionToken;
				request.Details = new List<PersonDetailContract>();
				request.Details.Add(PersonDetailContract.PersonIdentitaetAktuell);

				var criteria = new PersonSimpleCriteriaContract();
				criteria.SearchValue = "Schmitt";
				request.Criteria = criteria;

				Console.WriteLine("\nRequest object created ...");

				using (var scope = new OperationContextScope(personEntityServiceClient.InnerChannel))
				{
					scope.AddBearerTokenToHttpHeader(accessToken);

					// Call the service and print response  ...
					QueryResponse result = ((IPersonEntityService)personEntityServiceClient).Query(request);
					PersonIdentitaetContract person = result.Personen[0].PersonIdentitaeten[0];

					Console.WriteLine("Service called! Response is:");
					Console.WriteLine(person.Vorname + " " + person.Name);
				}
			}

			// Wait for user input before exit
			Console.WriteLine("\nPress <Enter> to terminate the client.");
			Console.ReadLine();
			return 0;
		}
	}
}