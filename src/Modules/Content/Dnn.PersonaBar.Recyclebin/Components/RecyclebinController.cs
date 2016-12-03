﻿﻿#region Copyright
// DotNetNuke® - http://www.dotnetnuke.com
// Copyright (c) 2002-2016
// by DotNetNuke Corporation
// All Rights Reserved
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dnn.PersonaBar.Library;
using Dnn.PersonaBar.Recyclebin.Components.Dto;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Content;
using DotNetNuke.Entities.Content.Common;
using DotNetNuke.Entities.Content.Taxonomy;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Entities.Tabs.TabVersions;
using DotNetNuke.Entities.Urls;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Services.Localization;

namespace Dnn.PersonaBar.Recyclebin.Components
{
    public class RecyclebinController : ServiceLocator<IRecyclebinController, RecyclebinController>, IRecyclebinController
    {
        public static string PageDateTimeFormat = "yyyy-MM-dd hh:mm tt";

        #region Fields

        private readonly ITabController _tabController;
        private readonly ITabVersionSettings _tabVersionSettings;
        private readonly ITabChangeSettings _tabChangeSettings;
        private readonly ITabWorkflowSettings _tabWorkflowSettings;
        private readonly IModuleController _moduleController;
        #endregion

        public RecyclebinController()
        {
            _tabController = TabController.Instance;
            _tabVersionSettings = TabVersionSettings.Instance;
            _tabWorkflowSettings = TabWorkflowSettings.Instance;
            _moduleController = ModuleController.Instance;
            _tabChangeSettings = TabChangeSettings.Instance;
        }

        #region Properties
        private string LocalResourcesFile
        {
            get { return Path.Combine("~/DesktopModules/admin/Dnn.PersonaBar/App_LocalResources/Recyclebin.resx"); }
        }

        private PortalSettings PortalSettings
        {
            get { return PortalSettings.Current; }
        }
        #endregion

        #region ServiceLocator

        protected override Func<IRecyclebinController> GetFactory()
        {
            return () => new RecyclebinController();
        }

        #endregion

        #region Public Methods

        public string LocalizeString(string key)
        {
            return Localization.GetString(key, LocalResourcesFile);
        }

        public void DeleteTabs(IEnumerable<PageItem> tabs, StringBuilder errors, bool deleteDescendants = false)
        {
            if (tabs == null || !tabs.Any())
            {
                return;
            }

            foreach (
                var tab in tabs.OrderByDescending(t => t.Level).Select(page => _tabController.GetTab(page.Id, PortalSettings.PortalId)))
            {
                if (tab == null)
                {
                    continue;
                }

                if (TabPermissionController.CanDeletePage(tab) && tab.IsDeleted)
                {
                    if (tab.HasChildren)
                    {
                        errors.Append(string.Format(LocalizeString("Service_RemoveTabError"), tab.TabName));
                    }
                    else
                    {
                        HardDeleteTab(tab, deleteDescendants);    
                    }
                }
            }
            
        }

        public void DeleteTabs(IEnumerable<TabInfo> tabs, StringBuilder errors, bool deleteDescendants = false)
        {
            if (tabs == null || !tabs.Any())
            {
                return;
            }

            foreach (
                var tab in tabs.OrderByDescending(t => t.Level).Select(page => _tabController.GetTab(page.TabID, PortalSettings.PortalId)))
            {
                if (tab == null)
                {
                    continue;
                }

                if (TabPermissionController.CanDeletePage(tab) && tab.IsDeleted)
                {
                    if (tab.HasChildren)
                    {
                        errors.Append(string.Format(LocalizeString("Service_RemoveTabError"), tab.TabName));
                    }
                    else
                    {
                        HardDeleteTab(tab, deleteDescendants);
                    }
                }
            }
        }

        public void DeleteModules(IEnumerable<ModuleItem> modules, StringBuilder errors)
        {
            if (modules != null && modules.Any())
            {
                foreach (var module in modules.Select(mod => ModuleController.Instance.GetModule(mod.Id, mod.TabID, true)))
                {
                    if (module == null)
                    {
                        continue;
                    }
                    if (ModulePermissionController.CanDeleteModule(module) && module.IsDeleted)
                    {
                        HardDeleteModule(module);
                    }
                }
            }
        }

        public void DeleteModules(IEnumerable<ModuleInfo> modules, StringBuilder errors)
        {
            if (modules != null && modules.Any())
            {
                foreach (
                    var module in
                        modules.Select(mod => ModuleController.Instance.GetModule(mod.ModuleID, mod.TabID, true)))
                {
                    if (module == null)
                    {
                        continue;
                    }
                    if (ModulePermissionController.CanDeleteModule(module) && module.IsDeleted)
                    {
                        HardDeleteModule(module);
                    }
                }
            }
        }

        private void HardDeleteTab(TabInfo tab, bool deleteDescendants)
        {
            //get tab modules before deleting page
            var tabModules = _moduleController.GetTabModules(tab.TabID);

            //hard delete the tab
            _tabController.DeleteTab(tab.TabID, tab.PortalID, deleteDescendants);

            //delete modules that do not have other instances
            foreach (var kvp in tabModules)
            {
                //check if all modules instances have been deleted
                var delModule = _moduleController.GetModule(kvp.Value.ModuleID, Null.NullInteger, false);
                if (delModule == null || delModule.TabID == Null.NullInteger)
                {
                    _moduleController.DeleteModule(kvp.Value.ModuleID);
                }
            }
        }

        private void HardDeleteModule(ModuleInfo module)
        {
            //hard-delete Tab Module Instance
            _moduleController.DeleteTabModule(module.TabID, module.ModuleID, false);
        }

        public bool RestoreTab(TabInfo tab, out string resultmessage)
        {
            var changeControlStateForTab = _tabChangeSettings.GetChangeControlState(tab.PortalID, tab.TabID);
            if (changeControlStateForTab.IsChangeControlEnabledForTab)
            {
                _tabVersionSettings.SetEnabledVersioningForTab(tab.TabID, false);
                _tabWorkflowSettings.SetWorkflowEnabled(tab.PortalID, tab.TabID, false);
            }

            var success = true;
            resultmessage = null;

            //if parent of the page is deleted, then can't restore - parent should be restored first
            var deletedTabs = GetDeletedTabs();
            if (!Null.IsNull(tab.ParentId) && deletedTabs.Any(t => t.TabID == tab.ParentId))
            {
                resultmessage = string.Format(LocalizeString("Service_RestoreTabError"), tab.TabName);
                success = false;
            }
            else
            {
                _tabController.RestoreTab(tab, PortalSettings);

                //restore modules in this tab
                var tabdeletedModules = GetDeletedModules().Where(m => m.TabID == tab.TabID);

                foreach (var m in tabdeletedModules)
                {
                    success = RestoreModule(m.ModuleID, m.TabID, out resultmessage);
                }

                if (changeControlStateForTab.IsChangeControlEnabledForTab)
                {
                    _tabVersionSettings.SetEnabledVersioningForTab(tab.TabID, changeControlStateForTab.IsVersioningEnabledForTab);
                    _tabWorkflowSettings.SetWorkflowEnabled(tab.PortalID, tab.TabID, changeControlStateForTab.IsWorkflowEnabledForTab);
                }
            }
            return success;
        }

        public bool RestoreModule(int moduleId, int tabId, out string errorMessage)
        {
            errorMessage = null;
            // restore module
            var module = _moduleController.GetModule(moduleId, tabId, false);
            if ((module != null))
            {
                var deletedTabs = GetDeletedTabs().Where(t => t.TabID == module.TabID);
                if (deletedTabs.Any())
                {
                    var title = !string.IsNullOrEmpty(module.ModuleTitle) ? module.ModuleTitle : module.DesktopModule.FriendlyName;
                    errorMessage = string.Format(LocalizeString("Service_RestoreModuleError"), title, deletedTabs.SingleOrDefault().TabName);
                    return false;
                }
                _moduleController.RestoreModule(module);
                
                TrackRestoreModuleAction(module);
            }
            return true;
        }

        private void TrackRestoreModuleAction(ModuleInfo module)
        {
            var currentUser = UserController.Instance.GetCurrentUserInfo();
            var currentModuleVersion = TabVersionBuilder.Instance.GetModuleContentLatestVersion(module);
            TabChangeTracker.Instance.TrackModuleAddition(module, currentModuleVersion, currentUser.UserID);
        }

        public List<TabInfo> GetDeletedTabs()
        {
            var adminTabId = PortalSettings.AdminTabId;
            var tabs = TabController.GetPortalTabs(PortalSettings.PortalId, adminTabId, true, true, true, true);
            var deletedtabs = tabs.Where(t => t.ParentId != adminTabId && t.IsDeleted && TabPermissionController.CanDeletePage(t)).ToList();
            return deletedtabs;
        }

        public List<ModuleInfo> GetDeletedModules()
        {
            var deletedModules = _moduleController.GetModules(PortalSettings.PortalId)
                .Cast<ModuleInfo>()
                .Where(module => module.IsDeleted && ModulePermissionController.CanDeleteModule(module))
                .ToList();
            return deletedModules;
        }

        public string GetTabStatus(TabInfo tab)
        {
            if (tab.DisableLink)
            {
                return "Disabled";
            }

            return tab.IsVisible ? "Visible" : "Hidden";
        }

        #endregion
    }
}
