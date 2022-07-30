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

| Name              | Required | Description                                  |
| ----------------- | -------- | -------------------------------------------- |
| `Caption`         | yes      | The caption of the main window and dialogs.  |
| `BaseUri`         | yes      | The URL of the OpenOLAT instance to display. |
| `CredentialsPath` | yes      | The path to the credentials XML file.        |
| `DecryptionKey`   | no       | An optional 256-bit AES key in Base64.       |


Credentials File Format
-----------------------
The credentials need to be stored as an XML file of the following format:

```xml
<Credentials UserName="name" Password="pass" />
```

Its path in the `appSettings` section may contain environment variables.

To create an encrypted XML, you can use the following PowerShell script:

```powershell
Function Encrypt-Credentials {
    Param (
        [byte[]] $Key,
        [string] $UserName,
        [string] $Password
    )

    $xml = [xml]::new()
    $credentials = $xml.AppendChild($xml.CreateElement("Credentials"))
    $credentials.SetAttribute("UserName", $UserName)
    $credentials.SetAttribute("Password", $Password)
    $aes = [System.Security.Cryptography.Aes]::Create()
    Try {
        $aes.Key = $Key
        $eXml = [System.Security.Cryptography.Xml.EncryptedXml]::new()
        $eXml.AddKeyNameMapping("", $aes)
        [System.Security.Cryptography.Xml.EncryptedXml]::ReplaceElement($credentials, $eXml.Encrypt($credentials, ""), $false)
    }
    Finally { $aes.Dispose() }
    Return ($xml)
}
```
