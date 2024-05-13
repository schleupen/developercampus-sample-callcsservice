using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using CallCSServiceFromDotNet4.Schleupen.CS.AP.ZD.Personen.PersonEntityService.v3_0;
using CallCSServiceFromDotNet4.Schleupen.CS.PI.SB.Sessions.CreateSessionTokenActivityService.v3_5;

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
			binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
			return binding;
		}
	}

	static class SessionTokenCreator
	{
		internal static string Create(string elementName, string elementTypeName)
		{
			using (CreateSessionTokenActivityServiceClient createSessionTokenClient = new CreateSessionTokenActivityServiceClient(
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
					ExecuteResponse response = createSessionTokenClient.Execute(request);
					return response.SessionToken;
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

			// Create a session tooken for the system structure element we want to work on ...
			// (this corresponds to PS example step 1)
			string sessionToken = SessionTokenCreator.Create("LieferantMoersHuelsdonk1", "Mandant");
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

				try
				{
					// Call the service and print response  ...
					QueryResponse result = ((IPersonEntityService)personEntityServiceClient).Query(request);
					PersonIdentitaetContract person = result.Personen[0].PersonIdentitaeten[0];
					Console.WriteLine("Service called! Response is:");
					Console.WriteLine(person.Vorname + " " + person.Name);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Ups! ... Failed to work with the person service ...");
					Console.WriteLine(ex.ToString());
				}
				finally
				{
					personEntityServiceClient.Close();
				}
			}

			// Wait for user input before exit
			Console.WriteLine("\nPress <Enter> to terminate the client.");
			Console.ReadLine();
			return 0;
		}
	}
}