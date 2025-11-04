#if UNITY_SERVER
using Amazon.GameLift.Model;
using Aws.GameLift.Server;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class GameLiftServerManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var gamePort = 7779;
        var initSDKOutcome = GameLiftServerAPI.InitSDK();

        if (initSDKOutcome.Success)
        {
            ProcessParameters processParameters = new ProcessParameters(
                (gameSession) =>
                {
                    GameLiftServerAPI.ActivateGameSession();

                },
                (updateGameSession) =>
                {
                    // handle incoming players properly if server is being given new players

                },
                () =>
                {
                    // OnprocessTerminate
                    GameLiftServerAPI.ProcessEnding();
                },
                () =>
                {
                    // Health check callback
                    // check health of any dependencies
                    return true;
                },
                gamePort,
                new LogParameters(new List<string>()
                {
                    // what files to upload when game session ends 
                    "/local/game/logs/gameliftserver.log"
                })
                );

            var processReadyOutcome = GameLiftServerAPI.ProcessReady(processParameters);
            if(processReadyOutcome.Success)
            {
                print("ProcessReady Success");
            } else
            {
                print("ProcessReady Fail: " + processReadyOutcome.Error.ToString());
            }
            
             
        }
        else
        {
            print("InitSDK Fail: " + initSDKOutcome.Error.ToString());
        }
    }


    private void OnApplicationQuit()
    {
        // resets local connection with gamelfit's agent
        GameLiftServerAPI.Destroy();
    }



    
}

#endif
