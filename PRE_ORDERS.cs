using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Windows;
using Resto.Front.Api;
using Resto.Front.Api.Attributes;
using Resto.Front.Api.Attributes.JetBrains;
using Resto.Front.Api.UI;

namespace PRE_ORDERS
{
    //[UsedImplicitly]
    [UsedImplicitly, PluginLicenseModuleId(ModuleId)]

    public sealed class PRE_ORDERS : IFrontPlugin
    {
        public const int ModuleId = 29026118;
        private readonly Stack<IDisposable> subscriptions = new Stack<IDisposable>();

        public PRE_ORDERS()
        {
            PluginContext.Log.Info("Initializing PRE_ORDERS");
            string PLUGIN_NAME = PRE_ORDERS_Ini.PRE_ORDERS_Start();
            PluginContext.Operations.AddButtonToPluginsMenu("PRE_ORDERS v 9.2.6029.0", x => x.vm.ShowOkPopup("IKS-T81F", "Pirminės sąskaitos numerio paieška"));
            subscriptions.Push(new PRE_ORDERS_Dialog());
            PluginContext.Log.Info("PRE_ORDERS started");}

        public void Dispose()
        {
            while (subscriptions.Any())
            {
                var subscription = subscriptions.Pop();
                try
                {
                    subscription.Dispose();
                }
                catch (RemotingException)
                {
                    // nothing to do with the lost connection
                }
            }
            PluginContext.Log.Info("PRE_ORDERS stopped");
        }
    }
}