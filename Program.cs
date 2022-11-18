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

using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Web;
using System.Windows.Forms;

namespace OlatAccessibilityApp
{
    internal static class Program
    {
        public static T GetResource<T>(string name, Func<Stream, T> reader)
        {
            using var stream = typeof(Program).Assembly.GetManifestResourceStream(typeof(Program), name);
            return reader(stream);
        }

        private static string? GetSetting([CallerMemberName] string name = "") => ConfigurationManager.AppSettings.Get(name);

        public static Uri BaseUri => new(GetSetting() ?? throw new ConfigurationErrorsException());

        public static string Caption => GetSetting() ?? "OpenOlat - infinite learning";

        public static string UserDataPath => GetSetting() switch
        {
            null or "" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), typeof(Program).Assembly.GetName().Name),
            string path => Environment.ExpandEnvironmentVariables(path),
        };

        public static string? WebView2Path => GetSetting() switch
        {
            null or "" => null,
            string path => Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), Environment.ExpandEnvironmentVariables(path)),
        };

        [STAThread]
        public static void Main()
        {
            Credential? credential = null;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm mainForm = new();
            SplashForm splashForm = new();
            ApplicationContext context = new(splashForm);
            mainForm.WebView.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
            mainForm.WebView.NavigationCompleted += OnNavigationComplete;
            mainForm.WebView.NavigationStarting += OnNavitationStarting;
            mainForm.WebView.EnsureCoreWebView2Async();
            Application.Run(context);

            void Login(bool alwaysQueryForCredential = false)
            {
                if (credential is null || alwaysQueryForCredential)
                {
                    credential = Credential.Query(splashForm.Handle, credential);
                }
                if (credential is not null)
                {
                    // do _not_ use using for content or ReadAsStreamAsync
                    var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                    {
                        { "o_fiooolat_login_name", credential.UserName },
                        { "o_fiooolat_login_pass", credential.Password },
                    });
                    var request = mainForm.WebView.CoreWebView2.Environment.CreateWebResourceRequest
                    (
                        new Uri(BaseUri, "/dmz/1%3A1%3Aoolat_login%3A1%3A0%3Aofo_%3Afid/").AbsoluteUri,
                        "POST",
                        content.ReadAsStreamAsync().Result,
                        content.Headers.ToString()
                    );
                    mainForm.WebView.CoreWebView2.NavigateWithWebResourceRequest(request);
                }
                else
                {
                    splashForm.Close();
                }
            }

            void OnCoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
            {
                if (e.IsSuccess)
                {
                    credential = Credential.Read();
                    Login();
                }
                else
                {
                    ReportError(e.InitializationException.Message);
                    splashForm.Close();
                }
            }

            void OnNavigationComplete(object sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (!e.IsSuccess)
                {
                    if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                    {
                        ReportError(e.WebErrorStatus.ToString());
                        context.MainForm.Close();
                    }
                }
                else if (new Uri(BaseUri, "/auth/").IsBaseOf(mainForm.WebView.Source))
                {
                    if (context.MainForm == splashForm)
                    {
                        context.MainForm = mainForm;
                        mainForm.Show();
                        splashForm.Close();
                    }
                }
                else
                {
                    if (context.MainForm == splashForm)
                    {
                        Login(alwaysQueryForCredential: true);
                    }
                    else
                    {
                        context.MainForm.Close();
                    }
                }
            }

            void OnNavitationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
            {
                if (!BaseUri.IsBaseOf(new Uri(e.Uri)))
                {
                    e.Cancel = true;
                    mainForm.WebView.ExecuteScriptAsync($"window.open({HttpUtility.JavaScriptStringEncode(e.Uri, addDoubleQuotes: true)});");
                }
            }

            void ReportError(string message) => MessageBox.Show(context.MainForm, message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
