﻿//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//----------------------------------------------------------------------- 

using System;
using System.Collections.Generic;
using Microsoft.Web.Management.Client;
using Microsoft.Web.Management.Client.Win32;
using Microsoft.Web.Management.Server;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Settings
{

    [ModulePageIdentifier(Globals.RuntimeLimitsPageIdentifier)]
    internal sealed class RuntimeLimitsPage : ModulePropertiesPage
    {
        // If adding properties here make sure to update the RuntimeLimistGlobal.cs
        private readonly string [] _settingNames = new string[6]{
            "max_execution_time",
            "max_input_time",
            "memory_limit",
            "post_max_size",
            "upload_max_filesize",
            "max_file_uploads"
        };

        private PropertyBag _clone;
        private PropertyBag _bag;
        private PageTaskList _taskList;

        protected override bool CanApplyChanges
        {
            get 
            {
                 return true;
            }
        }

        protected override bool ConfigNamesPresent
        {
            get
            {
                return true;
            }
        }

        private new PHPModule Module
        {
            get
            {
                return (PHPModule)base.Module;
            }
        }

        protected override TaskListCollection Tasks
        {
            get
            {
                TaskListCollection tasks = base.Tasks;
                if (_taskList == null)
                {
                    _taskList = new PageTaskList(this);
                }

                tasks.Add(_taskList);

                return tasks;
            }
        }

        protected override PropertyBag GetProperties()
        {
            PropertyBag result = new PropertyBag();

            object o = Module.Proxy.GetPHPIniSettings();
            PHPIniFile file = new PHPIniFile();
            file.SetData(o);

            for (int i = 0; i < _settingNames.Length; i++)
            {
                PHPIniSetting setting = file.GetSetting(_settingNames[i]);
                if (setting != null)
                {
                    result[i] = setting.Value;
                }
            }
           
            return result;
        }

        private void GoBack()
        {
            Navigate(typeof(PHPPage));
        }

        protected override void ProcessProperties(PropertyBag properties)
        {
            _bag = properties;
            _clone = _bag.Clone();

            RuntimeLimitSettings settings = TargetObject as RuntimeLimitSettings;
            if (settings == null)
            {
                settings = new RuntimeLimitSettings(this, _clone);
                TargetObject = settings;
            }
            else
            {
                settings.Initialize(_clone);
            }

            ClearDirty();
        }

        protected override PropertyBag UpdateProperties(out bool updateSuccessful)
        {
            updateSuccessful = false;

            RemoteObjectCollection<PHPIniSetting> settings = new RemoteObjectCollection<PHPIniSetting>();

            for (int i = 0; i < _settingNames.Length; i++)
            {
                settings.Add(new PHPIniSetting(_settingNames[i], (string)_clone[i], "PHP"));
            }

            try
            {
                Module.Proxy.AddOrUpdateSettings(settings);
                _bag = _clone;
                updateSuccessful = true;
            }
            catch(Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }

            return _bag;
        }


        private class PageTaskList : TaskList
        {
            private RuntimeLimitsPage _page;

            public PageTaskList(RuntimeLimitsPage page)
            {
                _page = page;
            }

            public override System.Collections.ICollection GetTaskItems()
            {
                List<TaskItem> tasks = new List<TaskItem>();

                tasks.Add(new MethodTaskItem("GoBack", Resources.AllPagesGoBackTask, "Tasks", null, Resources.GoBack16));

                return tasks;
            }

            public void GoBack()
            {
                _page.GoBack();
            }
        }


    }
}