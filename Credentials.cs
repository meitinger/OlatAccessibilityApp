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
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Serialization;

namespace OlatAccessibilityApp
{
    public class Credentials
    {
        private static readonly XmlSerializer Serializer = new(typeof(Credentials));

        public static Credentials Load()
        {
            XmlDocument doc = new();
            doc.Load(Program.CredentialsPath);
            if (!string.IsNullOrEmpty(Program.DecryptionKey))
            {
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(Program.DecryptionKey);
                EncryptedXml eXml = new(doc);
                eXml.AddKeyNameMapping(string.Empty, aes);
                eXml.DecryptDocument();
            }
            return (Credentials)Serializer.Deserialize(new XmlNodeReader(doc));
        }

        [XmlAttribute]
        public string UserName { get; set; } = string.Empty;

        [XmlAttribute]
        public string Password { get; set; } = string.Empty;
    }
}
