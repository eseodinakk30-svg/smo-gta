using UnityEngine;
using SanMonica.Core;

namespace SanMonica.UI
{
    /// <summary>
    /// On-screen readout of the things that decide whether the player has a
    /// floor. Two rounds of diagnosing "no floor at spawn" by reading the source
    /// were wrong, and the world generator was proven correct offline, so this
    /// reports what the running game actually sees on the device: whether the
    /// tile under the player carries a collider, what a downward ray hits, and
    /// where the player is relative to the terrain.
    ///
    /// IMGUI on purpose - it does not depend on the game's own UI, so it still
    /// draws if that is the part that is broken.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        public static bool Visible = true;

        private GUIStyle _style;
        private string _text = "";
        private float _timer;

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.25f;
            _text = Compose();
        }

        private string Compose()
        {
            var game = Services.Game;
            var streamer = Services.Streamer;
            var player = Services.Player;
            var map = Services.Map;

            var sb = new System.Text.StringBuilder(512);
            sb.Append("San Monica diagnostics\n");
            sb.Append("world ready: game=").Append(game != null && game.WorldReady)
              .Append("  streamer=").Append(streamer != null && streamer.WorldReady).Append('\n');

            if (streamer != null)
                sb.Append("chunks: loaded=").Append(streamer.LoadedChunks)
                  .Append(" pending=").Append(streamer.PendingChunks)
                  .Append(" withGround=").Append(streamer.ChunksWithGround).Append('\n');

            if (player == null) { sb.Append("player: none"); return sb.ToString(); }

            Vector3 p = player.transform.position;
            sb.Append("player: ").Append(p.ToString("0.0"))
              .Append("\n  grounded=").Append(player.IsGrounded)
              .Append(" frozen=").Append(player.Frozen)
              .Append(" inVehicle=").Append(player.InVehicle).Append('\n');

            if (map != null)
            {
                float terrain = map.SampleHeight(p.x, p.z);
                sb.Append("terrain y=").Append(terrain.ToString("0.00"))
                  .Append("  player is ").Append((p.y - terrain).ToString("0.00")).Append(" above\n");
            }

            var chunk = streamer != null ? streamer.ChunkAt(p) : null;
            sb.Append("tile under player: ");
            if (chunk == null) sb.Append("NOT LOADED\n");
            else sb.Append(chunk.Coord).Append(" lod=").Append(chunk.Lod)
                   .Append(" floor=").Append(chunk.HasGroundCollider).Append('\n');

            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 500f,
                                GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                sb.Append("ray down: hit '").Append(hit.collider.name)
                  .Append("' layer=").Append(hit.collider.gameObject.layer)
                  .Append(" y=").Append(hit.point.y.ToString("0.00"))
                  .Append(" dist=").Append(hit.distance.ToString("0.00"));
            else
                sb.Append("ray down: NOTHING within 500 m");

            return sb.ToString();
        }

        private void OnGUI()
        {
            if (!Visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(14, Screen.height / 42),
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = false
                };
                _style.normal.textColor = Color.white;
            }

            float w = Screen.width * 0.62f, h = Screen.height * 0.42f;
            var box = new Rect(12f, 12f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 10f, box.y + 8f, box.width - 20f, box.height - 16f), _text, _style);
        }
    }
}
