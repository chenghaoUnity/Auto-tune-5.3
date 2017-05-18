using System.Collections;
using System.Collections.Generic;
using Unity.AutoTune.MiniJSON;

namespace Unity.AutoTune {

	// keep this as an immutable object
	public class SegmentConfig 
	{
		public string segment_id;
		public long group_id;
		public Dictionary<string, object> settings;
        public string config_hash;

		public SegmentConfig(string segment_id, long group_id, Dictionary<string, object> settings, string config_hash)
		{
			this.segment_id = segment_id;
			this.group_id = group_id;
			this.settings = settings;
			this.config_hash = config_hash;
		}

		// miniJSON only supports dictionary
		public string ToJsonDictionary() {
			var dict = new Dictionary<string, object>();
	    	dict.Add("segment_id", this.segment_id);
	    	dict.Add("group_id", this.group_id);
	    	dict.Add("settings", this.settings);
	    	dict.Add("config_hash", this.config_hash);
    		return Json.Serialize(dict);
		}

		public static SegmentConfig fromJsonDictionary(string json) {
			var dict = Json.Deserialize(json) as Dictionary<string,object>;
			return new SegmentConfig(
				(string) dict["segment_id"],
				(long) dict["group_id"],
				dict["settings"] as Dictionary<string,object>,
				(string) dict["config_hash"]
				);
		}
	}
}