// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xenko.Core;
using Xenko.Engine;
using Xenko.Games;
using Xenko.Physics.Bepu;

namespace Xenko.Physics
{
    public class PhysicsSystem : GameSystem, IPhysicsSystem
    {
        private class PhysicsScene
        {
            public PhysicsProcessor Processor;
            public Simulation Simulation;
            public BepuSimulation BepuSimulation;
        }

        internal static volatile float timeToSimulate;
        private bool runThread;
        private ManualResetEventSlim doUpdateEvent;
        private Thread physicsThread;

        private readonly List<PhysicsScene> scenes = new List<PhysicsScene>();

        public PhysicsSystem(IServiceRegistry registry)
            : base(registry)
        {
            UpdateOrder = -1000; //make sure physics runs before everything

            Enabled = true; //enabled by default
        }

        private PhysicsSettings physicsConfiguration;

        public bool isMultithreaded
        {
            get
            {
                return ((physicsConfiguration?.Flags ?? 0) & PhysicsEngineFlags.MultiThreaded) != 0;
            }
        }

        public override void Initialize()
        {
            physicsConfiguration = Game?.Settings != null ? Game.Settings.Configurations.Get<PhysicsSettings>() : new PhysicsSettings();

            if (isMultithreaded)
            {
                doUpdateEvent = new ManualResetEventSlim(false);
                runThread = true;
                physicsThread = new Thread(new ThreadStart(PhysicsProcessingThreadBody));
                physicsThread.Name = "BulletPhysics Processing Thread";
                physicsThread.IsBackground = true;
                physicsThread.Start();
            }
        }

        private void EndThread()
        {
            runThread = false;
            if (physicsThread != null && physicsThread.IsAlive)
                physicsThread.Join(5000);
        }

        protected override void Destroy()
        {
            EndThread();

            base.Destroy();

            foreach (var scene in scenes)
            {
                scene.Simulation?.Dispose();
                scene.BepuSimulation?.Dispose();
            }
        }

        public object Create(PhysicsProcessor sceneProcessor, PhysicsEngineFlags flags = PhysicsEngineFlags.None, bool bepu = false)
        {
            var scene = new PhysicsScene
            { 
                Processor = sceneProcessor,
                Simulation = bepu == false ? new Simulation(sceneProcessor, physicsConfiguration) : null,
                BepuSimulation = bepu ? new BepuSimulation(physicsConfiguration) : null
            };
            scenes.Add(scene);
            return bepu ? (object)scene.BepuSimulation : (object)scene.Simulation;
        }

        public bool HasSimulation<T>()
        {
            for (int i=0; i<scenes.Count; i++)
            {
                if (scenes[i].Simulation is T s && s != null) return true;
                if (scenes[i].BepuSimulation is T bs && bs != null) return true;
            }
            return false;
        }

        public void Release(PhysicsProcessor processor)
        {
            EndThread();

            var scene = scenes.SingleOrDefault(x => x.Processor == processor);
            if (scene == null) return;

            scenes.Remove(scene);
            scene.Simulation?.Dispose();
            scene.BepuSimulation?.Dispose();
        }

        private void RunPhysicsSimulation(float time)
        {
            //read skinned meshes bone positions
            for (int i=0; i<scenes.Count; i++)
            {
                var physicsScene = scenes[i];

                if (physicsScene.Simulation != null)
                {
                    //first process any needed cleanup
                    physicsScene.Processor.UpdateRemovals();

                    // after we took care of cleanup, are we disabled?
                    if (Simulation.DisableSimulation == false)
                    {
                        //read skinned meshes bone positions and write them to the physics engine
                        physicsScene.Processor.UpdateBones();

                        //simulate physics
                        physicsScene.Simulation.Simulate(time);

                        //update character bound entity's transforms from physics engine simulation
                        physicsScene.Processor.UpdateCharacters();

                        //Perform clean ups before test contacts in this frame
                        physicsScene.Simulation.BeginContactTesting();

                        //handle frame contacts
                        physicsScene.Processor.UpdateContacts();

                        //This is the heavy contact logic
                        physicsScene.Simulation.EndContactTesting();

                        //send contact events
                        physicsScene.Simulation.SendEvents();
                    }
                } 

                if (physicsScene.BepuSimulation != null)
                {
                    // remove all bodies set to be removed
                    physicsScene.BepuSimulation.ProcessRemovals();

                    // did we request a clear?
                    int clearMode = physicsScene.BepuSimulation.clearRequested;
                    if (clearMode > 0) physicsScene.BepuSimulation.Clear(clearMode == 2, true);

                    // add everyone waiting (which could have been something just removed)
                    physicsScene.BepuSimulation.ProcessAdds();

                    if (Simulation.DisableSimulation == false)
                    {
                        // simulate!
                        physicsScene.BepuSimulation.Simulate(time);

                        // update all rigidbodies
                        for (int j = 0; j < physicsScene.BepuSimulation.AllRigidbodies.Count; j++)
                        {
                            // are we still in the scene?
                            BepuRigidbodyComponent rb = physicsScene.BepuSimulation.AllRigidbodies[j];

                            rb.swapProcessingContactsList();

                            // per-rigidbody update
                            if (rb.ActionPerSimulationTick != null)
                                rb.ActionPerSimulationTick(rb, time);

                            rb.UpdateTransformationComponent();
                        }
                    }
                }
            }
        }

        private void PhysicsProcessingThreadBody()
        {
            while (runThread)
            {
                if (doUpdateEvent.Wait(1000) && timeToSimulate > 0f)
                {
                    float simulateThisInterval = timeToSimulate;
                    timeToSimulate -= simulateThisInterval;

                    RunPhysicsSimulation(simulateThisInterval);
                }

                doUpdateEvent.Reset();
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (isMultithreaded)
            {
                timeToSimulate += (float)gameTime.Elapsed.TotalSeconds;
                doUpdateEvent.Set();
            } 
            else
            {
                RunPhysicsSimulation((float)gameTime.Elapsed.TotalSeconds);
            }
        }
    }
}
