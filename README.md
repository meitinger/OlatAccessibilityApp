Accessibility Wrapper Application for OpenOLAT
==============================================


Description
-----------
This [WinForm](https://docs.microsoft.com/en-us/dotnet/desktop/winforms/overview/?view=netdesktop-6.0)
application uses [WebView2](https://docs.microsoft.com/en-us/microsoft-edge/webview2/)
to display an [OpenOLAT](https://www.openolat.com/) site with additional
accessibility features.

These features include:
- auto-login using stored credentials
- color inversion
- quick zoom
- assisted reading using [TTS from .NET](https://docs.microsoft.com/en-us/dotnet/api/system.speech.synthesis)


Configuration
-------------
The following `appSettings` in `OlatAccessibilityApp.config` are supported:

| Name           | Required | Description                                  |
| ---------------| -------- | -------------------------------------------- |
| `BaseUri`      | yes      | The URL of the OpenOLAT instance to display. |
| `Caption`      | no       | The caption of the main window and dialogs.  |
| `UserDataPath` | no       | The path where WebView2 stores its data.     |
| `WebView2Path` | no       | The path to the fixed version binaries.      |
