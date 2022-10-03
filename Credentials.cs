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
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct Credential
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
        private struct UIInfo
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
        private static extern int CredUIPromptForWindowsCredentialsW(ref UIInfo uiInfo, int authError, ref uint authPackage, byte[]? inAuthBuffer, int inAuthBufferSize, out IntPtr outAuthBuffer, out int outAuthBufferSize, ref bool save, uint flags);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool CredUnPackAuthenticationBufferW(uint flags, IntPtr authBuffer, int authBufferSize, StringBuilder userName, ref int maxUserName, StringBuilder domainName, ref int maxDomainName, StringBuilder password, ref int maxPassword);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern bool CredWriteW(ref Credential credential, uint flags);

        #endregion

        public static Credential? Query(IntPtr parentWindowHandle, Credential? existingCedential)
        {
            // incorporate existing credentials
            bool saveCred = false;
            byte[]? oldCredBuffer = null;
            int oldCredBufferSize = 0;
            if (existingCedential.HasValue)
            {
                if (existingCedential.Value.Persist is CRED_PERSIST_ENTERPRISE)
                {
                    saveCred = true;
                }
                while (!CredPackAuthenticationBufferW(CRED_PACK_GENERIC_CREDENTIALS, existingCedential.Value.UserName, existingCedential.Value.Password, oldCredBuffer, ref oldCredBufferSize))
                {
                    if (Marshal.GetLastWin32Error() is not ERROR_INSUFFICIENT_BUFFER || oldCredBufferSize <= oldCredBuffer?.Length)
                    {
                        throw new Win32Exception();
                    }
                    oldCredBuffer = new byte[oldCredBufferSize];
                }
            }

            // show the dialog
            UIInfo uiInfo = new()
            {
                Size = Marshal.SizeOf<UIInfo>(),
                Parent = parentWindowHandle,
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

            // build, save and return the credentials
            Credential credentials = new()
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = Program.BaseUri.Host,
                Password = password.ToString(),
                UserName = userName.ToString(),
            };
            if (saveCred)
            {
                credentials.Persist = CRED_PERSIST_ENTERPRISE;
                if (!CredWriteW(ref credentials, 0))
                {
                    throw new Win32Exception();
                }
            }
            return credentials;
        }

        public static Credential? Read()
        {
            if (!CredReadW(Program.BaseUri.Host, CRED_TYPE_GENERIC, 0, out var credPtr))
            {
                return Marshal.GetLastWin32Error() is ERROR_NOT_FOUND ? null : throw new Win32Exception();
            }
            try
            {
                return Marshal.PtrToStructure<Credential>(credPtr);
            }
            finally
            {
                CredFree(credPtr);
            }
        }

        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        private int CredentialBlobSize;
        private string CredentialBlob;
        public uint Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;

        public string Password
        {
            get => CredentialBlob;
            set
            {
                CredentialBlob = value;
                CredentialBlobSize = CredentialBlob.Length * 2;
            }
        }
    }
}
