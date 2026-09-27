using System;
using System.Runtime.InteropServices;
using System.Text;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace HorrorTools.MacLessBuilder
{
    // Secrets live in Windows Credential Manager only, never in project files, logs or EditorPrefs.
    internal static class CredentialStore
    {
        private const int CredPersistLocalMachine = 2;
        private const int CredTypeGeneric = 1;

        public static void Delete(string target)
        {
            CredDeleteW(target, CredTypeGeneric, 0);
        }

        public static string Read(string target)
        {
            if (!CredReadW(target, CredTypeGeneric, 0, out IntPtr credentialPtr))
            {
                return null;
            }

            try
            {
                var credential = Marshal.PtrToStructure<Credential>(credentialPtr);
                var bytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                CredFree(credentialPtr);
            }
        }

        public static string ReadUserName(string target)
        {
            if (!CredReadW(target, CredTypeGeneric, 0, out IntPtr credentialPtr))
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStructure<Credential>(credentialPtr).UserName;
            }
            finally
            {
                CredFree(credentialPtr);
            }
        }

        public static void Write(string target, string userName, string secret)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(secret);
            IntPtr blob = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, blob, bytes.Length);
                var credential = new Credential
                {
                    Type = CredTypeGeneric,
                    TargetName = target,
                    UserName = userName,
                    CredentialBlob = blob,
                    CredentialBlobSize = bytes.Length,
                    Persist = CredPersistLocalMachine,
                };
                if (!CredWriteW(ref credential, 0))
                {
                    throw new InvalidOperationException(
                        $"Windows Credential Manager refused to save '{target}' (error {Marshal.GetLastWin32Error()}).");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(blob);
            }
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDeleteW(string target, int type, int flags);

        [DllImport("advapi32.dll")]
        private static extern void CredFree(IntPtr buffer);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredReadW(string target, int type, int reservedFlag, out IntPtr credential);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWriteW(ref Credential credential, int flags);

        // Layout of the Win32 CREDENTIALW struct.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }
    }
}
