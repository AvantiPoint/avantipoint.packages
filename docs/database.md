By default AvantiPoint packages ships 3 basic providers. For the most part the configuration can be implemented the same as with BaGet, however there are some additional helpers to help you more easily configure your database provider. While you do need to specify the Database provider type you can optionally pass as ConnectionString name to simplify configuration by only providing connection strings in the ConnectionStrings section of Azure App Services.

## MySql and MariaDb

When using MySql or MariaDb, note that you must provide a Database Server Version to properly configure the connection, and you must specify whether you are using MySql or MariaDb.