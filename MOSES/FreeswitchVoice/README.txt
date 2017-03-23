MOSES Freeswitch Module

This module is a replacement for the Halcyon Freeswitch voice module, which was in disrepair.  This has the following features:

1 - It has been modified to behave identically to the Halcyon Vivox voice module.
2 - It uses an external application to hold account data, and to service both Freeswitch and client SLVoice.exe requests.

While this module is not currently fully featured, it is functional at this point to support voice traffic during office hours on the MOSES-Halcyon Grid.


This module requires the following Halcyon.ini Changes:
[FreeSwitchVoice]
        enabled = "true"
        account_service = "http://[ip]:[port]/fsapi"


The account_service field is an html endpoint on the external voice service.  It supports the following:
GET /	                        - performed by this module on startup
POST /freeswitch-config		- performed by a configured Freeswitch installation
GET /getAccountInfo             - used by this module to requisition a voice account with credentials

The Get / expects the following XML response from the external service:
<config>
    <Realm>[freeswitch and external service public IP]</Realm>
    <APIPrefix>[/url/to/client/endpoint]</APIPrefix>
</config>

This information is used to direct SLVoice.exe clients to login to the external service for voice functionality.


On the client facing API of the external service, it services the following requests OpenSimulator Freeswitch style:
POST /viv_get_prelogin.php
POST /viv_signin.php

Functionality beyond this is also beyond this module.
