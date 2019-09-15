using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.Xml;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace OnCheckinTransformer.VisualStudio
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidOnCheckinTransforms_VisualStudioPkgString)]
    public sealed class OnCheckinTransforms_VisualStudioPackage : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public OnCheckinTransforms_VisualStudioPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }

        private static readonly string IsTransformFile = "IsTransformFile";

        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine("Entering Initialize() of: {0}", this);
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the menu item.
                var menuCommandID = new CommandID(GuidList.guidOnCheckinTransforms_VisualStudioCmdSet, (int)PkgCmdIDList.oncheckinEnvTransform);
                var menuCommand = new OleMenuCommand(OnAddTransformCommand, menuCommandID);
                menuCommand.BeforeQueryStatus += OnBeforeQueryStatusAddTransformCommand;

                mcs.AddCommand(menuCommand);
            }
        }

        private void OnBeforeQueryStatusAddTransformCommand(object sender, EventArgs e)
        {
            // get the menu that fired the event
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                // start by assuming that the menu will not be shown
                menuCommand.Visible = false;
                menuCommand.Enabled = false;

                IVsHierarchy hierarchy = null;
                uint itemid = VSConstants.VSITEMID_NIL;

                if (!IsSingleProjectItemSelection(out hierarchy, out itemid)) return;

                var vsProject = (IVsProject)hierarchy;
                if (!ProjectSupportsTransforms(vsProject)) return;

                if (!ItemSupportsTransforms(vsProject, itemid)) return;

                menuCommand.Visible = true;
                menuCommand.Enabled = true;
            }
        }

        private void OnAddTransformCommand(object sender, EventArgs e)
        {
            IVsHierarchy hierarchy = null;
            uint itemid = VSConstants.VSITEMID_NIL;

            if (!IsSingleProjectItemSelection(out hierarchy, out itemid)) return;

            var vsProject = (IVsProject)hierarchy;
            if (!ProjectSupportsTransforms(vsProject)) return;

            string projectFullPath = null;
            if (ErrorHandler.Failed(vsProject.GetMkDocument(VSConstants.VSITEMID_ROOT, out projectFullPath))) return;

            var buildPropertyStorage = vsProject as IVsBuildPropertyStorage;
            if (buildPropertyStorage == null) return;
            
            // get the name of the item
            string itemFullPath = null;
            if (ErrorHandler.Failed(vsProject.GetMkDocument(itemid, out itemFullPath))) return;

            // Save the project file
            var solution = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
            int hr = solution.SaveSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_SaveIfDirty, hierarchy, 0);
            if (hr < 0)
            {
                throw new COMException(string.Format("Failed to add project item", itemFullPath, GetErrorInfo()), hr);
            }

            var selectedProjectItem = GetProjectItemFromHierarchy(hierarchy, itemid);
            if (selectedProjectItem != null)
            {
                string itemFolder = Path.GetDirectoryName(itemFullPath);
                string itemFilename = Path.GetFileNameWithoutExtension(itemFullPath);
                string itemExtension = Path.GetExtension(itemFullPath);

                string content = Resources.TransformFileContents;

                var dialog = new EnvironmentEntry();
                dialog.ShowDialog();
                if (string.IsNullOrEmpty(dialog.InputText)) return;

                string itemName = string.Format("{0}.{1}{2}", itemFilename, dialog.InputText.ToCamelCase(), itemExtension);

                var newItemFullPath = Path.Combine(itemFolder, itemName);
                AddXdtTransformFile(selectedProjectItem, content, itemName, itemFolder, hierarchy);
                AddTransformToSolution(selectedProjectItem, itemName, itemFolder, hierarchy);

                // also add the web.oncheckin.config file as well if it's there.
                AddTransformToSolution(selectedProjectItem, "web.oncheckin.config", itemFolder, hierarchy);

                // now force the storage type.
                uint addedFileId;
                hierarchy.ParseCanonicalName(newItemFullPath, out addedFileId);
                //buildPropertyStorage.SetItemAttribute(addedFileId, IsTransformFile, "True");
            }
        }

        private void AddTransformToSolution(ProjectItem selectedProjectItem, string itemName, string projectPath, IVsHierarchy heirarchy)
        {
            string itemPath = Path.Combine(projectPath, itemName);
            if (!File.Exists(itemPath)) return;

            uint removeFileId;
            heirarchy.ParseCanonicalName(itemPath, out removeFileId);
            if (removeFileId < uint.MaxValue)
            {
                var itemToRemove = GetProjectItemFromHierarchy(heirarchy, removeFileId);
                if (itemToRemove!=null) itemToRemove.Remove();
            }

            // and add it to the project
            var addedItem = selectedProjectItem.ProjectItems.AddFromFile(itemPath);
            // we need to set the Build Action to None to ensure that it doesn't get published for web projects
            addedItem.Properties.Item("ItemType").Value = "None";
        }

        private void AddXdtTransformFile(ProjectItem selectedProjectItem, string content, string itemName, string projectPath, IVsHierarchy heirarchy)
        {
            string itemPath = Path.Combine(projectPath, itemName);
            if (!File.Exists(itemPath))
            {
                // create the new XDT file
                using (var writer = new StreamWriter(itemPath))
                {
                    writer.Write(content);
                }
            }
        }

        private ProjectItem GetProjectItemFromHierarchy(IVsHierarchy pHierarchy, uint itemID)
        {
            object propertyValue;
            ErrorHandler.ThrowOnFailure(pHierarchy.GetProperty(itemID, (int)__VSHPROPID.VSHPROPID_ExtObject, out propertyValue));
            
            var projectItem = propertyValue as ProjectItem;
            if (projectItem == null) return null;
            
            return projectItem;
        }
        
        public static string GetErrorInfo()
        {
            string errText = null;
            var uiShell = (IVsUIShell)Package.GetGlobalService(typeof(IVsUIShell));
            if (uiShell != null)
            {
                uiShell.GetErrorInfo(out errText);
            }
            if (errText == null) return string.Empty;
            return errText;
        }

        private bool IsItemTransformItem(IVsProject vsProject, uint itemid)
        {
            var buildPropertyStorage = vsProject as IVsBuildPropertyStorage;
            if (buildPropertyStorage == null) return false;

            bool isItemTransformFile = false;

            string value;
            buildPropertyStorage.GetItemAttribute(itemid, IsTransformFile, out value);
            if (string.Compare("true", value, true) == 0) isItemTransformFile = true;

            // we need to special case web.config transform files
            if (!isItemTransformFile)
            {
                string pattern = @"web\..+\.config";
                string filepath;
                buildPropertyStorage.GetItemAttribute(itemid, "FullPath", out filepath);
                if (!string.IsNullOrEmpty(filepath))
                {
                    var fi = new System.IO.FileInfo(filepath);
                    var regex = new System.Text.RegularExpressions.Regex(
                        pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (regex.IsMatch(fi.Name))
                    {
                        isItemTransformFile = true;
                    }
                }
            }
            return isItemTransformFile;
        }

        private bool ItemSupportsTransforms(IVsProject project, uint itemid)
        {
            string itemFullPath = null;

            if (ErrorHandler.Failed(project.GetMkDocument(itemid, out itemFullPath))) return false;

            // make sure its not a transform file itsle
            //bool isTransformFile = IsItemTransformItem(project, itemid);

            var transformFileInfo = new FileInfo(itemFullPath);
            bool isWebConfig = string.Compare("web.config", transformFileInfo.Name, StringComparison.OrdinalIgnoreCase) == 0;

            return (isWebConfig && IsXmlFile(itemFullPath));
        }

        List<string> SupportedProjectExtensions = new List<string>
        {
            ".csproj",
            ".vbproj",
            ".fsproj"
        };

        private bool ProjectSupportsTransforms(IVsProject project)
        {
            string projectFullPath = null;
            if (ErrorHandler.Failed(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out projectFullPath))) return false;

            string projectExtension = Path.GetExtension(projectFullPath);

            foreach (string supportedExtension in SupportedProjectExtensions)
            {
                if (projectExtension.Equals(supportedExtension, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsXmlFile(string filepath)
        {
            if (string.IsNullOrWhiteSpace(filepath)) { throw new ArgumentNullException("filepath"); }
            if (!File.Exists(filepath)) throw new FileNotFoundException("File not found", filepath);
            
            var isXmlFile = true;
            try
            {
                using (var xmlTextReader = new XmlTextReader(filepath))
                {
                    // This is required because if the XML file has a DTD then it will try and download the DTD!
                    xmlTextReader.DtdProcessing = DtdProcessing.Ignore;
                    xmlTextReader.Read();
                }
            }
            catch (XmlException)
            {
                isXmlFile = false;
            }
            return isXmlFile;
        }

        public static bool IsSingleProjectItemSelection(out IVsHierarchy hierarchy, out uint itemid)
        {
            hierarchy = null;
            itemid = VSConstants.VSITEMID_NIL;
            int hr = VSConstants.S_OK;

            var monitorSelection = Package.GetGlobalService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            var solution = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution;
            if (monitorSelection == null || solution == null)
            {
                return false;
            }

            IVsMultiItemSelect multiItemSelect = null;
            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainerPtr = IntPtr.Zero;

            try
            {
                hr = monitorSelection.GetCurrentSelection(out hierarchyPtr, out itemid, out multiItemSelect, out selectionContainerPtr);

                if (ErrorHandler.Failed(hr) || hierarchyPtr == IntPtr.Zero || itemid == VSConstants.VSITEMID_NIL)
                {
                    // there is no selection
                    return false;
                }

                // multiple items are selected
                if (multiItemSelect != null) return false;

                // there is a hierarchy root node selected, thus it is not a single item inside a project

                if (itemid == VSConstants.VSITEMID_ROOT) return false;

                hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;
                if (hierarchy == null) return false;

                Guid guidProjectID = Guid.Empty;

                if (ErrorHandler.Failed(solution.GetGuidOfProject(hierarchy, out guidProjectID)))
                {
                    return false; // hierarchy is not a project inside the Solution if it does not have a ProjectID Guid
                }

                // if we got this far then there is a single project item selected
                return true;
            }
            finally
            {
                if (selectionContainerPtr != IntPtr.Zero)
                {
                    Marshal.Release(selectionContainerPtr);
                }

                if (hierarchyPtr != IntPtr.Zero)
                {
                    Marshal.Release(hierarchyPtr);
                }
            }
        }
    }
}
