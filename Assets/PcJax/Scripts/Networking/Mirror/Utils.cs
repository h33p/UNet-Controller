using Mirror;

namespace PcJax.Networking.Mirror
{
    public class Utils
    {
        public static bool GetPlayerController(NetworkConnection conn, short playerControllerId, out PlayerController playerController)
        {
            playerController = conn.playerControllers.Find(pc => pc.IsValid && pc.playerControllerId == playerControllerId);
            return playerController != null;
        }
    }
}