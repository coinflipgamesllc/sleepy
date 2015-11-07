using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SleepyEditor : EditorWindow	
{
	private const string FileName = "Assets/CoinFlipGames/Sleepy/Resources/CoinFlipGames.Sleepy.txt";
	private const string HttpEndpointKey = "CoinFlipGames.Sleepy.HttpEndpoint";
	private const string ApiKeyKey = "CoinFlipGames.Sleepy.ApiKey";
	private const string ApiSecretKey = "CoinFlipGames.Sleepy.ApiSecret";
	private const string RoutesKey = "CoinFlipGames.Sleepy.Routes";
	private const string DebugKey = "CoinFlipGames.Sleepy.Debug";

	private enum RequestMethod 
	{
		GET,
		POST,
		PUT,
		DELETE
	}

	private struct Route
	{
		public string name;
		public RequestMethod method;
		public string path;
		public bool isSigned;
		public bool isAsync;

		public Route (string name, RequestMethod method, string path, bool isSigned, bool isAsync)
		{
			this.name = name;
			this.method = method;
			this.path = path;
			this.isSigned = isSigned;
			this.isAsync = isAsync;
		}
	}

	private string httpEndpoint;
	private string apiKey;
	private string apiSecret;
	private bool debugOutput;
	private List<Route> routes;

	private bool formToggle;
	private int formEditIndex = -1;
	private string formName;
	private RequestMethod formMethod;
	private string formPath;
	private bool formIsAsync;
	private bool formIsSigned;

	[MenuItem ("Coin Flip Games/Sleepy REST Client")]
	public static void ShowWindow ()
	{
		EditorWindow.GetWindow (typeof (SleepyEditor));
	}

	void OnEnable ()
	{
		this.httpEndpoint = "https://example.com";
		this.apiKey = "SOME_API_KEY";
		this.apiSecret = "SOME_LONG_SECRET_KEY";
		this.debugOutput = true;
		this.routes = new List<Route> ();
		
		this.Load ();
	}

	void OnGUI ()
	{
		GUILayout.Label ("Sleepy REST Client Settings", EditorStyles.boldLabel);

		// Authentication settings
		this.httpEndpoint = EditorGUILayout.TextField ("HTTP Endpoint", this.httpEndpoint);
		this.apiKey = EditorGUILayout.TextField ("API Key", this.apiKey);
		this.apiSecret = EditorGUILayout.TextField ("API Secret", this.apiSecret);
		this.debugOutput = EditorGUILayout.Toggle ("Show Debug Log", this.debugOutput);

		EditorGUILayout.Space ();
		EditorGUILayout.Separator ();

		// Routes
		GUILayout.Label ("Routes", EditorStyles.boldLabel);

		this.formToggle = EditorGUILayout.Foldout (this.formToggle, "Create/Update a Route");
		this.ShowAddEditForm ();
	
		this.ShowRouteList ();

		EditorGUILayout.Space ();
		EditorGUILayout.Separator ();

		if (GUILayout.Button ("Save Settings", EditorStyles.miniButton)) {
			this.Save ();
		}
	}

	private void ShowRouteList ()
	{
		for (int i = 0; i < this.routes.Count; i++) {
			GUILayout.BeginVertical (EditorStyles.helpBox);
			GUILayout.Label (this.routes [i].name + ": " + this.routes [i].method.ToString () + " " + this.routes [i].path, EditorStyles.label);
			
			GUILayout.BeginHorizontal ();

			GUILayout.Label (this.routes [i].isAsync ? "Asynchronous" : "Synchronous", EditorStyles.miniBoldLabel);
			GUILayout.Label (this.routes [i].isSigned ? "Signed" : "Unsigned", EditorStyles.miniBoldLabel);

			if (GUILayout.Button ("Edit", EditorStyles.miniButtonRight)) {
				this.formName = this.routes [i].name;
				this.formMethod = this.routes [i].method;
				this.formPath = this.routes [i].path;
				this.formIsAsync = this.routes [i].isAsync;
				this.formIsSigned = this.routes [i].isSigned;

				this.formToggle = true;
				this.formEditIndex = i;
			}

			if (GUILayout.Button ("Delete", EditorStyles.miniButtonRight)) {
				this.routes.RemoveAt (i);
				this.Repaint ();
			}

			GUILayout.EndHorizontal ();
			
			GUILayout.EndVertical ();
		}
	}

	private void ShowAddEditForm ()
	{
		if (this.formToggle) {
			this.formName = EditorGUILayout.TextField ("Name", this.formName);
			this.formMethod = (RequestMethod)EditorGUILayout.EnumPopup ("Method", this.formMethod);
			this.formPath = EditorGUILayout.TextField ("Path", this.formPath);
			this.formIsAsync = EditorGUILayout.Toggle ("Asynchronous?", this.formIsAsync);
			this.formIsSigned = EditorGUILayout.Toggle ("Signed?", this.formIsSigned);

			if (GUILayout.Button (this.formEditIndex > -1 ? "Update Route" : "Create Route", EditorStyles.miniButton)) {
				this.AddEditRoute (this.formName, this.formMethod, this.formPath, this.formIsAsync, this.formIsSigned, this.formEditIndex);
				
				// Reset the form
				this.formName = "";
				this.formMethod = RequestMethod.GET;
				this.formPath = "";
				this.formIsAsync = false;
				this.formIsSigned = false;
				this.formToggle = false;
				this.formEditIndex = -1;
				
				// Repaint
				this.Repaint ();
			}
		}
	}

	private void AddEditRoute (string name, RequestMethod method, string path, bool isAsync, bool isSigned, int editIndex)
	{
		if (name.Length > 0 && path.Length > 0) {
			if (editIndex == -1) {
				// Add the route
				this.routes.Add (new Route (name, method, path, isAsync, isSigned));
			} else {
				// Replace existing route
				this.routes [editIndex] = new Route (name, method, path, isAsync, isSigned);
			}
		}
	}

	private void Load ()
	{
		if (File.Exists (FileName)) {
			string raw = File.ReadAllText (FileName);
			Hashtable data = (Hashtable)JSON.JsonDecode (raw);
			
			this.httpEndpoint = data [HttpEndpointKey].ToString ();
			this.apiKey = data [ApiKeyKey].ToString ();
			this.apiSecret = data [ApiSecretKey].ToString ();
			this.debugOutput = bool.Parse (data [DebugKey].ToString ());
			
			this.routes = new List<Route> ();
			ArrayList serializedRoutes = (ArrayList) data[RoutesKey];
			foreach (Hashtable route in serializedRoutes) {
				this.routes.Add (new Route (
					route ["Name"].ToString (),
					(RequestMethod)System.Enum.Parse (typeof (RequestMethod), route ["Method"].ToString ()),
					route ["Path"].ToString (),
					bool.Parse (route ["Async"].ToString ()),
					bool.Parse (route ["Signed"].ToString ())
				));
			}
		}
	}

	private void Save ()
	{
		Hashtable data = new Hashtable ();
		data.Add (HttpEndpointKey, this.httpEndpoint);
		data.Add (ApiKeyKey, this.apiKey);
		data.Add (ApiSecretKey, this.apiSecret);
		data.Add (DebugKey, this.debugOutput);
		
		ArrayList serializedRoutes = new ArrayList ();
		foreach (Route route in this.routes) {
			Hashtable routeData = new Hashtable ();
			routeData ["Name"]   = route.name;
			routeData ["Method"] = route.method.ToString ();
			routeData ["Path"]   = route.path;
			routeData ["Async"]  = route.isAsync.ToString ();
			routeData ["Signed"] = route.isSigned.ToString ();
			
			serializedRoutes.Add (routeData);
		}
		data.Add (RoutesKey, serializedRoutes);
		
		File.WriteAllText(FileName, JSON.JsonEncode (data));
		AssetDatabase.Refresh ();
	}
}