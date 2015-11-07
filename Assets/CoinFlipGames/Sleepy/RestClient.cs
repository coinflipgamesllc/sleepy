using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CoinFlipGames.Sleepy
{
	public class RestClient 
	{
		private enum RequestMethod 
		{
			GET,
			POST,
			PUT,
			DELETE
		}

		private class Route
		{
			public string Name { get; private set; }
			public RequestMethod Method { get; private set; }
			public string Path { get; private set; }
			public bool IsSigned { get; private set; }
			public bool IsAsync { get; private set; }
			
			internal Route (string name, RequestMethod method, string path, bool isSigned, bool isAsync)
			{
				this.Name = name;
				this.Method = method;
				this.Path = path;
				this.IsSigned = isSigned;
				this.IsAsync = isAsync;
			}
			
			internal string ReplaceParams (Dictionary<string, string> replacements)
			{
				string path = this.Path;
				foreach(KeyValuePair<string, string> entry in replacements) {
					path = path.Replace (entry.Key, entry.Value);
				}
				
				return path;
			}
		}

		private const string FileName = "Assets/CoinFlipGames/Sleepy/Resources/CoinFlipGames.Sleepy.txt";
		private const string HttpEndpointKey = "CoinFlipGames.Sleepy.HttpEndpoint";
		private const string ApiKeyKey = "CoinFlipGames.Sleepy.ApiKey";
		private const string ApiSecretKey = "CoinFlipGames.Sleepy.ApiSecret";
		private const string RoutesKey = "CoinFlipGames.Sleepy.Routes";
		private const string DebugKey = "CoinFlipGames.Sleepy.Debug";

		/// <summary>
		/// The API URL.
		/// </summary>
		public string HttpEndpoint { get; private set; }

		/// <summary>
		/// API Key used for signing requests.
		/// </summary>
		public string ApiKey { get; private set; }

		/// <summary>
		/// API Secret used for signing requests.
		/// </summary>
		public string ApiSecret { get; private set; }

		/// <summary>
		/// Routes defined in the API.
		/// </summary>
		private Dictionary<string, Route> Routes;

		/// <summary>
		/// Returns the HTTP response headers as a Hashtable.
		/// </summary>
		public List<string> ResponseHeaders { get; private set; }

		/// <summary>
		/// Defines a delegate to run on completion.
		/// </summary>	
		public delegate void Complete (Hashtable response);
		
		/// <summary>
		/// Defines a delegate to run on failure.
		/// </summary>
		public delegate void Error (string response);

		/// <summary>
		/// Enables/disables debug output.
		/// </summary>
		private bool DebugOutput;

		public RestClient () 
		{
			this.Load ();

			if (this.DebugOutput) {
				Debug.Log ("Sleepy initialized for " + this.HttpEndpoint);
				foreach (KeyValuePair<string, Route> route in this.Routes) {
					Debug.Log ("Route " + route.Value.Name + " -> " + route.Value.Method.ToString () + " " + route.Value.Path);
				}
			}
		}

		/// <summary>
		/// Send request to the API with the given HTTP Method, and path. The authentication key is 
		/// automatically attached to appropriate requests.
		/// </summary>
		/// <param name="routeName">Route name.</param>
		/// <param name="callback">Callback.</param>
		public void Request (string routeName, Complete callback)
		{
			Request (routeName, new Dictionary<string, string> (), callback);
		}

		/// <summary>
		/// Send request to the API with the given HTTP Method, and path. The authentication key is 
		/// automatically attached to appropriate requests.
		/// </summary>
		/// <param name="routeName">Route name.</param>
		/// <param name="replacements">Replacements.</param>
		/// <param name="callback">Callback.</param>
		public void Request (string routeName, Dictionary<string, string> replacements, Complete callback) 
		{
			Request (routeName, replacements, callback, (string response) => {
				Debug.LogError ("Request failed: " + response);
			});
		}

		/// <summary>
		/// Send request to the API with the given HTTP Method, and path. The authentication key is 
		/// automatically attached to appropriate requests.
		/// </summary>
		/// <param name="routeName">Route name.</param>
		/// <param name="data">Data.</param>
		/// <param name="callback">Callback.</param>
		public void Request (string routeName, WWWForm data, Complete callback)
		{
			Request (routeName, new Dictionary<string, string> (), data, callback, (string response) => {
				Debug.LogError ("Request failed: " + response);
			});
		}

		/// <summary>
		/// Send request to the API with the given HTTP Method, and path. The authentication key is 
		/// automatically attached to appropriate requests.
		/// </summary>
		/// <param name="routeName">Route name.</param>
		/// <param name="replacements">Replacements.</param>
		/// <param name="callback">Callback.</param>
		/// <param name="errorCallback">Error callback.</param>
		public void Request (string routeName, Dictionary<string, string> replacements, Complete callback, Error errorCallback) 
		{
			Request (routeName, replacements, new WWWForm (), callback, errorCallback);
		}

		/// <summary>
		/// Send request to the API with the given HTTP Method, and path. The authentication key is 
		/// automatically attached to appropriate requests.
		/// </summary>
		/// <param name="routeName">Route name.</param>
		/// <param name="data">Data.</param>
		/// <param name="callback">Callback.</param>
		/// <param name="errorCallback">Error callback.</param>
		public void Request (string routeName, WWWForm data, Complete callback, Error errorCallback)
		{
			Request (routeName, new Dictionary<string, string> (), data, callback, errorCallback);
		}

		/// <summary>
		/// Send request to the API with the given HTTP Method, path and data. The authentication key is 
		/// automatically attached to appropriate requests.
		/// </summary>
		/// <param name="routeName">Route name.</param>
		/// <param name="callback">Callback.</param>
		public void Request (string routeName, Dictionary<string, string> replacements, WWWForm data, Complete callback, Error errorCallback) 
		{	
			Route route = this.Routes [routeName];

			// Add key to all requests, if we have it
			if (null != this.ApiKey && null != this.ApiSecret && route.IsSigned) {
				SignRequest (route.Path, ref data);
			}

			string path = route.Path;
			foreach (KeyValuePair<string, string> replacement in replacements) {
				path = path.Replace (replacement.Key, WWW.EscapeURL (replacement.Value));
			}

			HTTP.Request rq = new HTTP.Request (route.Method.ToString(), HttpEndpoint + path, data);
			rq.synchronous = !route.IsAsync;
			rq.useCache = false;
			
			GetResponse (rq, callback, errorCallback);
		}
		
		/// <summary>
		/// Using the currently assigned credentials, sign the request to the given API path.
		/// </summary>
		/// <param name="path">Path.</param>
		/// <param name="data">Data.</param>
		private void SignRequest (string path, ref WWWForm data)
		{
			string deviceId = SystemInfo.deviceUniqueIdentifier;
			long t = System.DateTime.Now.Ticks;
			
			string raw = deviceId + this.ApiKey + this.ApiSecret + path + t.ToString ();
			byte[] asciiBytes = ASCIIEncoding.ASCII.GetBytes (raw);
			byte[] hashedBytes = MD5CryptoServiceProvider.Create ().ComputeHash (asciiBytes);
			
			string signature = BitConverter.ToString (hashedBytes).Replace ("-", "").ToLower ();
			
			data.AddField ("signature", signature);
			data.AddField ("t", t.ToString ());

			if (this.DebugOutput) {
				Debug.Log ("Request signature: " + signature);
			}
		}
		
		/// <summary>
		/// Sends the request and sets the parsed response
		/// </summary>
		/// <returns>The HashTable response.</returns>
		/// <param name="rq">HTTP.Request rq.</param>
		/// <param name="callback">Method to call on complete</param>
		/// <param name="errorCallback">Method to call on error</param>
		private void GetResponse (HTTP.Request rq, Complete callback, Error errorCallback) 
		{
			ResponseHeaders = new List<string> ();
			
			try {
				rq.Send ((request) => {
					if (this.DebugOutput) {
						Debug.Log (rq.InfoString (true));
					}
					
					if (200 != request.response.status) {
						errorCallback (request.response.status + " Error: " + request.response.ToString ());
						return;
					}
					
					ResponseHeaders = request.response.GetHeaders ();
					bool success = false;
					Hashtable rs = (Hashtable)JSON.JsonDecode (request.response.Text, ref success);
					
					if (!success || rs.Count == 0) {
						errorCallback (request.response.Text);
						return;
					} else {
						callback (rs);
					}
				});
			} catch (System.Net.Sockets.SocketException e) {
				errorCallback ("Connection error: " + e.Message);
			} catch (System.Exception e) {
				errorCallback ("General error: " + e.Message);
			}
		}

		private void Load ()
		{
			if (File.Exists (FileName)) {
				string raw = File.ReadAllText (FileName);
				Hashtable data = (Hashtable)JSON.JsonDecode (raw);
				
				this.HttpEndpoint = data [HttpEndpointKey].ToString ();
				this.ApiKey = data [ApiKeyKey].ToString ();
				this.ApiSecret = data [ApiSecretKey].ToString ();
				
				this.Routes = new Dictionary<string, Route> ();
				ArrayList serializedRoutes = (ArrayList) data[RoutesKey];
				foreach (Hashtable route in serializedRoutes) {
					this.Routes.Add (
						route ["Name"].ToString (),
					    new Route (
							route ["Name"].ToString (),
							(RequestMethod)System.Enum.Parse (typeof(RequestMethod), route ["Method"].ToString ()),
							route ["Path"].ToString (),
							bool.Parse (route ["Async"].ToString ()),
							bool.Parse (route ["Secure"].ToString ())
						)
					);
				}

				this.DebugOutput = bool.Parse (data [DebugKey].ToString ());
			} else {
				this.HttpEndpoint = "";
				this.ApiKey = "";
				this.ApiSecret = "";
				this.Routes = new Dictionary<string, Route> ();
				this.DebugOutput = true;
			}
		}
	}
}