#if FISHNET
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Smooth camera for the local networked player. The predicted body moves on network ticks (30 per second by
    /// default), so a camera that follows it, or one of its children, steps 30 times a second while the screen draws
    /// many more frames: walking looks jerky even on a host. For every Cinemachine camera whose Follow or LookAt is part
    /// of the player but not of the smoothed Graphical Object, this puts a stand-in target that is interpolated between
    /// the last two ticks every frame, and points the camera at it. Teleports snap. Undone when the session ends.
    /// </summary>
    internal sealed class CameraTargetSmoother
    {
        private sealed class Proxy
        {
            public Transform source;
            public Transform stand;
            public Vector3 prevPos, currPos;
            public Quaternion prevRot, currRot;
        }

        private readonly Transform root;
        private Transform gfx;
        private Vector3 gfxRestLocal;
        // Fish-Net's NetworkTickSmoother keeps the model's unsmoothed pose on a "<model>_Tracker" object: the difference
        // between it and the model is exactly the smoothing, in position and rotation.
        private Transform tracker;
        private float nextTrackerSearch;
        private CameraStandInDriver driver;

        /// <summary>Called by the driver every LateUpdate, before the cameras are placed (gives the tick percent).</summary>
        public System.Func<float> TickPercent;
        private readonly float snapDistance;
        private readonly List<Proxy> proxies = new List<Proxy>();
        private readonly List<(CinemachineCamera cam, Transform follow, Transform lookAt)> retargeted = new List<(CinemachineCamera, Transform, Transform)>();
        // Cameras that were children of the body (they would step with it): moved under a smoothed stand-in of their parent.
        private readonly List<(Transform camera, Transform parent, Vector3 localPos, Quaternion localRot)> reparented = new List<(Transform, Transform, Vector3, Quaternion)>();

        public CameraTargetSmoother(Transform root, float snapDistance)
        {
            this.root = root;
            this.snapDistance = snapDistance;
        }

        public bool Active => retargeted.Count > 0 || reparented.Count > 0;

        /// <summary>Points the cameras following this player at smoothed stand-ins (call on the owning machine).</summary>
        public void Begin(Transform graphicalObject)
        {
            // With a smoothed model, the cameras move exactly like it (same delay, same curve): otherwise the model
            // drifts back and forth against the camera every tick, which looks like walking in jerks.
            gfx = graphicalObject != null && graphicalObject != root ? graphicalObject : null;
            tracker = null;
            nextTrackerSearch = 0f;
            if (gfx != null && root != null)
                gfxRestLocal = root.InverseTransformPoint(gfx.position);
            End();
            if (root == null)
                return;
            var cameras = new List<CinemachineCamera>(root.GetComponentsInChildren<CinemachineCamera>(true));
            foreach (CinemachineCamera cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include))
                if (!cameras.Contains(cam))
                    cameras.Add(cam);
            foreach (CinemachineCamera cam in cameras)
            {
                if (cam == null)
                    continue;
                Transform follow = cam.Follow, lookAt = cam.LookAt;
                Transform newFollow = StandIn(follow, graphicalObject);
                Transform newLookAt = StandIn(lookAt, graphicalObject);
                if (newFollow == follow && newLookAt == lookAt)
                    continue;
                retargeted.Add((cam, follow, lookAt));
                cam.Follow = newFollow;
                cam.LookAt = newLookAt;
            }

            // Cameras placed as children of the player (a camera rig on the body, no Follow target): their parent moves
            // on ticks, so put them under a smoothed stand-in of that parent, at the same local pose. The main camera
            // with the Cinemachine brain is placed by Cinemachine and stays where it is.
            var rigs = new List<Transform>();
            foreach (CinemachineCamera cam in root.GetComponentsInChildren<CinemachineCamera>(true))
                if (cam != null && cam.Follow == null && cam.LookAt == null)
                    rigs.Add(cam.transform);
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                if (cam != null && cam.GetComponent<CinemachineBrain>() == null)
                    rigs.Add(cam.transform);
            foreach (Transform rig in rigs)
            {
                Transform parent = rig.parent;
                if (parent == null || rigs.Exists(other => other != rig && rig.IsChildOf(other)))
                    continue; // nested under another rig that moves already
                if (graphicalObject != null && graphicalObject != root && (rig.IsChildOf(graphicalObject)))
                    continue; // smoothed with the model already
                Transform stand = StandIn(parent, graphicalObject);
                if (stand == parent)
                    continue;
                reparented.Add((rig, parent, rig.localPosition, rig.localRotation));
                rig.SetParent(stand, false);
            }
        }

        /// <summary>
        /// Gives the cameras their own targets and parents back and removes the stand-ins. <paramref name="destroying"/>:
        /// the player is being destroyed - its cameras go with it instead of being put back.
        /// </summary>
        public void End(bool destroying = false)
        {
            foreach ((Transform camera, Transform parent, Vector3 localPos, Quaternion localRot) in reparented)
            {
                if (camera == null)
                    continue;
                if (destroying || parent == null)
                {
                    Object.Destroy(camera.gameObject);
                    continue;
                }
                camera.SetParent(parent, false);
                camera.localPosition = localPos;
                camera.localRotation = localRot;
            }
            reparented.Clear();
            foreach ((CinemachineCamera cam, Transform follow, Transform lookAt) in retargeted)
            {
                if (cam == null)
                    continue;
                cam.Follow = follow;
                cam.LookAt = lookAt;
            }
            retargeted.Clear();
            foreach (Proxy p in proxies)
                if (p.stand != null)
                    Object.Destroy(p.stand.gameObject);
            proxies.Clear();
            if (driver != null)
                Object.Destroy(driver.gameObject);
            driver = null;
        }

        /// <summary>After each tick: where the real targets are now.</summary>
        public void RecordTick()
        {
            foreach (Proxy p in proxies)
            {
                if (p.source == null)
                    continue;
                p.prevPos = p.currPos;
                p.prevRot = p.currRot;
                p.currPos = p.source.position;
                p.currRot = p.source.rotation;
                if ((p.currPos - p.prevPos).sqrMagnitude > snapDistance * snapDistance)
                {
                    p.prevPos = p.currPos; // teleport: no sliding
                    p.prevRot = p.currRot;
                }
            }
        }

        /// <summary>
        /// Every frame: place the stand-ins between the last two ticks (<paramref name="tickPercent"/> 0..1). Only the
        /// position is interpolated - the rotation is the target's own, live: mouse look turns camera pivots every frame,
        /// and following it a tick late would make looking around feel sluggish.
        /// </summary>
        public void Interpolate(float tickPercent)
        {
            if (gfx != null && tracker == null && Time.unscaledTime >= nextTrackerSearch)
            {
                nextTrackerSearch = Time.unscaledTime + 0.5f;
                tracker = FindTracker();
            }
            if (gfx != null && tracker != null)
            {
                // Rigidly attached to the smoothed model: same position and rotation smoothing (the body turning on
                // ticks included), so the camera and the model never move against each other.
                Quaternion turn = gfx.rotation * Quaternion.Inverse(tracker.rotation);
                Vector3 offset = gfx.position - tracker.position;
                if (offset.sqrMagnitude > snapDistance * snapDistance)
                {
                    offset = Vector3.zero;
                    turn = Quaternion.identity;
                }
                foreach (Proxy p in proxies)
                {
                    if (p.stand == null || p.source == null)
                        continue;
                    Vector3 fromModel = p.source.position - tracker.position;
                    p.stand.SetPositionAndRotation(tracker.position + offset + turn * fromModel, turn * p.source.rotation);
                }
                return;
            }

            // Smoothed model without a tracker: how far its smoothing has it from where the body's tick put it.
            bool followModel = gfx != null && root != null;
            Vector3 smoothing = followModel ? gfx.position - root.TransformPoint(gfxRestLocal) : Vector3.zero;
            if (followModel && smoothing.sqrMagnitude > snapDistance * snapDistance)
                smoothing = Vector3.zero; // a teleport the model has not snapped for yet
            float t = Mathf.Clamp01(tickPercent);
            foreach (Proxy p in proxies)
            {
                if (p.stand == null || p.source == null)
                    continue;
                Vector3 position = followModel ? p.source.position + smoothing : Vector3.Lerp(p.prevPos, p.currPos, t);
                p.stand.SetPositionAndRotation(position, p.source.rotation);
            }
        }

        private Transform FindTracker()
        {
            string trackerName = gfx.name + "_Tracker";
            Transform parent = gfx.parent;
            if (parent != null)
                foreach (Transform t in parent)
                    if (t.name == trackerName)
                        return t;
            if (root != null)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == trackerName)
                        return t;
            return null;
        }

        private void EnsureDriver()
        {
            if (driver != null)
                return;
            var go = new GameObject("Smoothed Camera Targets");
            Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<CameraStandInDriver>();
            driver.Step = () => Interpolate(TickPercent != null ? TickPercent() : 1f);
        }

        // The stand-in for a camera target, or the target itself when it needs none (not this player's, or already smoothed).
        private Transform StandIn(Transform target, Transform graphicalObject)
        {
            if (target == null || (target != root && !target.IsChildOf(root)))
                return target;
            if (graphicalObject != null && graphicalObject != root && (target == graphicalObject || target.IsChildOf(graphicalObject)))
                return target;
            foreach (Proxy existing in proxies)
                if (existing.source == target)
                    return existing.stand;
            EnsureDriver();
            var go = new GameObject($"{target.name} (smoothed camera target)");
            // Not under the player: the stand-in must not move with the body between ticks. It survives scene changes
            // (the player travels between the City, the World and dungeons) and is removed by End().
            go.transform.SetParent(driver.transform, false);
            var p = new Proxy
            {
                source = target,
                stand = go.transform,
                prevPos = target.position,
                currPos = target.position,
                prevRot = target.rotation,
                currRot = target.rotation,
            };
            go.transform.SetPositionAndRotation(target.position, target.rotation);
            proxies.Add(p);
            return go.transform;
        }
    }
}
namespace GameNet
{
    /// <summary>
    /// Places the smoothed camera targets every frame in LateUpdate, before Cinemachine places the cameras (early
    /// execution order) and after Fish-Net moved and smoothed the player (first thing in the frame).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("")]
    internal sealed class CameraStandInDriver : MonoBehaviour
    {
        public System.Action Step;

        private void LateUpdate() => Step?.Invoke();
    }
}
#endif
