﻿[System.Serializable]
public class Net_LoginRequest : NetMessage
{
     public Net_LoginRequest()
    {
        OP = NetOP.LoginRequest;
    }

    public string UsernameOrEmail {set;get;}
    public string Password {set;get;}
}
