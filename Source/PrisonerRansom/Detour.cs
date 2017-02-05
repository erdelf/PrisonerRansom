using HugsLib.Source.Detour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace PrisonerRansom
{
    public static class Detour
    {
        /**
     * CCL Code.. which means, if you copy it, give credit to "1000101"
     */

        private static List<string> detoured = new List<string>();
        private static List<string> destinations = new List<string>();

        public static unsafe bool TryDetourFromTo(MethodInfo source, MethodInfo destination)
        {
            try
            {
                ((Action)(() =>
                {
                    DetourProvider.TryCompatibleDetour(source, destination);
                }))();
                Log.Message("HugsLib Present");
                return true;
            }
            catch (TypeLoadException)
            {
                Log.Message("HugsLib not present");
            }

            // error out on null arguments
            if (source == null)
            {
                Log.Error("Source MethodInfo is null");
                return false;
            }

            if (destination == null)
            {
                Log.Error("Destination MethodInfo is null");
                return false;
            }

            // keep track of detours and spit out some messaging
            string sourceString = source.DeclaringType.FullName + "." + source.Name + " @ 0x" + source.MethodHandle.GetFunctionPointer().ToString("X" + (IntPtr.Size * 2).ToString());
            string destinationString = destination.DeclaringType.FullName + "." + destination.Name + " @ 0x" + destination.MethodHandle.GetFunctionPointer().ToString("X" + (IntPtr.Size * 2).ToString());


            if (detoured.Contains(sourceString))
            {
                Log.Warning("Source method ('" + sourceString + "') is previously detoured to '" + destinations[detoured.IndexOf(sourceString)] + "'");
            }

            Debug.Log("Detouring '" + sourceString + "' to '" + destinationString + "'");


            detoured.Add(sourceString);
            destinations.Add(destinationString);

            if (IntPtr.Size == sizeof(Int64))
            {
                // 64-bit systems use 64-bit absolute address and jumps
                // 12 byte destructive

                // Get function pointers
                long Source_Base = source.MethodHandle.GetFunctionPointer().ToInt64();
                long Destination_Base = destination.MethodHandle.GetFunctionPointer().ToInt64();

                // Native source address
                byte* Pointer_Raw_Source = (byte*)Source_Base;

                // Pointer to insert jump address into native code
                long* Pointer_Raw_Address = (long*)(Pointer_Raw_Source + 0x02);

                // Insert 64-bit absolute jump into native code (address in rax)
                // mov rax, immediate64
                // jmp [rax]
                *(Pointer_Raw_Source + 0x00) = 0x48;
                *(Pointer_Raw_Source + 0x01) = 0xB8;
                *Pointer_Raw_Address = Destination_Base; // ( Pointer_Raw_Source + 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 )
                *(Pointer_Raw_Source + 0x0A) = 0xFF;
                *(Pointer_Raw_Source + 0x0B) = 0xE0;

            }
            else
            {
                // 32-bit systems use 32-bit relative offset and jump
                // 5 byte destructive

                // Get function pointers
                int Source_Base = source.MethodHandle.GetFunctionPointer().ToInt32();
                int Destination_Base = destination.MethodHandle.GetFunctionPointer().ToInt32();

                // Native source address
                byte* Pointer_Raw_Source = (byte*)Source_Base;

                // Pointer to insert jump address into native code
                int* Pointer_Raw_Address = (int*)(Pointer_Raw_Source + 1);

                // Jump offset (less instruction size)
                int offset = (Destination_Base - Source_Base) - 5;

                // Insert 32-bit relative jump into native code
                *Pointer_Raw_Source = 0xE9;
                *Pointer_Raw_Address = offset;
            }

            // done!
            return true;
        }
    }
}