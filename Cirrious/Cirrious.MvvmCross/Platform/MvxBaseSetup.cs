#region Copyright
// <copyright file="MvxBaseSetup.cs" company="Cirrious">
// (c) Copyright Cirrious. http://www.cirrious.com
// This source is subject to the Microsoft Public License (Ms-PL)
// Please see license.txt on http://opensource.org/licenses/ms-pl.html
// All other rights reserved.
// </copyright>
// 
// Project Lead - Stuart Lodge, Cirrious. http://www.cirrious.com
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cirrious.MvvmCross.Application;
using Cirrious.MvvmCross.Core;
using Cirrious.MvvmCross.ExtensionMethods;
using Cirrious.MvvmCross.Interfaces.Application;
using Cirrious.MvvmCross.Interfaces.IoC;
using Cirrious.MvvmCross.Interfaces.Plugins;
using Cirrious.MvvmCross.Interfaces.ServiceProvider;
using Cirrious.MvvmCross.Interfaces.ViewModels;
using Cirrious.MvvmCross.Interfaces.Views;
using Cirrious.MvvmCross.IoC;
using Cirrious.MvvmCross.Platform.Diagnostics;
using Cirrious.MvvmCross.Views;
using Cirrious.MvvmCross.Views.Attributes;

namespace Cirrious.MvvmCross.Platform
{
    public abstract class MvxBaseSetup
        : IMvxServiceProducer<IMvxViewsContainer>
        , IMvxServiceProducer<IMvxViewDispatcherProvider>
        , IMvxServiceProducer<IMvxViewModelLocatorFinder>
        , IMvxServiceProducer<IMvxViewModelLocatorAnalyser>
        , IMvxServiceProducer<IMvxViewModelLocatorStore>
        , IMvxServiceConsumer<IMvxViewsContainer>
        , IMvxServiceProducer<IMvxViewModelLoader>
        , IMvxServiceProducer<IMvxPluginManager>
        , IMvxServiceProducer<IMvxServiceProviderRegistry>
        , IMvxServiceProducer<IMvxServiceProvider>
        , IDisposable
    {
        #region some cleanup code - especially for test harness use

        ~MvxBaseSetup()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                MvxSingleton.ClearAllSingletons();
            }
        }

        #endregion Some cleanup code - especially for test harness use

        public virtual void Initialize()
        {
            InitializePrimary();
            InitializeSecondary();
        }

        public virtual void InitializePrimary()
        {
            MvxTrace.Trace("Setup: Primary start");
            InitializeIoC();
            MvxTrace.Trace("Setup: FirstChance start");
            InitializeFirstChance();
            MvxTrace.Trace("Setup: PlatformServices start");
            InitializePlatformServices();
            MvxTrace.Trace("Setup: DebugServices start");
            InitializeDebugServices();
        }

        public virtual void InitializeSecondary()
        {
            MvxTrace.Trace("Setup: ViewModelFramework start");
            InitializeViewModelFramework();
            MvxTrace.Trace("Setup: PluginManagerFramework start");
            InitializePluginFramework();
            MvxTrace.Trace("Setup: App start");
            InitializeApp();
            MvxTrace.Trace("Setup: ViewsContainer start");
            InitializeViewsContainer();
            MvxTrace.Trace("Setup: ViewDispatcherProvider start");
            InitiaiseViewDispatcherProvider();
            MvxTrace.Trace("Setup: Views start");
            InitializeViews();
            MvxTrace.Trace("Setup: LastChance start");
            InitializeLastChance();
            MvxTrace.Trace("Setup: Secondary end");
        }

        protected virtual void InitializeIoC()
        {
            // initialise the IoC service registry
            var iocProvider = CreateIocProvider();
            var serviceProvider = new MvxServiceProvider(iocProvider);
            this.RegisterServiceInstance<IMvxServiceProviderRegistry>(serviceProvider);
            this.RegisterServiceInstance<IMvxServiceProvider>(serviceProvider);
        }

        protected virtual IMvxIoCProvider CreateIocProvider()
        {
            return new MvxOpenNetCfIocServiceProvider();
        }

        protected virtual void InitializeFirstChance()
        {
            // always the very first thing to get initialized - after IoC and base platfom 
            // base class implementation is empty by default
        }

        protected virtual void InitializePlatformServices()
        {
            // do nothing by default
        }

        protected virtual void InitializeDebugServices()
        {
            MvxTrace.Initialize();
        }

        protected virtual void InitializeViewModelFramework()
        {
            this.RegisterServiceType<IMvxViewModelLoader, MvxViewModelLoader>();
            this.RegisterServiceType<IMvxViewModelLocatorAnalyser, MvxViewModelLocatorAnalyser>();
        }

        protected virtual void InitializePluginFramework()
        {
            this.RegisterServiceInstance<IMvxPluginManager>(CreatePluginManager());
        }

        protected abstract IMvxPluginManager CreatePluginManager();

        protected virtual void InitializeApp()
        {
            var app = CreateApp();
            this.RegisterServiceInstance<IMvxViewModelLocatorFinder>(app);
            this.RegisterServiceInstance<IMvxViewModelLocatorStore>(app);
        }

        protected abstract MvxApplication CreateApp();

        protected virtual void InitializeViewsContainer()
        {
            var container = CreateViewsContainer();
            this.RegisterServiceInstance<IMvxViewsContainer>(container);
        }

        protected virtual void InitiaiseViewDispatcherProvider()
        {
            var provider = CreateViewDispatcherProvider();
            this.RegisterServiceInstance<IMvxViewDispatcherProvider>(provider);
        }

        protected abstract MvxViewsContainer CreateViewsContainer();

        protected virtual void InitializeViews()
        {
            var container = this.GetService<IMvxViewsContainer>();

            foreach (var pair in GetViewModelViewLookup())
            {
                Add(container, pair.Key, pair.Value);
            }
        }

        protected abstract IDictionary<Type, Type> GetViewModelViewLookup();
        protected abstract IMvxViewDispatcherProvider CreateViewDispatcherProvider();

#if NETFX_CORE
        protected virtual IDictionary<Type, Type> GetViewModelViewLookup(Assembly assembly, Type expectedInterfaceType)
        {
            var views = from type in assembly.DefinedTypes
                        where !type.IsAbstract
                              && expectedInterfaceType.GetTypeInfo().IsAssignableFrom(type)
                              && !type.Name.StartsWith("Base")
                        let viewModelPropertyInfo = type.RecursiveGetDeclaredProperty("ViewModel")
                        where viewModelPropertyInfo != null
                        let viewModelType = viewModelPropertyInfo.PropertyType
                        select new {type, viewModelType};

            return views.ToDictionary(x => x.viewModelType, x => x.type.AsType());
        }

#warning Need to add unconventionalattributes to winrt code
#else
        protected virtual IDictionary<Type, Type> GetViewModelViewLookup(Assembly assembly, Type expectedInterfaceType)
        {
            var views = from type in assembly.GetTypes()
                        let viewModelType = GetViewModelTypeMappingIfPresent(type, expectedInterfaceType)
                        where viewModelType != null
                        select new { type, viewModelType };

            return views.ToDictionary(x => x.viewModelType, x => x.type);
        }

        protected virtual Type GetViewModelTypeMappingIfPresent(Type candidateType, Type expectedInterfaceType)
        {
            if (candidateType == null)
                return null;

            if (!expectedInterfaceType.IsAssignableFrom(candidateType))
                return null;

            if (candidateType.Name.StartsWith("Base"))
                return null;

            var unconventionalAttributes = candidateType.GetCustomAttributes(typeof(MvxUnconventionalViewAttribute), true);
            if (unconventionalAttributes.Length > 0)
                return null;

            var conditionalAttributes = candidateType.GetCustomAttributes(typeof(MvxConditionalConventionalViewAttribute), true);
            foreach (MvxConditionalConventionalViewAttribute conditional in conditionalAttributes)
            {
                var result = conditional.IsConventional;
                if (!result)
                    return null;
            }

            var viewModelPropertyInfo = candidateType.GetProperty("ViewModel");
            if (viewModelPropertyInfo == null)
                return null;

            return viewModelPropertyInfo.PropertyType;
        }
#endif

        protected virtual void InitializeLastChance()
        {
            // always the very last thing to get initialized
            // base class implementation is empty by default
        }

        protected void Add<TViewModel, TView>(IMvxViewsContainer container)
            where TViewModel : IMvxViewModel
        {
            container.Add(typeof(TViewModel), typeof(TView));
        }

        protected void Add(IMvxViewsContainer container, Type viewModelType, Type viewType)
        {
            container.Add(viewModelType, viewType);
        }
    }
}