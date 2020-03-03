# Authentication & Authorization PoC Design for Telepathy based on Oauth 2.0

## Backgroud

Current Telepathy works without security, while the security of HPC pack is based on Active Directory (AD) in windows server, which is not cross platform. Besides, the authentication model is expected to stand alone with authorization in recent software design, so that when some changes happen in authentication, only authentication server needs to update instead of client and service server.

Oauth 2.0 is the industry-standard protocol for authorization, which gives clients a bearer access token to access restricted resource after authentication. Since oauth provides authorization flows with http requests,the authentication and authorization can be extended to linux os. JWT (Json Web Token) is a common implement for access token, because json works well and lightly with http. Jwt contains 3 parts, hearder, payload (including identity claims), and signature.

The communication of the current telepathy between client and service server is based on WCF (Windows Communication Foundation), a TCP framework, while oauth is based on http. Therefore, we need a solution to handle identity from Oauth server for WCF requests. And also it can extend to other communication model in the future, such as GRPC, etc. 

## PoC Design

Oauth 2.0 provides 4 authorization flows for different situation, but target is to get access token. In PoC, we choose resource owner password credentials grant model for simple demo. 

Jwt cannot work with WCF requests, so we are suppose to adapt jwt to a xml format for WCF security framework. We take SAML2, a security token format, for WCF security validation. The PoC workflow is as below:
1. Identity server starts and gives public url.
2. Service server starts and loads discovery documents of identity server (HTTP).
3. Client gets the access token as jwt by user identity (HTTP).
4. Client converts jwt to SAML2, and create WCF channel with SAML2.
5. Client sends a request by channel (WCF).
6. Service server validates the security token of channel by transfering the SAML2 to jwt and checking if the jwt is consistent and has access to the service.
7. If available, service server takes the services and gives response. Otherwise, returns unauthored exception (WCF).

In the demo, passing security token validation does not mean the identity has the permission for all the service in server. Some operations need specific user instead of the role. So the service Api works with the identity information which is in JWT payload claims. We check if the identity is available to the service api before operating.   

## Try by Yourself

### Prerequisites

Install Visual Studio 2019 and .NET Core 3.0 or later.

### Run with Command Line

After building the project, 
1. Run `IdentityServer.exe` (With SelfHost)
2. Run `WcfService.exe` 
3. Run `WcfClient.exe alice alice` (There are 2 test users in identity Server, `alice` and `bob`, username password are same)
4. In WcfClient console, there are 5 operate commands, `add` *message* (add a message for the client), `echo` (get all messages from all clients), `update` *messageOld messageNew* (update message), `info` (get the client info), and `exit`. Eg `add HelloWorld`

### Expected Result

Client cannot access to the service server without available identity. And each client can add messages for its own, update the message if it adds, echo for all the messages in the server, and get the client information for its own.
