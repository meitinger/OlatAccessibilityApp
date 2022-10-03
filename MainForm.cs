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
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Windows.Forms;

namespace OlatAccessibilityApp
{
    internal class MainForm : Form
    {
        private readonly Dictionary<Prompt, int> _prompts = new();

        public MainForm()
        {
            SuspendLayout();
            WebView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            WebView.WebMessageReceived += WebView_WebMessageReceived;
            Controls.Add(WebView);
            Synthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
            Synthesizer.SpeakProgress += Synthesizer_SpeakProgress;
            Disposed += (s, e) => Synthesizer.Dispose();
            Icon = Program.Resource("App.ico", stream => new Icon(stream));
            StartPosition = FormStartPosition.WindowsDefaultBounds;
            Text = Program.Caption;
            WindowState = FormWindowState.Maximized;
            ResumeLayout(false);
        }

        private SpeechSynthesizer Synthesizer { get; } = new();

        public WebView2 WebView { get; } = new()
        {
            CreationProperties = new()
            {
                UserDataFolder = Program.UserDataPath,
            },
            Dock = DockStyle.Fill,
        };

        private void PostWebMessage(string name, params JProperty[] properties) => WebView.CoreWebView2.PostWebMessageAsJson(new JObject(properties.Prepend(new JProperty("name", name))).ToString());

        private void Synthesizer_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            if (_prompts.TryGetValue(e.Prompt, out var id) && _prompts.Remove(e.Prompt))
            {
                PostWebMessage("ttsSpeakComplete", new JProperty("id", id));
            }
        }

        private void Synthesizer_SpeakProgress(object sender, SpeakProgressEventArgs e)
        {
            if (_prompts.TryGetValue(e.Prompt, out var id))
            {
                PostWebMessage("ttsSpeakProgress", new JProperty("id", id), new JProperty("position", e.CharacterPosition), new JProperty("count", e.CharacterCount));
            }
        }

        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(Program.Resource("App.js", stream => new StreamReader(stream, Encoding.UTF8).ReadToEnd()));
            }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = JObject.Parse(e.WebMessageAsJson);
            var name = Get<string>("name");
            switch (name)
            {
                case "ttsInitialize":
                    Synthesizer.SetOutputToDefaultAudioDevice();
                    Synthesizer.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, CultureInfo.GetCultureInfo(Get<string>("language")));
                    PostWebMessage("ttsInitialized", new JProperty("voice", Synthesizer.Voice.Name));
                    break;
                case "ttsSpeak":
                    _prompts.Add(Synthesizer.SpeakAsync(Get<string>("text")), Get<int>("id"));
                    break;
                case "ttsSpeakCancelAll":
                    Synthesizer.SpeakAsyncCancelAll();
                    _prompts.Clear();
                    break;
                case "zoom":
                    WebView.ZoomFactor = Get<double>("factor") / 100;
                    break;
                default: throw new NotImplementedException(name);
            }

            T Get<T>(string property) => (message[property] ?? throw new MissingMemberException("WebMessage", property)).ToObject<T>() ?? throw new ArgumentNullException(property);
        }
    }
}
