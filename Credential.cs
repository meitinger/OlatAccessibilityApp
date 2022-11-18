/* Copyright (C) 2022, Manuel Meitinger
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace OlatAccessibilityApp
{
    internal class Credential
    {
        #region Win32

        private const int CRED_MAX_ATTRIBUTES = 64;
        private const uint CRED_PACK_GENERIC_CREDENTIALS = 4;
        private const uint CRED_PERSIST_ENTERPRISE = 3;
        private const uint CRED_TYPE_GENERIC = 1;
        private const uint CREDUIWIN_CHECKBOX = 2;
        private const uint CREDUIWIN_GENERIC = 1;
        private const int ERROR_CANCELLED = 1223;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_NOT_FOUND = 1168;
        private const int ERROR_SUCCESS = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIALW
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string? Comment;
            public long LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string? TargetAlias;
            public string UserName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFOW
        {
            public int Size;
            public IntPtr Parent;
            public string MessageText;
            public string CaptionText;
            public IntPtr Banner;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
        private static extern void CredFree(IntPtr buffer);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool CredPackAuthenticationBufferW(uint flags, string userName, string password, byte[]? packedCredentials, ref int packedCredentialsSize);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool CredReadW(string targetName, uint type, uint flags, out IntPtr credential);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
        private static extern int CredUIPromptForWindowsCredentialsW(ref CREDUI_INFOW uiInfo, int authError, ref uint authPackage, byte[]? inAuthBuffer, int inAuthBufferSize, out IntPtr outAuthBuffer, out int outAuthBufferSize, ref bool save, uint flags);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool CredUnPackAuthenticationBufferW(uint flags, IntPtr authBuffer, int authBufferSize, StringBuilder userName, ref int maxUserName, StringBuilder domainName, ref int maxDomainName, StringBuilder password, ref int maxPassword);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool CredWriteW(ref CREDENTIALW credential, uint flags);

        #endregion

        public static Credential? Query(IntPtr parentWindowHandle, Credential? existingCedential)
        {
            // incorporate any existing credential
            bool saveCred = false;
            byte[]? oldCredBuffer = null;
            int oldCredBufferSize = 0;
            if (existingCedential is not null)
            {
                if (existingCedential.LastWritten.HasValue)
                {
                    saveCred = true;
                }
                while (!CredPackAuthenticationBufferW(CRED_PACK_GENERIC_CREDENTIALS, existingCedential.UserName, existingCedential.Password, oldCredBuffer, ref oldCredBufferSize))
                {
                    if (Marshal.GetLastWin32Error() is not ERROR_INSUFFICIENT_BUFFER || oldCredBufferSize <= oldCredBuffer?.Length)
                    {
                        throw new Win32Exception();
                    }
                    oldCredBuffer = new byte[oldCredBufferSize];
                }
            }

            // show the dialog
            CREDUI_INFOW uiInfo = new()
            {
                Size = Marshal.SizeOf<CREDUI_INFOW>(),
                Parent = parentWindowHandle,
                MessageText = Program.BaseUri.Host,
                CaptionText = Program.Caption,
            };
            uint authPackage = 0;
            var result = CredUIPromptForWindowsCredentialsW(ref uiInfo, 0, ref authPackage, oldCredBuffer, oldCredBufferSize, out var newCredBuffer, out var newCredBufferSize, ref saveCred, CREDUIWIN_GENERIC | CREDUIWIN_CHECKBOX);
            switch (result)
            {
                case ERROR_SUCCESS: break;
                case ERROR_CANCELLED: return null;
                default: throw new Win32Exception(result);
            }

            // extract the user name and password
            StringBuilder userName = new();
            StringBuilder domain = new();
            StringBuilder password = new();
            try
            {
                var userNameSize = userName.Capacity;
                var domainSize = domain.Capacity;
                var passwordSize = password.Capacity;
                while (!CredUnPackAuthenticationBufferW(0, newCredBuffer, newCredBufferSize, userName, ref userNameSize, domain, ref domainSize, password, ref passwordSize))
                {
                    if (Marshal.GetLastWin32Error() is not ERROR_INSUFFICIENT_BUFFER || (userNameSize <= userName.Capacity && domainSize <= domain.Capacity && passwordSize <= password.Capacity))
                    {
                        throw new Win32Exception();
                    }
                    userName.Capacity = userNameSize;
                    domain.Capacity = domainSize;
                    password.Capacity = passwordSize;
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(newCredBuffer);
            }
            if (domain.Length > 0)
            {
                userName.Insert(0, domain).Insert(domain.Length, '\\');
            }

            // save and return the credential
            if (saveCred)
            {
                var blob = Marshal.StringToHGlobalUni(password.ToString());
                try
                {
                    CREDENTIALW credential = new()
                    {
                        Type = CRED_TYPE_GENERIC,
                        TargetName = Program.BaseUri.Host,
                        CredentialBlobSize = password.Length * 2,
                        CredentialBlob = blob,
                        Persist = CRED_PERSIST_ENTERPRISE,
                        UserName = userName.ToString(),
                    };
                    if (!CredWriteW(ref credential, 0))
                    {
                        throw new Win32Exception();
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(blob);
                }
            }
            return new(userName.ToString(), password.ToString(), saveCred ? DateTime.Now : null);
        }

        public static Credential? Read()
        {
            if (!CredReadW(Program.BaseUri.Host, CRED_TYPE_GENERIC, 0, out var credPtr))
            {
                return Marshal.GetLastWin32Error() is ERROR_NOT_FOUND ? null : throw new Win32Exception();
            }
            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIALW>(credPtr);
                return new(
                    credential.UserName,
                    Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2),
                    DateTime.FromFileTimeUtc(credential.LastWritten).ToLocalTime()
                );
            }
            finally
            {
                CredFree(credPtr);
            }
        }

        private Credential(string userName, string password, DateTime? lastWritten)
        {
            UserName = userName;
            Password = password;
            LastWritten = lastWritten;
        }

        public string UserName { get; }
        public string Password { get; }
        public DateTime? LastWritten { get; }
    }
}
