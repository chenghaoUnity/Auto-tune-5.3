using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Analytics;
using Unity.Performance;
using Unity.AutoTune.MiniJSON;
using System.Net;
using System.IO;
using System;

namespace Unity.AutoTune {

public class AutoTune : MonoBehaviour {
	private static string AutoTuneEndpoint = "https://test-auto-tune.uca.cloud.unity3d.com/";
	private static string CLIENT_DEFAULT_SEGMENT = "-1";
	private static int CLIENT_DEFAULT_GROUP = -1;
	private static string AutoTuneDir = "/unity.autotune";
	private static string SegmentConfigCacheFilePath = AutoTuneDir + "/segmentconfig.json";

	/// <summary>
	/// This is a custom version of DeviceInfo that we will use to get a segment
	/// for this particular device.
	/// </summary>
	public class DeviceInfo {
		public string model;
		public int    ram;
		public string cpu;
		public string gfx_name;
		public string gfx_vendor;
		public string device_id;
		public int    cpu_count;
		public float  dpi;
		public string screen;
		public string sheet_id;
		public int platformid; 
		public string os_ver;
		public int gfx_shader;
		public string gfx_ver;
		public int max_texture_size;
		public string app_build_version;

		public DeviceInfo(string sheetId, string app_build_version) {
			this.sheet_id = sheetId;
			this.app_build_version = app_build_version;
			this.model = SystemInfo.deviceModel;
			this.device_id = SystemInfo.deviceUniqueIdentifier;
			this.ram = SystemInfo.systemMemorySize;
			this.cpu = SystemInfo.processorType;
			this.cpu_count = SystemInfo.processorCount;
			this.gfx_name = SystemInfo.graphicsDeviceName;
			this.gfx_vendor = SystemInfo.graphicsDeviceVendor;
			this.screen = Screen.currentResolution.ToString();
			this.dpi = Screen.dpi;
			this.platformid = (int) Application.platform;
			this.os_ver = SystemInfo.operatingSystem;
			this.gfx_shader = SystemInfo.graphicsShaderLevel;
			this.gfx_ver = SystemInfo.graphicsDeviceVersion;
			this.max_texture_size = SystemInfo.maxTextureSize;
		}
	}

	private static AutoTune _instance;
	private static PerfRecorder _prInstance;

	[RuntimeInitializeOnLoadMethod]
	static AutoTune GetInstance()
	{
		if (_instance == null)
		{
			_instance = FindObjectOfType<AutoTune>();
			if (_instance == null)
			{
				var gO = new GameObject("AutoTune");
				_instance = gO.AddComponent<AutoTune>();
				_prInstance = gO.AddComponent<PerfRecorder>();
				gO.hideFlags = HideFlags.HideAndDontSave;
			}
			DontDestroyOnLoad(_instance.gameObject);
		}
		return _instance;
	}

	void Awake()
	{
		if (!GetInstance().Equals(this)) Destroy(gameObject);
	}
	
	public delegate void AutoTuneCallback(Dictionary<string, object> settings, int group);
	private string _sheetId;
	private string _buildVersion;
	private SegmentConfig _clientDefaultConfig;
	private string _storePath;

	// once initialized, the _cachedSegmentConfig is never null
	private SegmentConfig _cachedSegmentConfig;
	private bool _isPlayerOverride = false;

	// request state
	private bool _updateNeeded = false;
	private bool _isError = false;
	private float _startTime = 0;
	private AutoTuneCallback _callback;
	private DeviceInfo _deviceInfo = null;


	/// <summary>
	/// Setups the AutoTune API. Make sure you call this before
	/// anything.
	/// <param name="sheetId">The google sheet id where you defined the segments and configs.</param>
	/// <param name="buildVersion">The build version.</param>
	/// <param name="usePersistentPath">True to use persistentDataPath for storing segment config, otherwise temporaryCachePath would be use.</param>
	/// <param name="defaultValues">The default values that will be used in case of network error.</param>
	/// </summary>
	public static void Init(string sheetId,
							string buildVersion,
							bool usePersistentPath,
							Dictionary<string, object> defaultValues
							)
	{
		GetInstance ()._sheetId = sheetId;
		GetInstance ()._clientDefaultConfig = new SegmentConfig(CLIENT_DEFAULT_SEGMENT, CLIENT_DEFAULT_GROUP, defaultValues, "client_default");

		// Application.persistentDataPath can only be called from the main thread so we cache it in init
		GetInstance ()._storePath = usePersistentPath ? Application.persistentDataPath : Application.temporaryCachePath;
		GetInstance ()._buildVersion = buildVersion;

		// load cache segment config last after all other variables has been set
		GetInstance ().LoadCacheSegmentConfig();
		_prInstance.SetBuildVersion(buildVersion);
		System.Net.ServicePointManager.ServerCertificateValidationCallback += (o, certificate, chain, errors) => true;
	}

	/// <summary>
	/// Start AutoTune: fetch new settings for this specific device.
	/// <param name="callback">The callback that will get executed when new settings are received and parsed</param>
	/// </summary>
	public static void Fetch(AutoTuneCallback callback)
	{
		GetInstance ().CleanUp();
		GetInstance ()._callback = callback;
		GetInstance ()._startTime = Time.time;
		GetInstance ().TryFetch();
	}

	/// <summary>
	/// Gets the PerfRecorder instance
	/// </summary>
	public static PerfRecorder GetPerfRecorder()
	{
		return _prInstance;
	}

	/// <summary>
	/// API to auto-tune that the player is using manual override of setting.
	/// This information allows us to differentiate devices that do not use
	/// auto-tune settings.
	/// </summary>
	public static void SetPlayerOverride(bool isPlayerOverride) 
	{
		GetInstance ()._isPlayerOverride = isPlayerOverride;
	}

	public void Update()
	{
		if (_updateNeeded == true && _callback != null)
		{
			var segmentConfig = _cachedSegmentConfig;
			var deviceInfo = _deviceInfo;
			try
			{
				var roundtripTime = Time.time - _startTime;
				Debug.LogFormat ("autotune roundtrip: {0}", roundtripTime);
				_callback(segmentConfig.settings, segmentConfig.group_id);
                                
				// should not happen but do not want to write null checks in code below this
				if(deviceInfo == null) {
					deviceInfo = new DeviceInfo(_sheetId, _buildVersion);
				}

				// device information data should reuse the same naming convention as DeviceInfo event
				var status = Analytics.CustomEvent("autotune.SegmentRequestInfo", new Dictionary<string,object>() {
						{"segment_id", segmentConfig.segment_id},
						{"group_id", segmentConfig.group_id},
						{"error", _isError},
						{"player_override", _isPlayerOverride},
						{"request_latency", roundtripTime},
						{"model", deviceInfo.model},
						{"ram", deviceInfo.ram},
						{"cpu", deviceInfo.cpu},
						{"cpu_count", deviceInfo.cpu_count},
						{"gfx_name", deviceInfo.gfx_name},
						{"gfx_vendor", deviceInfo.gfx_vendor},
						{"screen", deviceInfo.screen},
						{"dpi", deviceInfo.dpi},
						{"gfx_ver", deviceInfo.gfx_ver},
						{"gfx_shader", deviceInfo.gfx_shader},
						{"max_texture_size", deviceInfo.max_texture_size},
						{"os_ver", deviceInfo.os_ver},
						{"platformid", deviceInfo.platformid},
						{"app_build_version", _buildVersion},
						{"plugin_version", AutoTuneMeta.version},
						{"sheet_id", deviceInfo.sheet_id}
					});
        
					Debug.Log("autotune.SegmentRequestInfo event status: " + status);
					FirebaseConnection.GetInstance().PushEventStatus(status);

					var dic = new Dictionary<string,object>() {
						{"segment_id", segmentConfig.segment_id},
						{"group_id", segmentConfig.group_id},
						{"error", _isError},
						{"player_override", _isPlayerOverride},
						{"request_latency", roundtripTime},
						{"model", deviceInfo.model},
						{"ram", deviceInfo.ram},
						{"cpu", deviceInfo.cpu},
						{"cpu_count", deviceInfo.cpu_count},
						{"gfx_name", deviceInfo.gfx_name},
						{"gfx_vendor", deviceInfo.gfx_vendor},
						{"screen", deviceInfo.screen},
						{"dpi", deviceInfo.dpi},
						{"gfx_ver", deviceInfo.gfx_ver},
						{"gfx_shader", deviceInfo.gfx_shader},
						{"max_texture_size", deviceInfo.max_texture_size},
						{"os_ver", deviceInfo.os_ver},
						{"platformid", deviceInfo.platformid},
						{"app_build_version", _buildVersion},
						{"plugin_version", AutoTuneMeta.version},
						{"sheet_id", deviceInfo.sheet_id}
					};

					FirebaseConnection.GetInstance ().PushAutotuneDic (dic);
			}
			catch (System.Exception e)
			{
				Debug.LogError(e);
			}
			finally
			{
				_isError = false;
				_updateNeeded = false;
			}
		}
	}

	private void TryFetch()
	{
		using (var client = new WebClient()) {
			client.UploadDataCompleted += (new UploadDataCompletedEventHandler(wc_UploadDataCompleted));
			client.Headers.Add("Content-Type","application/json");
			DeviceInfo di = new DeviceInfo(_sheetId, _buildVersion);
			string payload = JsonUtility.ToJson(di);
			_deviceInfo = di;
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(payload);
			var uri = new System.Uri(AutoTuneEndpoint + "/v1/settings");
			try {
				Debug.Log("autotune will send data:");
				Debug.Log(payload);
				client.UploadDataAsync(uri, "POST", bytes);
			} catch (WebException err) {
				Debug.LogError("autotune error on web request");
				Debug.LogError(err);
			}
		}
	}

	/// <summary>
	/// Clean the dictionary and strip the params from the server response.
	/// Never returns null.
	/// </summary>
	private SegmentConfig ParseResponse(Dictionary<string,object> response)
	{
		var res = new Dictionary<string,object>();
		var pars = response["params"] as List<object>;
		var segmentId = (string)response["segment_id"];
		int groupId = (int)(long)response["group"];
		foreach (var param in pars)
		{
			var dict = param as Dictionary<string,object>;
			if (dict["value"] is long) {
				res[(string)dict["name"]] = (int)(long)dict["value"];
			} else if (dict["value"] is double) {
				res[(string)dict["name"]] = (float)(double)dict["value"];
			} else {
				res[(string)dict["name"]] = dict["value"];
			}
		}
		
		// TODO: add server provided hash
		return new SegmentConfig(segmentId, groupId, res, "0");
	}

	private void wc_UploadDataCompleted(object sender,
										UploadDataCompletedEventArgs e)
	{
		if (e.Cancelled)
		{
			Debug.Log("autotune request canceled");
			lock(this)
			{
				_isError = true;
				_updateNeeded = true;
			}
		}
		else if (e.Error != null)
		{
			// something happened, send the default config
			Debug.Log("autotune request error: ");
			Debug.LogError(e.Error);
			
			lock(this)
			{
				_isError = true;
				_updateNeeded = true;
			}
		}
		else
		{
			try {
				var jsonStr = System.Text.Encoding.UTF8.GetString(e.Result);
				var resp = Json.Deserialize(jsonStr) as Dictionary<string,object>;
				Debug.LogFormat ("autotune response payload: {0}", jsonStr);
				var newSegmentConfig = ParseResponse(resp);

				lock(this)
				{
					// configuration changed, save it to file
					if(_cachedSegmentConfig.config_hash != newSegmentConfig.config_hash) {
						CacheSegmentConfig(newSegmentConfig);
					}
					else {
						// TODO - remove once server provided hash is in place
						CacheSegmentConfig(newSegmentConfig);
					}

					_cachedSegmentConfig = newSegmentConfig;
					_updateNeeded = true;
				}
			}
			catch(Exception ex) {
				Debug.LogError("autotune error parsing response: " + ex);
				lock(this)
				{
					_isError = true;
					_updateNeeded = true;
				}
			}
		}
	}

	private void CacheSegmentConfig(SegmentConfig config) 
    {
    	var dirPath = _storePath + AutoTuneDir;
    	if(!Directory.Exists(dirPath)) {
    		Directory.CreateDirectory(dirPath);
    	}

    	var filePath = _storePath + SegmentConfigCacheFilePath;
    	Debug.Log("autotune storing segment config to file: " + filePath);
    	using(var writer = new StreamWriter(filePath)) {
    		writer.Write(config.ToJsonDictionary()); 
    	}
    }


    /// <summary>
	/// Loads from file.
	/// On failure this defaults to use client default setting.
	/// </summary>
    private void LoadCacheSegmentConfig()
    {
    	var filePath = _storePath + SegmentConfigCacheFilePath;
    	if(!File.Exists(filePath)){
    		_cachedSegmentConfig = _clientDefaultConfig;
    		Debug.Log("autotune did not find cached config in path: " + filePath);
    		return;
    	}

		try {
			using(var reader = new StreamReader(filePath)) {
				var json = reader.ReadToEnd();
				_cachedSegmentConfig = SegmentConfig.fromJsonDictionary(json);
				Debug.Log("autotune loaded cached config: " + json);
			}
		}
		catch(Exception ex) {
			// for any issues with the file, use client defaults
			_cachedSegmentConfig = _clientDefaultConfig;
			Debug.LogError("autotune error processing cached config file: " + filePath + " , error: " + ex);
		}
    }

	private void CleanUp()
	{
		_updateNeeded = false;
		_isError = false;
		_deviceInfo = null;
		_callback = null;
		_startTime = 0;
	}

}

}
