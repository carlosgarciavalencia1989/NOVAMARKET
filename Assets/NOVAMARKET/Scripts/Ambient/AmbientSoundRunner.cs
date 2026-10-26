using System.Collections.Generic;
using System.Threading.Tasks;
using NovaMarket.CameraControl;
using NovaMarket.Data;
using NovaMarket.Operation;
using NovaMarket.Simulation;
using NovaMarket.Simulation.Ambient;
using UnityEngine;
using Num = System.Numerics;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Sound of the scene in Play mode. From the strategy view only a soft background music plays (pieces composed
    /// by the game, <see cref="ProceduralMusic"/>, or the tracks of the profile). Close to the ground the street
    /// fades in (<see cref="AmbientSoundMix.NearGround"/>): wind, engines of the nearest vehicles, the metro and
    /// the reverse beepers of the CEDI, on a few pooled 3D voices that follow the nearest sources. Empty clips
    /// play temporary synthesised sounds (<see cref="ProceduralSounds"/>).
    /// </summary>
    public class AmbientSoundRunner : MonoBehaviour
    {
        private const float ReassignSeconds = 0.25f;
        private const float MetroHeight = 12f;
        private const int MetroVoices = 2;
        private const int BeepVoices = 2;
        private const int ComposedPieces = 3;
        private const float FirstPieceDelay = 2f;

        [SerializeField] private AmbientAudioProfile profile;
        [SerializeField] private AmbientTrafficRunner traffic;
        [SerializeField] private MetroRunner metro;
        [SerializeField] private CediOperationRunner operation;
        [SerializeField] private WindController wind;
        [SerializeField] private StrategyCameraController cameraController;
        [Tooltip("Where the ears are between the ground focus (0) and the camera (1): low values hear the street better.")]
        [SerializeField, Range(0f, 1f)] private float earsHeight = 0.2f;

        private sealed class Voice
        {
            public AudioSource Source;
            public object Target;
        }

        private readonly List<Voice> vehicleVoices = new List<Voice>();
        private readonly List<Voice> metroVoices = new List<Voice>();
        private readonly List<Voice> beepVoices = new List<Voice>();
        private readonly List<Num.Vector2> positions = new List<Num.Vector2>();
        private readonly List<object> targets = new List<object>();
        private readonly List<AudioClip> pieces = new List<AudioClip>();
        private readonly List<Task<float[]>> composing = new List<Task<float[]>>();
        private AudioSource musicSource;
        private AudioSource windSource;
        private AudioClip carClip, motoClip, dieselClip, metroClip, beepClip;
        private Transform ears;
        private float reassignTimer;
        private float musicPauseLeft = FirstPieceDelay;
        private int nextPiece;

        /// <summary>Voices playing now, music included (for the debug overlay).</summary>
        public int ActiveVoices { get; private set; }

        private void Start()
        {
            if (profile == null)
            {
                Debug.LogWarning("AmbientSoundRunner: assign the audio profile (NOVAMARKET → Ambient → Setup).");
                enabled = false;
                return;
            }
            SetUpEars();
            SetUpMusic();
            windSource = Loop2D("Wind", Clip(profile.Wind, "Wind", () => ProceduralSounds.Wind()));
            carClip = Clip(profile.CarEngine, "Engine_Car", () => ProceduralSounds.Engine(55f));
            motoClip = Clip(profile.MotoEngine, "Engine_Moto", () => ProceduralSounds.Engine(90f, roughness: 0.3f, seed: 6));
            dieselClip = Clip(profile.DieselEngine, "Engine_Diesel", () => ProceduralSounds.Engine(32f, roughness: 0.35f, seed: 7));
            metroClip = Clip(profile.Metro, "Metro", () => ProceduralSounds.Metro());
            beepClip = Clip(profile.ReverseBeep, "ReverseBeep", () => ProceduralSounds.ReverseBeep());

            for (int i = 0; i < profile.VehicleVoices; i++) vehicleVoices.Add(NewVoice("Vehicle"));
            for (int i = 0; i < MetroVoices; i++) metroVoices.Add(NewVoice("Metro"));
            for (int i = 0; i < BeepVoices; i++) beepVoices.Add(NewVoice("Beeper"));
        }

        private void Update()
        {
            if (ears == null) return;
            PlaceEars(out float cameraHeight);
            UpdateMusic();
            int music = musicSource.isPlaying ? 1 : 0;

            // The street is only heard close to the ground; from the strategy view it is silent.
            float street = profile.Master * AmbientSoundMix.NearGround(cameraHeight);
            float gust = wind != null && wind.Wind != null ? wind.Wind.Gust : 0.4f;
            windSource.volume = street * profile.WindVolume * AmbientSoundMix.WindVolume(gust);
            if (street <= 0.001f)
            {
                SilenceAll();
                ActiveVoices = music;
                return;
            }

            reassignTimer -= Time.deltaTime;
            if (reassignTimer <= 0f)
            {
                reassignTimer = ReassignSeconds;
                AssignVehicles();
                AssignMetro();
                AssignBeepers();
            }
            ActiveVoices = music + 1 + UpdateVehicles(street) + UpdateMetro(street) + UpdateBeepers(street);
        }

        // ------------------------------------------------------------------ music

        /// <summary>The profile tracks, or a few calm pieces composed on a worker thread (no hitch at start).</summary>
        private void SetUpMusic()
        {
            var go = new GameObject("Music");
            go.transform.SetParent(transform, false);
            musicSource = go.AddComponent<AudioSource>();
            musicSource.loop = false;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;

            foreach (AudioClip track in profile.Music)
                if (track != null) pieces.Add(track);
            if (pieces.Count > 0) return;
            int seed = Random.Range(0, 100000);
            for (int i = 0; i < ComposedPieces; i++)
            {
                int pieceSeed = seed + i * 7919;
                composing.Add(Task.Run(() => ProceduralMusic.Render(ProceduralMusic.Compose(pieceSeed))));
            }
        }

        /// <summary>Plays the pieces one after another, with a few seconds of silence between them.</summary>
        private void UpdateMusic()
        {
            for (int i = composing.Count - 1; i >= 0; i--)
            {
                Task<float[]> task = composing[i];
                if (!task.IsCompleted) continue;
                composing.RemoveAt(i);
                if (task.Status != TaskStatus.RanToCompletion) continue;
                float[] samples = task.Result;
                AudioClip clip = AudioClip.Create($"Music_Composed_{pieces.Count + 1}", samples.Length, 1,
                    ProceduralMusic.SampleRate, false);
                clip.SetData(samples, 0);
                pieces.Add(clip);
            }

            musicSource.volume = profile.Master * profile.MusicVolume;
            if (musicSource.isPlaying || pieces.Count == 0) return;
            musicPauseLeft -= Time.deltaTime;
            if (musicPauseLeft > 0f) return;
            musicSource.clip = pieces[nextPiece % pieces.Count];
            nextPiece++;
            musicSource.Play();
            musicPauseLeft = profile.MusicPause;
        }

        private void SilenceAll()
        {
            foreach (Voice voice in vehicleVoices) Silence(voice);
            foreach (Voice voice in metroVoices) Silence(voice);
            foreach (Voice voice in beepVoices) Silence(voice);
        }

        // ------------------------------------------------------------------ listener

        /// <summary>
        /// The ears sit low between the camera and the ground point it looks at, so the strategy view hears the
        /// street it shows (the camera's own listener is switched off while this runs).
        /// </summary>
        private void SetUpEars()
        {
            Camera main = Camera.main;
            if (main != null && main.TryGetComponent(out AudioListener cameraListener)) cameraListener.enabled = false;
            ears = new GameObject("Ears").transform;
            ears.SetParent(transform, false);
            ears.gameObject.AddComponent<AudioListener>();
        }

        private void PlaceEars(out float cameraHeight)
        {
            Camera main = Camera.main;
            Vector3 cameraPosition = main != null ? main.transform.position : Vector3.up * 150f;
            Vector3 focus = cameraController != null && cameraController.Rig != null ? cameraController.Rig.Focus : cameraPosition - Vector3.up * cameraPosition.y;
            ears.position = Vector3.Lerp(focus, cameraPosition, earsHeight);
            if (main != null) ears.rotation = main.transform.rotation;
            cameraHeight = cameraPosition.y;
        }

        private Num.Vector2 EarsGround => new Num.Vector2(ears.position.x, ears.position.z);

        // ------------------------------------------------------------------ vehicles

        private void AssignVehicles()
        {
            positions.Clear();
            targets.Clear();
            if (traffic != null && traffic.Traffic != null)
                foreach (AmbientVehicle v in traffic.Traffic.Vehicles)
                {
                    positions.Add(v.Position);
                    targets.Add(v);
                }
            Assign(vehicleVoices, profile.HearingDistance);
        }

        private int UpdateVehicles(float volume)
        {
            int playing = 0;
            foreach (Voice voice in vehicleVoices)
            {
                if (!(voice.Target is AmbientVehicle v) || traffic == null || traffic.Traffic == null || !Contains(traffic.Traffic.Vehicles, v))
                {
                    Silence(voice);
                    continue;
                }
                AudioClip clip = v.Kind switch
                {
                    AmbientVehicleKind.Moto => motoClip,
                    AmbientVehicleKind.Bus => dieselClip,
                    AmbientVehicleKind.Truck => dieselClip,
                    _ => carClip
                };
                Play(voice, clip);
                voice.Source.transform.position = new Vector3(v.Position.X, 1f, v.Position.Y);
                voice.Source.pitch = AmbientSoundMix.EnginePitch(v.Kind, v.Speed);
                voice.Source.volume = volume * profile.EngineVolume * AmbientSoundMix.EngineVolume(v.Kind, v.Speed);
                playing++;
            }
            return playing;
        }

        private static bool Contains(IReadOnlyList<AmbientVehicle> list, AmbientVehicle vehicle)
        {
            // Vehicles leave the simulation when they drive away from the camera; a removed one is silenced.
            for (int i = 0; i < list.Count; i++)
                if (list[i] == vehicle) return true;
            return false;
        }

        // ------------------------------------------------------------------ metro

        private void AssignMetro()
        {
            positions.Clear();
            targets.Clear();
            if (metro != null && metro.Metro != null)
                foreach (MetroTrain train in metro.Metro.Trains)
                {
                    positions.Add(train.Position);
                    targets.Add(train);
                }
            Assign(metroVoices, profile.HearingDistance * 2f);
        }

        private int UpdateMetro(float volume)
        {
            int playing = 0;
            foreach (Voice voice in metroVoices)
            {
                if (!(voice.Target is MetroTrain train))
                {
                    Silence(voice);
                    continue;
                }
                Play(voice, metroClip);
                voice.Source.transform.position = new Vector3(train.Position.X, MetroHeight, train.Position.Y);
                float run = Mathf.Clamp01(train.Speed / 20f);
                voice.Source.pitch = Mathf.Lerp(0.7f, 1.15f, run);
                voice.Source.volume = volume * profile.MetroVolume * Mathf.Lerp(0.15f, 1f, run);
                playing++;
            }
            return playing;
        }

        // ------------------------------------------------------------------ CEDI reverse beepers

        private void AssignBeepers()
        {
            positions.Clear();
            targets.Clear();
            if (operation != null && operation.Simulation != null)
                foreach (VehicleAgent agent in operation.Simulation.Vehicles)
                    if (agent.IsReversing)
                    {
                        positions.Add(agent.Position);
                        targets.Add(agent);
                    }
            Assign(beepVoices, profile.HearingDistance);
        }

        private int UpdateBeepers(float volume)
        {
            int playing = 0;
            foreach (Voice voice in beepVoices)
            {
                if (!(voice.Target is VehicleAgent agent) || !agent.IsReversing)
                {
                    Silence(voice);
                    continue;
                }
                Play(voice, beepClip);
                voice.Source.transform.position = new Vector3(agent.Position.X, 2f, agent.Position.Y);
                voice.Source.volume = volume * profile.BeepVolume;
                playing++;
            }
            return playing;
        }

        // ------------------------------------------------------------------ voices

        /// <summary>Gives the voices to the nearest targets; a voice that keeps its target keeps playing without a restart.</summary>
        private void Assign(List<Voice> voices, float radius)
        {
            List<int> nearest = AmbientSoundMix.Nearest(positions, EarsGround, voices.Count, radius);
            var wanted = new List<object>(nearest.Count);
            foreach (int i in nearest) wanted.Add(targets[i]);

            foreach (Voice voice in voices)
                if (voice.Target != null && !wanted.Remove(voice.Target)) voice.Target = null;
            foreach (Voice voice in voices)
            {
                if (wanted.Count == 0) break;
                if (voice.Target != null) continue;
                voice.Target = wanted[0];
                wanted.RemoveAt(0);
            }
        }

        private Voice NewVoice(string kind)
        {
            var go = new GameObject($"Voice_{kind}");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 6f;
            source.maxDistance = profile.HearingDistance * (kind == "Metro" ? 2f : 1f);
            source.dopplerLevel = 0f;
            source.spread = 60f;
            return new Voice { Source = source };
        }

        private static void Play(Voice voice, AudioClip clip)
        {
            AudioSource source = voice.Source;
            if (source.clip != clip)
            {
                source.clip = clip;
                source.Stop();
            }
            if (!source.isPlaying)
            {
                source.time = Random.Range(0f, clip.length * 0.9f); // engines side by side do not pulse together
                source.Play();
            }
        }

        private static void Silence(Voice voice)
        {
            voice.Target = null;
            if (voice.Source.isPlaying) voice.Source.Stop();
        }

        private AudioSource Loop2D(string sourceName, AudioClip clip)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            return source;
        }

        private static AudioClip Clip(AudioClip assigned, string clipName, System.Func<float[]> synthesise)
        {
            if (assigned != null) return assigned;
            float[] samples = synthesise();
            AudioClip clip = AudioClip.Create($"Placeholder_{clipName}", samples.Length, 1, ProceduralSounds.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
