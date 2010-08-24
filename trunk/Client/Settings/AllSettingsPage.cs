//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//----------------------------------------------------------------------- 

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.Management.Client;
using Microsoft.Web.Management.Client.Win32;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Settings
{

    [ModulePageIdentifier(Globals.PHPSettingsPageIdentifier)]
    internal sealed class AllSettingsPage : ModuleListPage, IModuleChildPage
    {
        private ColumnHeader _nameColumn;
        private ColumnHeader _valueColumn;
        private ColumnHeader _sectionColumn;
        private ModuleListPageGrouping _sectionGrouping;
        private PageTaskList _taskList;
        private ModuleListPageSearchField[] _searchFields;
        private PHPIniFile _file;

        private const string NameString = "Name";
        private const string ValueString = "Value";
        private const string SectionString = "Section";
        private string _filterBy;
        private string _filterValue;
        private IModulePage _parentPage;

        protected override bool CanRefresh
        {
            get
            {
                return true;
            }
        }

        protected override bool CanSearch
        {
            get
            {
                return true;
            }
        }

        protected override ModuleListPageGrouping DefaultGrouping
        {
            get
            {
                return Groupings[0];
            }
        }

        public override ModuleListPageGrouping[] Groupings
        {
            get
            {
                if (_sectionGrouping == null)
                {
                    _sectionGrouping = new ModuleListPageGrouping(SectionString, Resources.AllSettingsPageSectionField);
                }

                return new ModuleListPageGrouping[] { _sectionGrouping };
            }
        }

        internal bool IsReadOnly
        {
            get
            {
                return Connection.ConfigurationPath.PathType == Microsoft.Web.Management.Server.ConfigurationPathType.Site &&
                        !Connection.IsUserServerAdministrator;
            }
        }

        private new PHPModule Module
        {
            get
            {
                return (PHPModule)base.Module;
            }
        }

        public IModulePage ParentPage
        {
            get
            {
                return _parentPage;
            }
            set
            {
                _parentPage = value;
            }
        }

        protected override ModuleListPageSearchField[] SearchFields
        {
            get
            {
                if (_searchFields == null)
                {
                    _searchFields = new ModuleListPageSearchField[]{
                        new ModuleListPageSearchField(NameString, Resources.AllSettingsPageNameField),
                        new ModuleListPageSearchField(ValueString, Resources.AllSettingsPageValueField),
                        new ModuleListPageSearchField(SectionString, Resources.AllSettingsPageSectionField)};
                }

                return _searchFields;
            }
        }

        private PHPSettingItem SelectedItem
        {
            get
            {
                if (ListView.SelectedIndices.Count == 1)
                {
                    return ListView.SelectedItems[0] as PHPSettingItem;
                }

                return null;
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

        private void AddPHPSetting()
        {
            using (AddEditSettingDialog dlg = new AddEditSettingDialog(Module, GetListOfSections()))
            {
                if (ShowDialog(dlg) == DialogResult.OK)
                {
                    Refresh();
                }
            }
        }

        private void EditPHPSetting()
        {
            if (SelectedItem != null && !this.IsReadOnly)
            {
                using (AddEditSettingDialog dlg = new AddEditSettingDialog(Module, SelectedItem.Setting))
                {
                    if (ShowDialog(dlg) == DialogResult.OK)
                    {
                        Refresh();
                    }
                }
            }
        }

        protected override ListViewGroup[] GetGroups(ModuleListPageGrouping grouping)
        {
            Dictionary<string, ListViewGroup> groups = new Dictionary<string, ListViewGroup>();

            if (grouping == _sectionGrouping)
            {
                ListView.ListViewItemCollection items = ListView.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    PHPSettingItem item = (PHPSettingItem)items[i];
                    string sectionName = item.SectionName;
                    if (String.IsNullOrEmpty(sectionName)) {
                        continue;
                    }
                    if (!groups.ContainsKey(sectionName))
                    {
                        ListViewGroup sectionGroup = new ListViewGroup(sectionName, sectionName);
                        groups.Add(sectionName, sectionGroup);
                    }
                }
            }

            ListViewGroup[] result = new ListViewGroup[groups.Count];
            groups.Values.CopyTo(result, 0);
            return result;
        }

        private IList<string> GetListOfSections()
        {
            SortedList<string, object> sections = new SortedList<string, object>();

            foreach (PHPSettingItem item in ListView.Items)
            {
                string section = item.Setting.Section;
                if (String.IsNullOrEmpty(section))
                {
                    continue;
                }

                if (!sections.ContainsKey(section))
                {
                    sections.Add(item.Setting.Section, null);
                }
            }

            return sections.Keys;
        }

        private void GetSettings()
        {
            StartAsyncTask(Resources.AllSettingsPageGettingSettings, OnGetSettings, OnGetSettingsCompleted);
        }

        private void GoBack()
        {
            Navigate(typeof(PHPPage));
        }

        protected override void InitializeListPage()
        {
            _nameColumn = new ColumnHeader();
            _nameColumn.Text = Resources.AllSettingsPageNameField;
            _nameColumn.Width = 180;

            _valueColumn = new ColumnHeader();
            _valueColumn.Text = Resources.AllSettingsPageValueField;
            _valueColumn.Width = 180;

            _sectionColumn = new ColumnHeader();
            _sectionColumn.Text = Resources.AllSettingsPageSectionField;
            _sectionColumn.Width = 100;

            ListView.Columns.AddRange(new ColumnHeader[] { _nameColumn, _valueColumn, _sectionColumn });

            ListView.MultiSelect = false;
            ListView.SelectedIndexChanged += new EventHandler(OnListViewSelectedIndexChanged);
            ListView.DoubleClick += new EventHandler(OnListViewDoubleClick);
        }

        private void LoadPHPIni(PHPIniFile file)
        {
            try
            {
                ListView.SuspendLayout();
                ListView.Items.Clear();

                foreach (PHPIniSetting setting in file.Settings)
                {
                    if (_filterBy != null && _filterValue != null) {
                        if (_filterBy == NameString &&
                            setting.Name.IndexOf(_filterValue, StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            continue;
                        }
                        else if (_filterBy == ValueString &&
                            setting.Value.IndexOf(_filterValue, StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            continue;
                        }
                        else if (_filterBy == SectionString &&
                            setting.Section.IndexOf(_filterValue, StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            continue;
                        }
                    }
                   
                    ListView.Items.Add(new PHPSettingItem(setting));
                }

                if (SelectedGrouping != null)
                {
                    Group(SelectedGrouping);
                }
            }
            finally
            {
                ListView.ResumeLayout();
            }
        }

        protected override void OnActivated(bool initialActivation)
        {
            base.OnActivated(initialActivation);

            if (initialActivation)
            {
                GetSettings();
            }
        }

        private void OnGetSettings(object sender, DoWorkEventArgs e)
        {
            e.Result = Module.Proxy.GetPHPIniSettings();
        }

        private void OnGetSettingsCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                object o = e.Result;

                _file = new PHPIniFile();
                _file.SetData(o);

                LoadPHPIni(_file);
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }
        }

        protected override void OnGroup(ModuleListPageGrouping grouping)
        {
            ListView.SuspendLayout();
            try
            {
                foreach (PHPSettingItem item in ListView.Items)
                {
                    if (grouping == _sectionGrouping)
                    {
                        item.Group = ListView.Groups[item.SectionName];
                    }
                }
            }
            finally
            {
                ListView.ResumeLayout();
            }
        }

        private void OnListViewDoubleClick(object sender, EventArgs e)
        {
            EditPHPSetting();
        }

        private void OnListViewSelectedIndexChanged(object sender, EventArgs e)
        {
            Update();
        }

        protected override void OnSearch(ModuleListPageSearchOptions options)
        {
            if (options.ShowAll)
            {
                _filterBy = null;
                _filterValue = null;
                LoadPHPIni(_file);
            }
            else
            {
                _filterBy = options.Field.Name;
                _filterValue = options.Text;
                LoadPHPIni(_file);
            }
        }

        internal void OpenPHPIniFile()
        {
            try
            {
                string physicalPath = Module.Proxy.GetPHPIniPhysicalPath();
                if (!String.IsNullOrEmpty(physicalPath) &&
                    String.Equals(Path.GetExtension(physicalPath), ".ini", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(physicalPath))
                {
                    Process.Start(physicalPath);
                }
                else
                {
                    ShowMessage(String.Format(CultureInfo.CurrentCulture, Resources.ErrorPHPIniFileDoesNotExist, physicalPath), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                DisplayErrorMessage(ex, Resources.ResourceManager);
            }
        }

        protected override void Refresh()
        {
            GetSettings();
        }

        private void RemovePHPSetting()
        {
            PHPSettingItem item = SelectedItem;

            if (item != null)
            {
                if (ShowMessage(Resources.PHPIniSettingDeleteConfirmation, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        Module.Proxy.RemoveSetting(item.Setting);
                        ListView.Items.Remove(item);
                    }
                    catch(Exception ex)
                    {
                        DisplayErrorMessage(ex, Resources.ResourceManager);
                    }
                }
            }
        }


        private class PageTaskList : TaskList
        {
            private AllSettingsPage _page;

            public PageTaskList(AllSettingsPage page)
            {
                _page = page;
            }

            public void AddSetting()
            {
                _page.AddPHPSetting();
            }

            public void EditSetting()
            {
                _page.EditPHPSetting();
            }

            public override System.Collections.ICollection GetTaskItems()
            {
                List<TaskItem> tasks = new List<TaskItem>();

                if (_page.IsReadOnly)
                {
                    tasks.Add(new MessageTaskItem(MessageTaskItemType.Information, Resources.AllPagesPageIsReadOnly, "Information"));
                }
                else
                {
                    tasks.Add(new MethodTaskItem("AddSetting", Resources.AllSettingsPageAddSettingTask, "Edit"));

                    if (_page.SelectedItem != null)
                    {
                        tasks.Add(new MethodTaskItem("EditSetting", Resources.AllSettingsPageEditTask, "Edit", null));
                        tasks.Add(new MethodTaskItem("RemoveSetting", Resources.AllSettingsPageRemoveTask, "Edit", null, Resources.Delete16));
                    }

                    if (_page.Connection.IsLocalConnection)
                    {
                        tasks.Add(new MethodTaskItem("OpenPHPIniFile", Resources.AllPagesOpenPHPIniTask, "Tasks", null));
                    }
                }

                tasks.Add(new MethodTaskItem("GoBack", Resources.AllPagesGoBackTask, "Tasks", null, Resources.GoBack16));

                return tasks;
            }

            public void GoBack()
            {
                _page.GoBack();
            }

            public void OpenPHPIniFile()
            {
                _page.OpenPHPIniFile();
            }

            public void RemoveSetting()
            {
                _page.RemovePHPSetting();
            }

        }


        private class PHPSettingItem : ListViewItem
        {
            private PHPIniSetting _setting;

            public PHPSettingItem(PHPIniSetting setting)
            {
                _setting = setting;
                Text = _setting.Name;
                SubItems.Add(_setting.Value);
                SubItems.Add(_setting.Section);
            }

            public string SectionName
            {
                get
                {
                    return _setting.Section;
                }
            }

            public PHPIniSetting Setting
            {
                get
                {
                    return _setting;
                }
            }
        }
    }
}
