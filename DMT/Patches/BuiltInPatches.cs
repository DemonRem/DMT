﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DMT.Patches
{
    class BuiltInPatches
    {

        internal void Patch(ModuleDefinition game, ModuleDefinition dmt)
        {
            MarkAsPatched(game);
            HookHarmony(game, dmt);
            HookConsoleCommands(game, dmt);
        }

        private void MarkAsPatched(ModuleDefinition game)
        {
            var fieldDef = new FieldDefinition("SdxPatchedCheck", FieldAttributes.Public | FieldAttributes.Static, game.Import(typeof(bool)));
            var modManager = game.Types.FirstOrDefault(d => d.Name == "ModManager");
            modManager.Fields.Add(fieldDef);
        }

        private void HookHarmony(ModuleDefinition game, ModuleDefinition mod)
        {
            Logging.Log("Hooking harmony");

            var steam = game.Types.FirstOrDefault(d => d.Name == "Steam");
            var singletonCreated = steam.Methods.First(d => d.Name == "singletonCreated");
            var helper = mod.Types.First(d => d.Name == "DMTChanges");
            var hookHarmony = game.Import(helper.Methods.First(d => d.Name == "HookHarmony"));

            var pro = singletonCreated.Body.GetILProcessor();
            var body = pro.Body.Instructions;
            var ins = body.First(d => d.OpCode == OpCodes.Newobj);

            pro.InsertBefore(ins, Instruction.Create(OpCodes.Callvirt, hookHarmony));


        }
        private void HookConsoleCommands(ModuleDefinition game, ModuleDefinition mod)
        {
            Logging.Log("Hooking console commands");

            var console = game.Types.FirstOrDefault(d => d.Name == "SdtdConsole");
            var register = console.Methods.First(d => d.Name == "RegisterCommands");
            var helper = mod.Types.First(d => d.Name == "DMTChanges");
            var addRefs = game.Import(helper.Methods.First(d => d.Name == "AddReferencedAssemblies"));
            var initMethods = game.Import(helper.Methods.First(d => d.Name == "DmtInit"));

            var pro = register.Body.GetILProcessor();
            var body = pro.Body.Instructions;
            var ins = body.Last(d => d.OpCode == OpCodes.Ret);

            //pro.InsertBefore(ins, Instruction.Create(OpCodes.Ldloc_1));
            pro.InsertBefore(ins, Instruction.Create(OpCodes.Callvirt, addRefs));
            pro.InsertBefore(ins, Instruction.Create(OpCodes.Callvirt, initMethods));


        }
    }
}