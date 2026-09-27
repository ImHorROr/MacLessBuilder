using System.IO;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HorrorTools.MacLessBuilder
{
    // The tool ships two ways: as a UPM package (git URL) and as a .unitypackage imported into Assets/.
    // PackageInfo only finds the first, so the asmdef location covers the second.
    internal static class PackagePaths
    {
        public static string HelperExe => Path.Combine(Root, "Editor", "Bin", "sideload-helper.exe");

        public static string HelpImages => Path.Combine(Root, "Editor", "HelpImages");

        public static string Root
        {
            get
            {
                PackageInfo package = PackageInfo.FindForAssembly(typeof(PackagePaths).Assembly);
                if (package != null)
                {
                    return package.resolvedPath;
                }

                string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(typeof(PackagePaths).Assembly.GetName().Name);
                // <root>/Editor/HorrorTools.MacLessBuilder.Editor.asmdef
                return Path.GetFullPath(Path.GetDirectoryName(Path.GetDirectoryName(asmdef)));
            }
        }
    }
}
