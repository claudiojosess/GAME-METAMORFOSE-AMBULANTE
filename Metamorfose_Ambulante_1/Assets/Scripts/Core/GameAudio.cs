using UnityEngine;
namespace AeroMorphET
{
    public static class GameAudio
    {
        public static void Play(string name)
        {
            var clip = Resources.Load<AudioClip>("Sounds/" + name);
            if (clip == null) return;
            var camera = Camera.main;
            AudioSource.PlayClipAtPoint(clip, camera != null ? camera.transform.position : Vector3.zero, .35f);
        }
    }
}
