using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AutoTune;

public class ManyParticles : MonoBehaviour {

	public GameObject center;
	public float radius = 10.0f;
	private float _startTime = 0.0f;
	/// <summary>
	/// The EXPERIMENT TIME, in seconds.
	/// </summary>
	private const float EXPERIMENT_TIME = 30.0f;
	/// <summary>
	/// how many seconds to wait until the experiment starts
	/// </summary>
	private const float EXPERIMENT_DELAY = 10.0f;
	private int _experimentState = 0;

	// Use this for initialization
	void Start ()
	{
		var defaults = new Dictionary<string, object>() {
			{"totalObjects", 10}
		};
		_startTime = Time.time;
		AutoTune.Init("1OffXev7qC2FOX1Zbp1dwAcXei67rtwOc3X9Bop2g8y8", "1", true, defaults);
		AutoTune.Fetch(GotSettings);
	}

	void GotSettings(Dictionary<string,object> settings, long group)
	{
		// apply settings to your game, eg:
		// SettingsManager.maxParticles = settings['max_particles']
		Debug.Log("got new settings");
		FirebaseConnection.GetInstance ().PushGetSettings ();
		long numObjects = (long)settings ["totalObjects"];

		Debug.LogFormat ("will create {0} cubes", numObjects);
		FirebaseConnection.GetInstance ().PushTotalObjects (numObjects);

		while (numObjects-- > 0)
		{
			var cube = GameObject.CreatePrimitive (PrimitiveType.Cube);
			cube.AddComponent<Rotate> ();
			float x = Random.Range (-radius, radius);
			float y = Random.Range (-radius, radius);
			float z = Random.Range (-radius, radius);
			cube.transform.position = new Vector3 (x, y, z);
			cube.transform.parent = center.transform;
		}
	}
	
	// Update is called once per frame
	void Update ()
	{
		float t = Time.time;
		if (_experimentState == 0 && t - _startTime > EXPERIMENT_DELAY)
		{
			AutoTune.GetPerfRecorder ().BeginExperiment ("autotune-qa");
			_experimentState = 1;
		}
		if (_experimentState == 1 && t - (_startTime + EXPERIMENT_DELAY) > EXPERIMENT_TIME)
		{
			AutoTune.GetPerfRecorder ().EndExperiment ();
			_experimentState = 2;
		}
	}
}
