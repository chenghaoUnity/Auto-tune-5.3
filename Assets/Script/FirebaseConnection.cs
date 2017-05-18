using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Unity.Editor;
using UnityEngine.Events;

public class FirebaseConnection : MonoBehaviour 
{
	public UnityEvent onCompelete;
	private DatabaseReference reference;
	private static FirebaseConnection _instance;

	public static FirebaseConnection GetInstance()
	{
		if (_instance == null)
		{
			_instance = FindObjectOfType<FirebaseConnection>();
		}
		return _instance;
	}

	// Use this for initialization
	void Awake () 
	{
		FirebaseApp.DefaultInstance.SetEditorDatabaseUrl("https://devivetiering.firebaseio.com/");
		reference = FirebaseDatabase.DefaultInstance.RootReference;
	}

	public void Start() 
	{
		PushDeviceInfo ();
	}

	public void PushDeviceInfo() 
	{
		string key = reference.Child("devivetiering").Push().Key;
		Dictionary<string, object> childUpdates = new Dictionary<string, object>();
		childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/deviceModel/"] = SystemInfo.deviceModel.ToString ();
		childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/operatingSystem/"] = SystemInfo.operatingSystem.ToString ();
		childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/deviceType/"] = SystemInfo.deviceType.ToString ();
		childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/eventStatus/"] = "wait";
		childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/getSettings/"] = "wait";

		reference.UpdateChildrenAsync(childUpdates);
	}

	public void PushEventStatus(object status) 
	{
		string key = reference.Child("devivetiering").Push().Key;
		Dictionary<string, object> childUpdates = new Dictionary<string, object>();
		childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/eventStatus/"] = status.ToString ();
		reference.UpdateChildrenAsync(childUpdates);
	}

	public void PushGetSettings() 
	{
		string key = reference.Child("devivetiering").Push().Key;
		Dictionary<string, object> childUpdates = new Dictionary<string, object>();
		childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/getSettings/"] = "Ok";
		reference.UpdateChildrenAsync(childUpdates);
	}

	public void PushTotalObjects(long value) 
	{
		string key = reference.Child("devivetiering").Push().Key;
		Dictionary<string, object> childUpdates = new Dictionary<string, object>();
		childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/totalObjects/"] = value.ToString();
		reference.UpdateChildrenAsync(childUpdates);
	}

	public void PushAutotuneDic(Dictionary<string, object> dic) 
	{
		string key = reference.Child("devivetiering").Push().Key;
		Dictionary<string, object> childUpdates = new Dictionary<string, object>();
		foreach(string thekey in dic.Keys)
		{
			object value = dic [thekey];
			childUpdates ["/Device/" + SystemInfo.deviceUniqueIdentifier + "/AutotuneDic/" + thekey] = value.ToString();
		}
		reference.UpdateChildrenAsync(childUpdates);
	}
}
