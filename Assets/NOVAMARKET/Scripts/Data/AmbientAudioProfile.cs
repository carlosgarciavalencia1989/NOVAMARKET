using System.Collections.Generic;
using UnityEngine;

namespace NovaMarket.Data
{
    /// <summary>
    /// Music and sounds of the valley, and their volumes. From the strategy view only the soft background music
    /// plays; the street (engines, metro, beepers, wind) is heard only with the camera close to the ground. Every
    /// empty slot plays something made by the game (ProceduralMusic / ProceduralSounds); drop real CC0 tracks and
    /// recordings (Kenney packs) here when they arrive, nothing else changes.
    /// </summary>
    [CreateAssetMenu(menuName = MenuRoot + "Ambient Audio Profile", fileName = "AmbientAudio")]
    public sealed class AmbientAudioProfile : NovaDataAsset
    {
        [Header("Music (empty = calm pieces composed by the game)")]
        [Tooltip("Background tracks, played one after another with a pause between them (CC0 / licensed music).")]
        [SerializeField] private AudioClip[] music = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;
        [Tooltip("Seconds of silence between two pieces.")]
        [SerializeField, Min(0f)] private float musicPause = 8f;

        [Header("Street sounds, only heard close to the ground (empty = temporary synthesised sound)")]
        [Tooltip("Loop: wind in the open.")]
        [SerializeField] private AudioClip wind;
        [Tooltip("Loops: idle engines (the pitch rises with the speed).")]
        [SerializeField] private AudioClip carEngine;
        [SerializeField] private AudioClip motoEngine;
        [SerializeField] private AudioClip dieselEngine;
        [Tooltip("Loop: metro running on the viaduct.")]
        [SerializeField] private AudioClip metro;
        [Tooltip("Loop: reverse beeper (beep + silence).")]
        [SerializeField] private AudioClip reverseBeep;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float master = 0.8f;
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float metroVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float beepVolume = 0.45f;

        [Header("Voices")]
        [Tooltip("Vehicles heard at once (the nearest to the camera).")]
        [SerializeField, Range(0, 24)] private int vehicleVoices = 12;
        [Tooltip("Meters at which a vehicle, train or beeper can no longer be heard.")]
        [SerializeField, Min(20f)] private float hearingDistance = 220f;

        public IReadOnlyList<AudioClip> Music => music;
        public float MusicVolume => musicVolume;
        public float MusicPause => musicPause;
        public AudioClip Wind => wind;
        public AudioClip CarEngine => carEngine;
        public AudioClip MotoEngine => motoEngine;
        public AudioClip DieselEngine => dieselEngine;
        public AudioClip Metro => metro;
        public AudioClip ReverseBeep => reverseBeep;
        public float Master => master;
        public float WindVolume => windVolume;
        public float EngineVolume => engineVolume;
        public float MetroVolume => metroVolume;
        public float BeepVolume => beepVolume;
        public int VehicleVoices => vehicleVoices;
        public float HearingDistance => hearingDistance;
    }
}
