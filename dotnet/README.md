# Azure Function to control Chamberlain MyQ-enabled garage doors
## Setup
1. Copy `sample.local.settings.json` -> `local.settings.json`  
1. Publish `MyQFunctionApp.csproj` -> a new Azure Function app
1. Copy the application settings from `local.settings.json` to the Function App in Azure, specifically
    * MyQApplicationId
    * MyQCulture
    * MyQBaseAddress
    * LoginEndpoint
    * GetSystemDetailEndpoint
    * SetStateEndpoint
4. Create an Azure KeyVault instance
4. Enable Managed Identity on the new Azure Function app, and grant it access to the new Azure KeyVault as per [this blog post by Functions PM, Jeff Hollan](https://medium.com/statuscode/getting-key-vault-secrets-in-azure-functions-37620fd20a0b)
4. Add secrets to KeyVault containing your MyQ username & password

## Usage
To use the new Azure Function, make a call to it with the URIs to your username and password secrets contained in either:
* The `x-keyvaultUsernameUri` & `x-keyvaultPwdUri` *headers*, respectively  
or
* JSON body properties of `keyvaultUsernameUri` & `keyvaultPwdUri`, respectively

In addition to the above, include in the JSON body a property `op` set to either [`open`](#secure-by-default) or `close`

### Example
#### Username/Pass as header values
```
POST /api/Function1 HTTP/1.1
Host: functionapp.azurewebsites.net
x-keyvaultUsernameUri: https://foo.vault.azure.net/secrets/myq-username/abcxyz
x-keyvaultPwdUri: https://foo.vault.azure.net/secrets/myq-password/12345
Content-Type: application/json

{ "op" : "close" }
```

#### Username/Pass as body values
```
POST /api/Function1 HTTP/1.1
Host: functionapp.azurewebsites.net
Content-Type: application/json

{
  "op" : "close",
  "keyvaultUsernameUri":"https://foo.vault.azure.net/secrets/myq-username/abcxyz",
  "keyvaultPwdUri":"https://foo.vault.azure.net/secrets/myq-password/12345"
}
```

### Response values

| HTTP Status Code | Reason |
| --- | --- |
| 202 | All good, request was sent to MyQ
| 400 | You didn't send URIs to KeyVault for your credentials, or no user/pass was found at the KeyVault location
| 401 | Tried to hit KeyVault, didn't succeed or tried `op: open` with `CanOpen = false` (see [Secure by default](#secure-by-default))
| 500 | Something else went wrong; check your Application Insights / Function logs to troubleshoot

## Secure by default
By default, the Function will **only allow you to *close* your garage door**. However, you can enable opening it by setting the `CanOpen` application setting on the Function App to `true`.