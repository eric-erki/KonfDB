﻿#region License and Product Information

// 
//     This file 'KonfDBService.cs' is part of KonfDB application - 
//     a project perceived and developed by Punit Ganshani.
// 
//     KonfDB is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     KonfDB is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with KonfDB.  If not, see <http://www.gnu.org/licenses/>.
// 
//     You can also view the documentation and progress of this project 'KonfDB'
//     on the project website, <http://www.konfdb.com> or on 
//     <http://www.ganshani.com/applications/konfdb>

#endregion

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.ServiceProcess;
using System.Threading;
using KonfDB.Engine.Services;
using KonfDB.Infrastructure.Database.Providers;
using KonfDB.Infrastructure.Enums;
using KonfDB.Infrastructure.Extensions;
using KonfDB.Infrastructure.Services;
using KonfDB.Infrastructure.Shell;
using KonfDB.Infrastructure.WCF;
using KonfDB.Infrastructure.WCF.Bindings;
using KonfDB.Infrastructure.WCF.Interfaces;

namespace KonfDBHost
{
    public class KonfDBH : ServiceBase
    {
        private Thread _thread;
        private ManualResetEvent _shutdownEvent;
        private WcfService<ICommandService<object>, NativeCommandService> _serviceHostNative;
        private WcfService<ICommandService<string>, JsonCommandService> _serviceHostJson;
        public ServiceCore ServiceFacade;
        public string AuthenticationToken;

        protected override void OnStart(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            InitDatabase();

            #region Run Command Service

            ServiceFacade = new ServiceCore();

            // Ensure that the super user admin exists
            ServiceFacade.ExecuteCommand(String.Format("NewUser /name:{0} /pwd:{1} /cpwd:{1} /role:admin /silent",
                AppContext.Current.Config.Runtime.SuperUser.Username,
                AppContext.Current.Config.Runtime.SuperUser.Password), null);

            // Ensure that the super user readonly exists
            ServiceFacade.ExecuteCommand(
                String.Format("NewUser /name:{0}_ro /pwd:{1} /cpwd:{1} /role:readonly /silent",
                    AppContext.Current.Config.Runtime.SuperUser.Username,
                    AppContext.Current.Config.Runtime.SuperUser.Password), null);

            var serviceConfig = AppContext.Current.Config.Runtime.Server;
            _serviceHostNative = new WcfService<ICommandService<object>, NativeCommandService>("localhost",
                "CommandService");
            _serviceHostJson = new WcfService<ICommandService<string>, JsonCommandService>("localhost", "CommandService");

            for (int i = 0; i < serviceConfig.Count; i++)
            {
                var configuration = new BindingConfiguration
                {
                    Port = serviceConfig[i].Port.ToString(CultureInfo.InvariantCulture),
                    ServiceType = serviceConfig[i].GetWcfServiceType()
                };

                var binding = BindingFactory.Create(configuration);
                if (binding.DataTypes.IsSet(DataTypeSupport.Native))
                {
                    _serviceHostNative.AddBinding(binding);
                }
                else if (binding.DataTypes.IsSet(DataTypeSupport.Json))
                {
                    _serviceHostJson.AddBinding(binding);
                }
            }

            if (AppContext.Current.Config.Runtime.ServiceSecurity == ServiceSecurityMode.BasicSSL)
            {
                var serviceSecurity = new ServiceSecurity
                {
                    CertificateConfiguration = AppContext.Current.Config.Providers.Certificate.Default,
                    SecurityMode = AppContext.Current.Config.Runtime.ServiceSecurity
                };

                _serviceHostJson.SetSecured(serviceSecurity);
                _serviceHostNative.SetSecured(serviceSecurity);
            }

            _serviceHostNative.Host();
            _serviceHostJson.Host();

            var authOutput = ServiceFacade.ExecuteCommand(String.Format("UserAuth /name:{0} /pwd:{1}",
                AppContext.Current.Config.Runtime.SuperUser.Username,
                AppContext.Current.Config.Runtime.SuperUser.Password), null);

            var authenticationOutput = authOutput.Data as AuthenticationOutput;
            if (authenticationOutput == null)
            {
                throw new InvalidOperationException(
                    "Could not authenticate server user.  Check the sanity of database.");
            }

            AuthenticationToken = authenticationOutput.Token;

            // get settings from database
            var settingsOutput = ServiceFacade.ExecuteCommand("GetSettings", null);
            if (settingsOutput != null && settingsOutput.Data != null)
            {
                var settings = (Dictionary<string, string>) settingsOutput.Data;
                foreach (var setting in settings)
                {
                    AppContext.Current.ApplicationParams.Add(setting.Key, setting.Value);
                }
            }

            //AppContext.Current.Log.Info("Agent Started: " + _serviceHostNative);
            //AppContext.Current.Log.Info("Agent Started: " + _serviceHostJson);

            #endregion

            _thread = new Thread(RunInBackground)
            {
                Name = "ShellService",
                IsBackground = true
            };

            _shutdownEvent = new ManualResetEvent(false);
            _thread.Start();
        }

        protected override void OnStop()
        {
            _shutdownEvent.Set();
            _serviceHostNative.Stop();
            _serviceHostNative.Stop();
            if (!_thread.Join(3000))
            {
                // give the thread 3 seconds to stop
                _thread.Abort();
            }
        }

        private void RunInBackground()
        {
            // wait for exit event
            _shutdownEvent.WaitOne();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //TODO: Handle unhandled exception

            AppContext.Current.Log.Info(e.ExceptionObject.ToString());

            if (e.IsTerminating)
            {
                AppContext.Current.Log.Info("Terminating");
            }
        }

        private BaseProvider GetDatabaseProviderInstance()
        {
            var defaultDatabaseConfig = AppContext.Current.Config.Providers.Database.Default;
            var providerTypesConfig = AppContext.Current.Config.Providers.Types;


            if (!providerTypesConfig.IsValid(defaultDatabaseConfig.Type))
                throw new ConfigurationErrorsException("Provider type not found: " + defaultDatabaseConfig.Type +
                                                       " for database provider: " +
                                                       defaultDatabaseConfig.ProviderName);
            var providerConfiguration = providerTypesConfig[defaultDatabaseConfig.Type];

            Type providerType = Type.GetType(providerConfiguration.AssemblyPath);
            if (providerType != null)
            {
                var instance = Activator.CreateInstance(providerType, defaultDatabaseConfig);

                var baseProvider = instance as BaseProvider;
                if (baseProvider != null)
                {
                    baseProvider.Init();
                    return baseProvider;
                }
                throw new InvalidOperationException("Type : " + providerType + " does not inherit from BaseProvider");
            }

            throw new InvalidOperationException(string.Format("Unknown Case: Could not get database provider for :{0}",
                defaultDatabaseConfig.Type));
        }

        private void InitDatabase()
        {
            AppContext.Current.Provider = GetDatabaseProviderInstance();
            AppContext.Current.Log.Info("Agent Started: DataManagement");
        }

        private void InitializeComponent()
        {
            // 
            // KonfDBService
            // 
            this.ServiceName = "KonfDBH";
        }
    }
}