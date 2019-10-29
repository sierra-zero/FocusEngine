// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xenko.Core;
using Xenko.Engine;
using Xenko.Games;

namespace Xenko.Physics
{
    public class Bullet2PhysicsSystem : GameSystem, IPhysicsSystem
    {
        private class PhysicsScene
        {
            public PhysicsProcessor Processor;
            public Simulation Simulation;
        }

        internal static volatile float timeToSimulate;
        private bool runThread;
        private ManualResetEventSlim doUpdateEvent;
        private Thread physicsThread;

        private readonly List<PhysicsScene> scenes = new List<PhysicsScene>();

        static Bullet2PhysicsSystem()
        {
        }

        public Bullet2PhysicsSystem(IServiceRegistry registry)
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
                scene.Simulation.Dispose();
            }
        }

        public Simulation Create(PhysicsProcessor sceneProcessor, PhysicsEngineFlags flags = PhysicsEngineFlags.None)
        {
            var scene = new PhysicsScene { Processor = sceneProcessor, Simulation = new Simulation(sceneProcessor, physicsConfiguration) };
            scenes.Add(scene);
            return scene.Simulation;
        }

        public void Release(PhysicsProcessor processor)
        {
            EndThread();

            var scene = scenes.SingleOrDefault(x => x.Processor == processor);
            if (scene == null) return;

            scenes.Remove(scene);
            scene.Simulation.Dispose();
        }

        private void RunPhysicsSimulation(float time)
        {
            //read skinned meshes bone positions
            for (int i=0; i<scenes.Count; i++)
            {
                var physicsScene = scenes[i];

                //first process any needed cleanup
                physicsScene.Processor.UpdateRemovals();

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
            if (Simulation.DisableSimulation) return;

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
