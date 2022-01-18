We commonly many Open Source projects using Shields to provide users an "At a Glance" idea of what the latest package versions are available. This is of course really easy to deal with when our packages are on NuGet.org as we can simply use shields.io to provide us what we need. With a self hosted package feed however it's not as easy. The AvantiPoint Packages feed now includes Shields out of the box in version 3. In order to enable the Shields endpoints simply update your configuration with a Server Name:

```json
{
  "Shields": {
    "ServerName": "AP Packages"
  }
}
```

!!! note
    If the Shields ServerName is null or empty the Shields endpoint will not be enabled, and will return a 404.