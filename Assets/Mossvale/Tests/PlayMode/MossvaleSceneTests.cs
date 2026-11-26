using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mossvale.Tests
{
    /// Play Mode smoke tests for Mossvale_Main. FarmClickMovement lives in Assembly-CSharp (not referenceable
    /// from an asmdef), so the tests drive it through reflection exactly as a mouse click would: by setting
    /// its destination + hasDestination fields.
    public class HitLogger : MonoBehaviour
    {
        readonly System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.collider.name != "Ground" && seen.Add(hit.collider.name))
                Debug.Log($"[MossvaleTest] player hit {hit.collider.name} at {hit.point}");
        }
    }

    public class MossvaleSceneTests
    {
        GameObject player;
        Component mover;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Application.targetFrameRate = 60; // batch mode otherwise runs at thousands of fps
            // Isolated save slot so tests never load or overwrite the player's real save.
            Mossvale.Farming.SaveManager.SlotOverride = "tests";
            Mossvale.Farming.SaveManager.DeleteSlot("tests");
            Mossvale.Farming.GameplayInput.Blocked = false;
            SceneManager.LoadScene("Mossvale_Main");
            yield return null;
            yield return null;
            player = GameObject.FindWithTag("Player");
            Assert.IsNotNull(player, "Player not found");
            mover = player.GetComponent("FarmClickMovement");
            Assert.IsNotNull(mover, "FarmClickMovement missing on Player");
        }

        IEnumerator WalkTo(Vector3 start, Vector3 target, float seconds)
        {
            var cc = player.GetComponent<CharacterController>();
            if (player.GetComponent<HitLogger>() == null) player.AddComponent<HitLogger>();
            cc.enabled = false;
            player.transform.position = start;
            cc.enabled = true;
            var t = mover.GetType();
            t.GetField("destination", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mover, new Vector3(target.x, start.y, target.z));
            t.GetField("hasDestination", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mover, true);
            float time = 0f;
            while (time < seconds) { time += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator ClickRaycastFromCameraHitsGround()
        {
            yield return null;
            var cam = Camera.main;
            Assert.IsNotNull(cam, "No MainCamera");
            var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.4f));
            Assert.IsTrue(Physics.Raycast(ray, out var hit, 200f), "Click ray hit nothing");
            Debug.Log("[MossvaleTest] centre click hit " + hit.collider.name + " at " + hit.point);
        }

        [UnityTest]
        public IEnumerator PlayerWalksUpTheEntranceRoadAndCameraFollows()
        {
            var start = player.transform.position;
            yield return WalkTo(start, start + new Vector3(-0.8f, 0f, 5.5f), 3f);
            float moved = Vector3.Distance(start, player.transform.position);
            Debug.Log($"[MossvaleTest] road walk moved {moved:F2} m to {player.transform.position}");
            Assert.Greater(moved, 4.0f, "Player did not walk along the road");
            Assert.Less(Mathf.Abs(player.transform.position.y - start.y), 0.2f, "Player height drifted");
            float camDist = Vector3.Distance(Camera.main.transform.position, player.transform.position);
            Assert.Less(camDist, 18f, "Camera is not following the player");
        }

        [UnityTest]
        public IEnumerator CottageBlocksThePlayer()
        {
            var start = new Vector3(-5f, 0.02f, 1f);
            yield return WalkTo(start, new Vector3(-5f, 0f, 9.8f), 5f);
            var p = player.transform.position;
            Debug.Log($"[MossvaleTest] cottage walk stopped at {p}");
            Assert.Greater(p.z, start.z + 1f, "Player should walk up the cottage path (gate is passable)");
            Assert.Less(p.z, 8.3f, "Player walked into the cottage");
            // Walk back down the garden path: gravity must bring the player back to ground level.
            yield return WalkTo(p, new Vector3(-5f, 0f, 1f), 3f);
            var back = player.transform.position;
            Debug.Log($"[MossvaleTest] walked back to {back}");
            Assert.Less(back.y, 0.15f, "Player is floating above the ground after leaving the porch");
        }

        [UnityTest]
        public IEnumerator PondBlocksThePlayer()
        {
            var start = new Vector3(-14.5f, 0.02f, -1.5f); // north bank, away from the dock
            yield return WalkTo(start, new Vector3(-14.5f, 0f, -10.3f), 5f);
            var p = player.transform.position;
            Debug.Log($"[MossvaleTest] pond walk stopped at {p}");
            Assert.Less(p.z, start.z - 2f, "Player should walk down to the bank");
            Assert.Greater(p.z, -6.5f, "Player walked into the pond");
        }
    }
}
