using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lobby = Unity.Services.Lobbies.Models.Lobby;
using LobbyPlayer = Unity.Services.Lobbies.Models.Player;

public class NewLobbyManager : MonoBehaviour
{
    public Button createLobbyButton;
    public Button joinLobbyButton;
    public Button leaveLobbyButton;

    public InputField joinCodeInput;
    public InputField playerNameInput;
    public Text statusText;
    public Text lobbyInfoText;

    public Transform playerListContainer;
    public GameObject playerNamePrefab;

    private Lobby createdLobby;
    private float heartbeatTimer;
    private float pollTimer;

    private async void Start()
    {
        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            statusText.text = "Signed in!";
        }
        catch (Exception e)
        {
            statusText.text = "Sign-in failed: " + e.Message;
        }

        createLobbyButton.onClick.AddListener(CreateLobby);
        joinLobbyButton.onClick.AddListener(JoinLobbyFromInput);
        leaveLobbyButton.onClick.AddListener(LeaveLobby);
    }

    private void Update()
    {
        if (createdLobby != null)
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= 15f)
            {
                LobbyService.Instance.SendHeartbeatPingAsync(createdLobby.Id);
                heartbeatTimer = 0f;
            }

            pollTimer += Time.deltaTime;
            if (pollTimer >= 2f)
            {
                _ = RefreshLobby();
                pollTimer = 0f;
            }
        }
    }

    private LobbyPlayer GetLocalPlayer()
    {
        string playerName = string.IsNullOrWhiteSpace(playerNameInput.text) ? "Player" : playerNameInput.text;
        return new LobbyPlayer(id: AuthenticationService.Instance.PlayerId, data: new Dictionary<string, PlayerDataObject>
        {
            { "DisplayName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
        });
    }

    public async void CreateLobby()
    {
        try
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetLocalPlayer()
            };

            createdLobby = await LobbyService.Instance.CreateLobbyAsync("My New Lobby", 4, options);
            statusText.text = "Created Lobby: " + createdLobby.LobbyCode;
            UpdateLobbyUI();
        }
        catch (Exception e)
        {
            statusText.text = "Lobby creation failed: " + e.Message;
        }
    }

    public void JoinLobbyFromInput()
    {
        string code = joinCodeInput.text.Trim();
        if (!string.IsNullOrEmpty(code))
        {
            _ = JoinLobby(code);
        }
        else
        {
            statusText.text = "Please enter a valid lobby code.";
        }
    }

    public async Task JoinLobby(string code)
    {
        try
        {
            var options = new JoinLobbyByCodeOptions
            {
                Player = GetLocalPlayer()
            };

            createdLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);
            statusText.text = "Joined Lobby: " + createdLobby.Name;
            UpdateLobbyUI();
        }
        catch (Exception e)
        {
            statusText.text = "Join failed: " + e.Message;
        }
    }

    private async Task RefreshLobby()
    {
        try
        {
            if (createdLobby != null)
            {
                createdLobby = await LobbyService.Instance.GetLobbyAsync(createdLobby.Id);
                UpdateLobbyUI();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Refresh failed: " + e.Message);
        }
    }

    private void UpdateLobbyUI()
    {
        if (createdLobby == null) return;

        lobbyInfoText.text = $"Lobby: {createdLobby.Name} | Players: {createdLobby.Players.Count}/{createdLobby.MaxPlayers}";

        foreach (Transform child in playerListContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var player in createdLobby.Players)
        {
            GameObject nameEntry = Instantiate(playerNamePrefab, playerListContainer);
            string name = player.Data != null && player.Data.ContainsKey("DisplayName")
                ? player.Data["DisplayName"].Value
                : player.Id;
            nameEntry.GetComponent<Text>().text = name;
        }
    }

    public async void LeaveLobby()
    {
        if (createdLobby == null) return;

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(createdLobby.Id, AuthenticationService.Instance.PlayerId);
            createdLobby = null;
            statusText.text = "Left the lobby.";
            lobbyInfoText.text = "";
            foreach (Transform child in playerListContainer)
            {
                Destroy(child.gameObject);
            }
        }
        catch (Exception e)
        {
            statusText.text = "Failed to leave: " + e.Message;
        }
    }
}
