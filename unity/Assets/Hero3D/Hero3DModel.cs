using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drakepg.Hero3D
{
    /// <summary>
    /// What a hero model's look file (hero_look.json) says beyond its materials, kept on the model prefab by
    /// Hero3DImport (Assets/Editor/Hero3DImport.cs) so the game needs no JSON at run time: the game view, the light,
    /// the size, which objects are body parts armor may hide, the gear pieces, and the optional rim and shadow mask.
    /// Also shows and hides objects by name (body parts, gear). Free of game types.
    /// </summary>
    public class Hero3DModel : MonoBehaviour
    {
        [Serializable]
        public class Gear
        {
            public string id, slot, bone, socket;
            public string[] objects = new string[0];
            public string[] hides = new string[0];
        }

        [Header("From the look file")]
        public string lookVersion = "1";
        /// <summary>Rest height (m) the picture's size is reckoned from: DrawnHeight px = this many metres.</summary>
        public float heightM = 1.75f;
        /// <summary>Top of the rest pose above the feet (m), as measured at import (hair included).</summary>
        public float restHeightM;
        /// <summary>Which way the model's front points, as a turn about up from +Z (degrees): 180 = it faces -Z.</summary>
        public float frontYaw;
        /// <summary>Game view: turned this far toward the facing side, camera tilted down this far (degrees).</summary>
        public float gameYaw = 40, gamePitch = 5;
        /// <summary>Light, camera space (x right, y up, z toward the viewer), and the N.L cut.</summary>
        public Vector3 lightView = new Vector3(0.55f, 0.65f, 0.52f);
        public float cut;
        public bool rim;
        public Color rimColor = new Color(1, 0.95f, 0.8f, 1);
        public float rimWidthPx = 1;
        public bool shadowMask;
        public string shadowMaskLayer = "";
        public int shadowMaskChannel = -1;
        public float shadowMaskThreshold = 0.5f;
        /// <summary>Body objects armor may hide (empty: every mesh object counts as a part).</summary>
        public List<string> parts = new List<string>();
        public List<Gear> gear = new List<Gear>();
        public List<string> clips = new List<string>();

        Dictionary<string, GameObject> objects, bones;

        void Index()
        {
            if (objects != null) return;
            // Mesh objects and bones apart: a mesh and a bone may share a name (round 1's "Head").
            objects = new Dictionary<string, GameObject>();
            bones = new Dictionary<string, GameObject>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                var map = t.GetComponent<Renderer>() != null ? objects : bones;
                if (!map.ContainsKey(t.name)) map[t.name] = t.gameObject;
            }
        }

        /// <summary>A drawn object (mesh) by name, else any child by that name, or null.</summary>
        public GameObject Find(string objectName)
        {
            Index();
            if (objectName == null) return null;
            return objects.TryGetValue(objectName, out var go) ? go : bones.TryGetValue(objectName, out go) ? go : null;
        }

        /// <summary>Show or hide one mesh object by name; false when there is no such object.</summary>
        public bool SetVisible(string objectName, bool on)
        {
            Index();
            var go = objectName != null && objects.TryGetValue(objectName, out var o) ? o : null;
            if (go == null) return false;
            go.SetActive(on);
            return true;
        }

        /// <summary>Put on (or take off) a gear piece: its objects shown, the body parts it covers hidden (or shown again).</summary>
        public bool Wear(string gearId, bool on)
        {
            var g = gear.Find(x => x.id == gearId);
            if (g == null) return false;
            foreach (var o in g.objects) SetVisible(o, on);
            foreach (var p in g.hides) SetVisible(p, !on);
            return true;
        }

        /// <summary>Every gear piece off and every body part on.</summary>
        public void Bare()
        {
            foreach (var g in gear) foreach (var o in g.objects) SetVisible(o, false);
            foreach (var p in parts) SetVisible(p, true);
        }

        /// <summary>A bone or socket by name (never a mesh object), or null.</summary>
        public Transform Bone(string boneName)
        {
            Index();
            return boneName != null && bones.TryGetValue(boneName, out var go) ? go.transform : null;
        }
    }
}
