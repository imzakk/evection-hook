using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Doorstop
{
    /// <summary>
    /// UnityDoorstop's hard-coded entry point. It runs before the game's own code, so this class may only
    /// touch BCL types until the assembly resolver is installed.
    /// </summary>
    public static class Entrypoint
    {
        public static void Start()
        {
            try
            {
                var core = Path.GetDirectoryName(
                    Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH") ?? typeof(Entrypoint).Assembly.Location)!;
                Evection.Core.Mono.AssemblyResolver.Install(core);
                Boot(core);
            }
            catch (Exception e)
            {
                // Last resort: there's no logger yet if we got here.
                try { File.WriteAllText("EvectionHook_crash.txt", e.ToString()); }
                catch (IOException) { }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Boot(string core) => Evection.Core.Mono.Bootstrap.Run(core);
    }
}
