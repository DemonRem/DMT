﻿using System.IO;
using System.Linq;
using DMT.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DMT.Tasks
{
    [MinRun]
    public class BackupFiles : BaseTask
    {

        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public int BuildNumber { get; set; }

        private ModuleDefinition Module { get; set; }

        private void GetVersionInfo()
        {

            var consts = Module.Types.First(d => d.Name == "Constants");
            var ctor = consts.Methods.Single(d => d.Name == ".cctor");
            var pro = ctor.Body.GetILProcessor();
            var ins = pro.Body.Instructions;
            var start = ins.First(d => d.OpCode == OpCodes.Stsfld && ((FieldDefinition)d.Operand).Name.Contains("cCompatibilityVersion"));

            start = start.Next.Next;

            MajorVersion = start.GetValueAsInt();
            MinorVersion = start.Next.GetValueAsInt();
            BuildNumber = start.Next.Next.GetValueAsInt();

        }

        private void RemoveSdxReference(PatchData data)
        {

            var filesToRemove = new[] { "SDX.Core.dll", "SDX.Payload.dll", };


            foreach (var s in filesToRemove)
            {
                var path = data.ManagedFolder + s;
                if (File.Exists(path))
                {
                    LogWarning("Removing SDX reference as it's no longer needed: " + s);
                    File.Delete(path);
                }
            }

        }

        public override bool Patch(PatchData data)
        {

            RemoveSdxReference(data);

            var ass = AssemblyDefinition.ReadAssembly(data.GameDllLocation);
            Module = ass.MainModule;
            GetVersionInfo();

            data.BackupFolder = data.BackupFolder + MajorVersion + "." + MinorVersion + "b" + BuildNumber + "/";
            data.BackupFolder.MakeFolder();
            data.BackupDllLocataion = data.BackupFolder + PatchData.AssemblyFilename;

            if (File.Exists(data.BackupDllLocataion))
            {
                LogInfo("Backup dll found: " + data.BackupDllLocataion);
                return true;
            }

            var modManager = Module.Types.FirstOrDefault(d => d.Name == "ModManager");
            var patchedObject = modManager?.Fields.FirstOrDefault(d => d.Name == "SdxPatchedCheck");
            var isPatched = patchedObject != null;
            if (isPatched)
            {
                LogError("This Assembly-CSharp.dll file has already been patched by SDX so can not be used as a backup. Reset your game files and try again.");
                return false;
            }


            File.Copy(data.GameDllLocation, data.BackupDllLocataion, true);
            Helper.CopyFolder(data.ConfigFolder, data.BackupFolder + "Config", true);
            LogInfo("Copied backup dll to: " + data.BackupDllLocataion);
            return true;

        }

    }
}