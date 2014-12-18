﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using TuesPechkin.EventHandlers;
using ErrorEventHandler = TuesPechkin.EventHandlers.ErrorEventHandler;
using SysAssembly = System.Reflection.Assembly;
using SysPath = System.IO.Path;

namespace TuesPechkin
{
    /// <summary>
    /// Deployments loaded with this class must be marked Serializable.
    /// </summary>
    /// <typeparam name="TToolset">The type of toolset to manage remotely.</typeparam>
    public class RemotingToolset<TToolset> : NestingToolset
        where TToolset : MarshalByRefObject, IToolset, new()
    {
        public RemotingToolset(IDeployment deployment)
        {
            if (deployment == null)
            {
                throw new ArgumentNullException("deployment");
            }

            Deployment = deployment;
        }

        public override void Load(IDeployment deployment = null)
        {
            if (Loaded)
            {
                return;
            }

            if (deployment != null)
            {
                Deployment = deployment;
            }

            SetupAppDomain();

            var handle = Activator.CreateInstanceFrom(
                remoteDomain,
                typeof(TToolset).Assembly.Location,
                typeof(TToolset).FullName,
                false,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                null,
                null,
                null,
                null);

            NestedToolset = handle.Unwrap() as IToolset;
            NestedToolset.Load(Deployment);
            Deployment = NestedToolset.Deployment;

            Loaded = true;
        }

        public void Unload()
        {
            TearDownAppDomain(null, EventArgs.Empty);
        }

        private AppDomain remoteDomain;

        private void SetupAppDomain()
        {
            var setup = AppDomain.CurrentDomain.SetupInformation;

            setup.LoaderOptimization = LoaderOptimization.SingleDomain;

            remoteDomain = AppDomain.CreateDomain(
                string.Format("tuespechkin_{0}", Guid.NewGuid()),
                null,
                setup);

            if (AppDomain.CurrentDomain.IsDefaultAppDomain() == false)
            {
                AppDomain.CurrentDomain.DomainUnload += TearDownAppDomain;
            }
        }

        private void TearDownAppDomain(object sender, EventArgs e)
        {
            AppDomain.Unload(remoteDomain);

            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                var expected = Path.Combine(
                    Deployment.Path, 
                    Path.GetFileName(module.FileName));

                if (module.FileName == expected)
                {
                    while (WinApiHelper.FreeLibrary(module.BaseAddress))
                    {
                    }
                }
            }
        }
    }
}