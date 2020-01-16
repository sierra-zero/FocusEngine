// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Xenko.Audio;
using Xenko.Core;
using Xenko.Core.Annotations;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Engine.Design;
using Xenko.Games;

namespace Xenko.Engine
{
    /// <summary>
    /// Efficiently plays and manages sound effects for an entire project
    /// </summary>
    [Display("Global Sound Manager", Expand = ExpandRule.Once)]
    [DataContract("GlobalSoundManager")]
    [ComponentOrder(7500)]
    [ComponentCategory("Audio")]
    public sealed class GlobalSoundManager : ActivableEntityComponent
    {
        private struct PositionalSound
        {
            public SoundInstance soundInstance;
            public Entity entity;
            public Vector3 pos;
            public float distance_scale;
        }

        [DataMember]
        public float MaxSoundDistance = 48f;

        [DataMember]
        public int MaxSameSoundOverlaps = 8;

        [DataMember]
        public float MasterVolume = 1f;

        public SoundInstance PlayCentralSound(string url, float pitch = 1f, float volume = 1f, float pan = 0.5f, bool looped = false)
        {
            SoundInstance s = getFreeInstance(url, false);
            if (s != null)
            {
                s.Pitch = pitch < 0f ? RandomPitch() : pitch;
                s.Volume = volume * MasterVolume;
                s.IsLooping = looped;
                s.Pan = pan;
                s.Play();
            }
            return s;
        }

        public SoundInstance PlayPositionSound(string url, Vector3 position, float pitch = 1f, float volume = 1f, float pan = 0.5f, float distanceScale = 1f, bool looped = false)
        {
            float sqrDist = (position - Listener.Listener.Position).LengthSquared();
            if (MaxSoundDistance > 0f && sqrDist >= MaxSoundDistance * MaxSoundDistance) return null;
            SoundInstance s = getFreeInstance(url, true);
            if (s == null) return null;
            s.Pitch = pitch < 0f ? RandomPitch() : pitch;
            s.Volume = volume * MasterVolume;
            s.IsLooping = looped;
            s.Pan = pan;
            s.Apply3D(position, null, null, distanceScale);
            s.Play();
            return s;
        }

        public SoundInstance PlayAttachedSound(string url, Entity parent, float pitch = 1f, float volume = 1f, float pan = 0.5f, float distanceScale = 1f, bool looped = false)
        {
            Vector3 pos = parent.Transform.WorldPosition();
            float sqrDist = (pos - Listener.Listener.Position).LengthSquared();
            if (MaxSoundDistance > 0f && sqrDist >= MaxSoundDistance * MaxSoundDistance) return null;
            SoundInstance s = getFreeInstance(url, true);
            if (s == null) return null;
            s.Pitch = pitch < 0f ? RandomPitch() : pitch;
            s.Volume = volume * MasterVolume;
            s.IsLooping = looped;
            s.Pan = pan;
            s.Apply3D(pos, null, null, distanceScale);
            s.Play();
            currentAttached.Add(new PositionalSound()
            {
                pos = pos,
                soundInstance = s,
                entity = parent,
                distance_scale = distanceScale
            });
            return s;
        }

        public void UpdatePlayingSoundPositions()
        {
            for (int i = 0; i < currentAttached.Count; i++)
            {
                PositionalSound ps = currentAttached[i];
                if (ps.entity.Scene == null)
                {
                    ps.soundInstance.Stop();
                    currentAttached.RemoveAt(i);
                    i--;
                    continue;
                }
                else if (ps.soundInstance.PlayState == Media.PlayState.Stopped)
                {
                    currentAttached.RemoveAt(i);
                    i--;
                    continue;
                }
                Vector3 newpos = ps.entity.Transform.WorldPosition();
                ps.soundInstance.Apply3D(newpos, newpos - ps.pos, null, ps.distance_scale);
                ps.pos = newpos;
            }
        }

        public void StopAllSounds()
        {
            foreach (List<SoundInstance> si in instances.Values)
            {
                for (int i = 0; i < si.Count; i++)
                {
                    si[i].Stop();
                }
            }
            currentAttached.Clear();
        }

        public void StopSound(string url)
        {
            if (instances.TryGetValue(url, out var snds))
            {
                for (int i = 0; i < snds.Count; i++)
                {
                    snds[i].Stop();
                }
            }
        }

        public float RandomPitch(float range = 0.2f, float middle = 1f)
        {
            if (rand == null) rand = new Random(Environment.TickCount);
            return (float)rand.NextDouble() * range * 2f + middle - range;
        }

        public void Reset()
        {
            StopAllSounds();
            Sounds.Clear();
            foreach (List<SoundInstance> si in instances.Values)
            {
                for (int i = 0; i < si.Count; i++)
                {
                    si[i].Dispose();
                }
                si.Clear();
            }
            instances.Clear();
        }

        private Game game
        {
            get
            {
                // don't have this sound... try loading it!
                if (internalGame == null)
                    internalGame = ServiceRegistry.instance?.GetService<IGame>() as Game;

                return internalGame;
            }
        }

        [DataMemberIgnore]
        private AudioListenerComponent Listener
        {
            get
            {
                Game g = game;

                if (g == null)
                    throw new InvalidOperationException("No Game object has been fully initialized yet!");

                if (_listener == null || _listener.Enabled == false || g.Audio.Listeners.ContainsKey(_listener) == false)
                {
                    // find a valid listener!
                    foreach (AudioListenerComponent alc in g.Audio.Listeners.Keys)
                    {
                        if (alc.Enabled)
                        {
                            _listener = alc;
                            break;
                        }
                    }

                    if (_listener == null)
                        throw new InvalidOperationException("Could not find an Audio Listener Component in scene!");
                }
                return _listener;
            }
            set
            {
                // don't set us to something null, which just breaks things
                if (value == null) return;

                _listener = value;
            }
        }

        public void OverrideListener(AudioListenerComponent listener)
        {
            Listener = listener;
        }

        private Dictionary<string, Sound> Sounds = new Dictionary<string, Sound>();
        private Dictionary<string, List<SoundInstance>> instances = new Dictionary<string, List<SoundInstance>>();
        private List<PositionalSound> currentAttached = new List<PositionalSound>();
        private System.Random rand;
        private Game internalGame;
        private AudioListenerComponent _listener;

        private SoundInstance getFreeInstance(string url, bool spatialized)
        {
            if (url == null) return null;

            if (instances.TryGetValue(url, out var ins))
            {
                for (int i=0; i<ins.Count; i++)
                {
                    if (ins[i].PlayState == Media.PlayState.Stopped)
                        return ins[i];
                }

                // have we reached our max sounds though?
                if (ins.Count >= MaxSameSoundOverlaps) return null;

                // don't have a free one to play, add a new one to the list
                if (Sounds.TryGetValue(url, out var snd0))
                {
                    SoundInstance si0 = snd0.CreateInstance(Listener?.Listener, true, false, 0f, HrtfEnvironment.Small);
                    ins.Add(si0);
                    return si0;
                }
            }

            // don't have a list for this, make one
            if (Sounds.TryGetValue(url, out var snd1))
            {
                SoundInstance si1 = snd1.CreateInstance(Listener?.Listener, true, false, 0f, HrtfEnvironment.Small);
                List<SoundInstance> lsi1 = new List<SoundInstance>();
                lsi1.Add(si1);
                instances[url] = lsi1;
                return si1;
            }

            // this might throw an exception if you provided a bad url
            Sound snd2 = game.Content.Load<Sound>(url);

            if (!snd2.Spatialized && spatialized)
                throw new InvalidOperationException("Trying to play " + url + " positionally, yet it is a non-spatialized sound!");

            SoundInstance si = snd2.CreateInstance(Listener?.Listener, true, false, 0f, HrtfEnvironment.Small);
            List<SoundInstance> lsi = new List<SoundInstance>();
            lsi.Add(si);
            instances[url] = lsi;
            Sounds[url] = snd2;
            return si;
        }
    }
}
