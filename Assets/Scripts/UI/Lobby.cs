using UnityEngine;
using System.Collections.Generic;
using System;

public class Lobby : MonoBehaviour {
        public static string localPlayerName = "";
        public static string hostAddress = "";
        public static string hostPort = "11235";

        ComSat comSat;
        int scrollMode;

        private Vector2 replayListScrollPosition;

        void Start() {
                comSat = FindObjectOfType<ComSat>();
                localPlayerName = PlayerPrefs.GetString("localPlayerName", Environment.UserName);
                hostAddress = PlayerPrefs.GetString("hostAddress", "");
                hostPort = PlayerPrefs.GetString("hostPort", "11235");
                scrollMode = PlayerPrefs.GetInt("Scroll Mode");
        }

        void OnGUI() {
                if(comSat.connectionState == ComSat.ConnectionState.Connecting) {
                        ConnectingGUI();
                } else if(comSat.connectionState == ComSat.ConnectionState.Lobby) {
                        LobbyGUI();
                } else {
                        ConnectGUI();
                }
        }

        void StateDumpButton() {
                if(comSat.fullDump) {
                        if(GUILayout.Button("Full State Dump Enabled")) {
                                comSat.fullDump = false;
                        }
                } else {
                        if(GUILayout.Button("Full State Dump Disabled")) {
                                comSat.fullDump = true;
                        }
                }
        }

        void TraceButton() {
                if(comSat.enableActionTracing) {
                        if(GUILayout.Button("Action Tracing Enabled (very verbose!)")) {
                                comSat.enableActionTracing = false;
                        }
                } else {
                        if(GUILayout.Button("Action Tracing Disabled")) {
                                comSat.enableActionTracing = true;
                        }
                }
        }

        void GameSpeedButtons() {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Game Speed: ");
                float[] speeds = { 0.25f, 0.5f, 1.0f, 2.0f, 4.0f, 10.0f };
                foreach(var speed in speeds) {
                        GUI.backgroundColor = (comSat.timeAccel == speed) ? Color.green : Color.white;
                        if(GUILayout.Button(speed.ToString() + "x")) {
                                comSat.timeAccel = speed;
                        }
                        GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();
        }

        void ConnectGUI() {
		GUILayout.BeginArea(new Rect (100, 50, Screen.width-200, Screen.height-100));
                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Player Name: ");
                localPlayerName = GUILayout.TextField(localPlayerName, 40);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Server Address: ");
                hostAddress = GUILayout.TextField(hostAddress, 40);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Server port: ");
                hostPort = GUILayout.TextField(hostPort, 40);
                GUILayout.EndHorizontal();

		if(GUILayout.Button("Connect")) {
                        comSat.Connect(hostAddress, System.Convert.ToInt32(hostPort));
                        SaveSettngs();
                }
		if(GUILayout.Button("Host")) {
                        comSat.Host(System.Convert.ToInt32(hostPort));
                        SaveSettngs();
                }

                StateDumpButton();
                TraceButton();
                GameSpeedButtons();

                // Game options.
                scrollMode = GUILayout.Toggle(scrollMode == 1 ? true : false, "Camera Scroll Mode") ? 1 : 0;
                PlayerPrefs.SetInt("Scroll Mode", scrollMode);

                if(System.IO.Directory.Exists("Replays")) {
                        replayListScrollPosition = GUILayout.BeginScrollView(replayListScrollPosition);
                        foreach(var replay in System.IO.Directory.GetFiles("Replays", "*.replay")) {
                                if(GUILayout.Button(replay)) {
                                        comSat.PlayReplay(replay);
                                }
                        }
                        GUILayout.EndScrollView();
                }

                GUILayout.EndVertical();
		GUILayout.EndArea();
        }

        private void SaveSettngs() {
                PlayerPrefs.SetString("localPlayerName", localPlayerName);
                PlayerPrefs.SetString("hostAddress", hostAddress);
                PlayerPrefs.SetString("hostPort", hostPort);
                PlayerPrefs.Save();
        }

        void ConnectingGUI() {
                GUILayout.BeginArea(new Rect(100, 50, Screen.width-200, Screen.height-100));
                GUILayout.Label("Connecting to " + hostAddress + ":" + hostPort);
                if(GUILayout.Button("Abort")) {
                        comSat.Disconnect();
                }
                GUILayout.EndArea();
        }

        void LobbyGUI() {
		GUILayout.BeginArea(new Rect(100, 50, Screen.width-200, Screen.height-100));
                GUILayout.BeginVertical();

                foreach(var player in comSat.players) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(player.name);
                        GUILayout.Label(player.id.ToString());

                        var prevColour = GUI.backgroundColor;

                        if(player.team != ComSat.spectatorTeam) {
                                GUI.backgroundColor = Utility.TeamColour(player.team);
                        }
                        if(GUILayout.Button(player.team == ComSat.spectatorTeam ? "Spectate" : player.team.ToString()) &&
                           (comSat.isHost || player.id == comSat.localPlayerID)) {
                                comSat.SetPlayerTeam(player, (player.team + 1) % ComSat.teamCount);
                        }
                        GUI.backgroundColor = prevColour;

                        if(comSat.isHost && player.id != 0 && GUILayout.Button("Kick")) {
                                comSat.Kick(player);
                        }
                        GUILayout.EndHorizontal();
                }

		if(comSat.isHost && GUILayout.Button("Start Game")) {
                        comSat.StartGame();
                }
		if(GUILayout.Button(comSat.isHost ? "Close Server" : "Disconnect")) {
                        comSat.Disconnect();
                }

                if(GUILayout.Button(comSat.enableContinuousSyncCheck ? "Continuous Sync Checking Enabled (slow)" : "Continuous Sync Checking Disabled")) {
                        comSat.enableContinuousSyncCheck = !comSat.enableContinuousSyncCheck;
                }

                StateDumpButton();
                TraceButton();

                if(comSat.isHost) {
                        GameSpeedButtons();
                }

                GUILayout.EndVertical();
		GUILayout.EndArea();
        }
}
